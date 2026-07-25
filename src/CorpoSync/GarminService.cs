using YetAnotherGarminConnectClient;
using YetAnotherGarminConnectClient.Dto;
using YetAnotherGarminConnectClient.Dto.Garmin.Fit;

namespace CorpoSync;

// Resultado simples de uma tentativa de envio, já em linguagem humana.
public class ResultadoGarmin
{
	public bool Sucesso;
	public bool PrecisaCodigo;   // Garmin pediu código de verificação (2FA)
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
		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
			return new ResultadoGarmin { Mensagem = "Este perfil não tem e-mail/senha do Garmin. Edite o perfil e preencha o login." };

		if (m.PesoKg is not > 0)
			return new ResultadoGarmin { Mensagem = "Não há peso para enviar." };

		try
		{
			// 1ª chamada (sem código): abre uma sessão nova.
			// Chamada com código (2FA): reaproveita a sessão que já pediu o código.
			if (string.IsNullOrEmpty(codigo) || _cliente == null)
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

			var r = await _cliente.UploadWeight(dados, perfil, credenciais, codigo ?? "");

			if (r.MFACodeRequested || r.AuthStatus == AuthStatus.MFARedirected)
				return new ResultadoGarmin { PrecisaCodigo = true, Mensagem = "O Garmin pediu um código de verificação (2FA)." };

			if (r.IsSuccess)
			{
				_cliente = null; // envio concluído: descarta a sessão
				return new ResultadoGarmin { Sucesso = true, Mensagem = "Enviado pro Garmin com sucesso!" };
			}

			var bruto = (r.ErrorLogs != null && r.ErrorLogs.Count > 0) ? r.ErrorLogs[0] : r.AuthStatus.ToString();
			return new ResultadoGarmin { Mensagem = Traduzir(r.AuthStatus, bruto) };
		}
		catch (Exception ex)
		{
			_cliente = null;
			return new ResultadoGarmin { Mensagem = "Não consegui enviar pro Garmin: " + ex.Message };
		}
	}

	// Traduz os erros técnicos do Garmin para frases simples.
	static string Traduzir(AuthStatus s, string bruto) => s switch
	{
		AuthStatus.AuthenticationFailedCheckCredencials => "E-mail ou senha do Garmin incorretos. Confira no perfil.",
		AuthStatus.AuthenticationFailed => "Não consegui entrar na conta do Garmin. Confira o e-mail e a senha.",
		AuthStatus.InvalidMFACode => "O código de verificação (2FA) estava errado. Tente de novo.",
		AuthStatus.AuthBlockedByCloudFlare or AuthStatus.MFAAuthBlockedByCloudFlare =>
			"O Garmin bloqueou o acesso por segurança. Espere alguns minutos e tente de novo.",
		_ => "Não consegui enviar pro Garmin. (" + bruto + ")"
	};
}
