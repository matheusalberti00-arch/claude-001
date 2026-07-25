using YetAnotherGarminConnectClient;
using YetAnotherGarminConnectClient.Dto;
using YetAnotherGarminConnectClient.Dto.Garmin.Fit;

namespace CorpoSync;

// Resultado simples de uma tentativa de envio, já em linguagem humana.
public class ResultadoGarmin
{
	public bool Sucesso;
	public bool PrecisaCodigo;   // Garmin pediu código de verificação (2FA)
	public bool Passageiro;      // falha que costuma resolver tentando de novo
	public string Mensagem = "";
}

// Envia uma pesagem para o Garmin Connect usando e-mail + senha.
// Reaproveita a biblioteca open-source YetAnotherGarminConnectClient (mesmo
// autor do projeto de referência mi-scale-exporter). Ela cuida do login,
// dos tokens e do 2FA. Nada de senha em servidor: vai direto do celular
// para o Garmin.
public class GarminService
{
	IClient? _cliente;

	// Envia os dados. Se o Garmin pedir código (2FA), retorna PrecisaCodigo=true;
	// aí a tela pede o código e chama de novo passando 'codigo' (reaproveitando
	// a mesma sessão de login).
	public async Task<ResultadoGarmin> EnviarAsync(Medicao m, Perfil p, string email, string senha, string? codigo = null)
	{
		email = email?.Trim() ?? "";
		senha = senha?.Trim() ?? "";

		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
			return new ResultadoGarmin { Mensagem = "Este perfil não tem e-mail/senha do Garmin. Edite o perfil e preencha o login." };

		if (m.PesoKg is not > 0)
			return new ResultadoGarmin { Mensagem = "Não há peso para enviar." };

		// Continuação do 2FA: reaproveita a MESMA sessão que pediu o código.
		if (!string.IsNullOrEmpty(codigo) && _cliente != null)
			return await UmaTentativaAsync(m, p, email, senha, codigo, novaSessao: false);

		// 1ª chamada: o login do Garmin às vezes falha por instabilidade momentânea
		// (ex.: OAuthToken2IsNull). Tentamos até 3 vezes com uma sessão nova.
		ResultadoGarmin r = new();
		for (int tentativa = 1; tentativa <= 3; tentativa++)
		{
			r = await UmaTentativaAsync(m, p, email, senha, "", novaSessao: true);
			if (r.Sucesso || r.PrecisaCodigo || !r.Passageiro)
				return r;
			await Task.Delay(2500);   // falha passageira: espera um pouco e tenta de novo
		}
		return r;
	}

	async Task<ResultadoGarmin> UmaTentativaAsync(Medicao m, Perfil p, string email, string senha, string codigo, bool novaSessao)
	{
		try
		{
			if (novaSessao || _cliente == null)
				_cliente = await ClientFactory.Create(GarminServer.GLOBAL);

			var dados = new GarminWeightScaleDTO
			{
				TimeStamp = m.Quando ?? DateTime.Now,
				Weight = (float)m.PesoKg!.Value,
			};

			if (m.GorduraPct.HasValue)
				dados.PercentFat = (float)m.GorduraPct.Value;

			// Garmin guarda hidratação em % — a balança nos dá a MASSA de água (kg).
			if (m.AguaMassaKg.HasValue && m.PesoKg.Value > 0)
				dados.PercentHydration = (float)(m.AguaMassaKg.Value / m.PesoKg.Value * 100.0);

			// Garmin guarda músculo em kg — a balança nos dá o % de músculo.
			if (m.MusculoPct.HasValue && m.PesoKg.Value > 0)
				dados.MuscleMass = (float)(m.MusculoPct.Value / 100.0 * m.PesoKg.Value);

			if (m.ImcCalculado.HasValue)
				dados.BodyMassIndex = (float)m.ImcCalculado.Value;

			var perfil = new UserProfileSettings
			{
				Gender = p.Sexo == 0 ? Dynastream.Fit.Gender.Male : Dynastream.Fit.Gender.Female,
				Age = p.IdadeAnos(),
				Height = p.AlturaCm,
			};

			var credenciais = new CredentialsData { Email = email, Password = senha };

			var r = await _cliente!.UploadWeight(dados, perfil, credenciais, codigo ?? "");

			if (r.MFACodeRequested || r.AuthStatus == AuthStatus.MFARedirected)
				return new ResultadoGarmin { PrecisaCodigo = true, Mensagem = "O Garmin pediu um código de verificação (2FA)." };

			if (r.IsSuccess)
			{
				_cliente = null; // envio concluído: descarta a sessão
				return new ResultadoGarmin { Sucesso = true, Mensagem = "Enviado pro Garmin com sucesso!" };
			}

			var bruto = (r.ErrorLogs != null && r.ErrorLogs.Count > 0) ? r.ErrorLogs[0] : r.AuthStatus.ToString();
			_cliente = null;
			return new ResultadoGarmin { Mensagem = Traduzir(r.AuthStatus, bruto), Passageiro = EhPassageiro(r.AuthStatus) };
		}
		catch (Exception ex)
		{
			_cliente = null;
			return new ResultadoGarmin { Mensagem = "Não consegui enviar pro Garmin: " + ex.Message, Passageiro = true };
		}
	}

	// Falhas que costumam ser passageiras (vale tentar de novo automaticamente).
	static bool EhPassageiro(AuthStatus s) => s switch
	{
		AuthStatus.OAuthToken2IsNull or
		AuthStatus.OAuthToken2IsNullFromSavedOAuth1 or
		AuthStatus.OAuth2TokensProblem or
		AuthStatus.OAuth1TokensProblem or
		AuthStatus.OAuth1TokensAreEmpty or
		AuthStatus.SuccessButCouldNotFindServiceTicket or
		AuthStatus.SuccessButTicketIsEmpty or
		AuthStatus.InitCookiesError or
		AuthStatus.CSRFTokenNotFound or
		AuthStatus.CSRFTokenEmpty or
		AuthStatus.CSRFTokenCannotParse => true,
		_ => false
	};

	// Traduz os erros técnicos do Garmin para frases simples.
	static string Traduzir(AuthStatus s, string bruto) => s switch
	{
		AuthStatus.AuthenticationFailedCheckCredencials => "E-mail ou senha do Garmin incorretos. Confira no perfil.",
		AuthStatus.AuthenticationFailed => "Não consegui entrar na conta do Garmin. Confira o e-mail e a senha.",
		AuthStatus.InvalidMFACode => "O código de verificação (2FA) estava errado. Tente de novo.",
		AuthStatus.AuthBlockedByCloudFlare or AuthStatus.MFAAuthBlockedByCloudFlare =>
			"O Garmin bloqueou o acesso por segurança. Espere alguns minutos e tente de novo.",
		AuthStatus.OAuthToken2IsNull or AuthStatus.OAuthToken2IsNullFromSavedOAuth1
			or AuthStatus.OAuth2TokensProblem or AuthStatus.OAuth1TokensProblem
			or AuthStatus.OAuth1TokensAreEmpty =>
			"O Garmin não concluiu o login (instabilidade momentânea). Tente enviar de novo; se persistir, confira a senha ou se sua conta usa código de verificação (2FA).",
		_ => "Não consegui enviar pro Garmin. (" + bruto + ")"
	};
}
