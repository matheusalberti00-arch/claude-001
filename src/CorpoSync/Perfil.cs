namespace CorpoSync;

// Um "perfil favorito": junta os dados do corpo (para a balança calcular)
// e o login do Garmin (para onde a pesagem será enviada).
public class Perfil
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
	public string Nome { get; set; } = "";

	// Dados do corpo (usados no handshake com a balança)
	public int Sexo { get; set; } = 0;      // 0 = masculino, 1 = feminino
	public int AnoNasc { get; set; } = 1990;
	public int MesNasc { get; set; } = 1;
	public int DiaNasc { get; set; } = 1;
	public int AlturaCm { get; set; } = 170;

	// Login do Garmin (a senha fica no cofre seguro do celular, não aqui)
	public string GarminEmail { get; set; } = "";

	// Índice do usuário que este perfil ocupa NA balança (preenchido no 1º uso).
	public int ScaleUserIndex { get; set; } = -1;

	public int IdadeAnos()
	{
		var hoje = DateTime.Today;
		int idade = hoje.Year - AnoNasc;
		var aniv = new DateTime(Math.Clamp(AnoNasc, 1900, hoje.Year), Math.Clamp(MesNasc, 1, 12), Math.Clamp(DiaNasc, 1, 28));
		if (hoje < aniv.AddYears(idade)) idade--;
		return Math.Clamp(idade, 0, 120);
	}
}
