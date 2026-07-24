using System.Text.Json;
using Microsoft.Maui.Storage;

namespace CorpoSync;

// Guarda os perfis no celular (lista em Preferences; senha do Garmin no cofre seguro).
public static class PerfilStore
{
	const string KEY = "perfis_json";
	const string ATIVO = "perfil_ativo_id";

	public static List<Perfil> Carregar()
	{
		var json = Preferences.Default.Get(KEY, "");
		if (string.IsNullOrWhiteSpace(json))
			return new List<Perfil>();
		try
		{
			return JsonSerializer.Deserialize<List<Perfil>>(json) ?? new List<Perfil>();
		}
		catch
		{
			return new List<Perfil>();
		}
	}

	public static void Salvar(List<Perfil> perfis)
		=> Preferences.Default.Set(KEY, JsonSerializer.Serialize(perfis));

	public static string AtivoId
	{
		get => Preferences.Default.Get(ATIVO, "");
		set => Preferences.Default.Set(ATIVO, value ?? "");
	}

	public static Perfil? Ativo()
	{
		var lista = Carregar();
		var id = AtivoId;
		return lista.FirstOrDefault(p => p.Id == id) ?? lista.FirstOrDefault();
	}

	// Cria um perfil inicial "Eu" na primeira vez, reaproveitando o usuário
	// que já foi registrado na balança nos testes (se houver).
	public static void GarantirPerfilInicial()
	{
		var lista = Carregar();
		if (lista.Count > 0)
			return;

		var eu = new Perfil
		{
			Nome = "Eu",
			Sexo = 0,
			AnoNasc = 1996,
			MesNasc = 1,
			DiaNasc = 3,
			AlturaCm = 170,
			ScaleUserIndex = Preferences.Default.Get("userIndex", -1)
		};
		lista.Add(eu);
		Salvar(lista);
		AtivoId = eu.Id;
	}

	public static async Task SalvarSenhaAsync(string id, string senha)
	{
		try { await SecureStorage.Default.SetAsync($"pwd_{id}", senha ?? ""); }
		catch { }
	}

	public static async Task<string> LerSenhaAsync(string id)
	{
		try { return await SecureStorage.Default.GetAsync($"pwd_{id}") ?? ""; }
		catch { return ""; }
	}
}
