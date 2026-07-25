namespace CorpoSync;

// Uma pesagem já traduzida (dos códigos crus da balança para números).
public class Medicao
{
	public double? PesoKg;
	public double? GorduraPct;
	public double? MusculoPct;
	public double? AguaMassaKg;
	public double? MassaMagraKg;
	public double? BasalKcal;
	public double? ImpedanciaOhm;
	public double? ImcCalculado;
	public DateTime? Quando;   // carimbo de hora que veio dentro da pesagem
	public int? UsuarioId;     // número do usuário (U?) a que esta pesagem pertence

	// Fator de massa dos campos de composição. Empiricamente 0,1 bateu com o
	// app oficial (ex.: massa magra 52,7 kg). Ajustável após conferência.
	const double FATOR_MASSA = 0.1;

	// Canal 2a9d — Weight Measurement (peso).
	public void LerPeso(byte[] d)
	{
		if (d == null || d.Length < 3) return;
		int flags = d[0];
		bool imperial = (flags & 0x01) != 0;
		int peso = d[1] | (d[2] << 8);
		PesoKg = imperial ? peso * 0.01 : peso * 0.005;

		// Carimbo de hora (se presente): bytes 3..9 = ano(2), mês, dia, hora, min, seg.
		if ((flags & 0x02) != 0 && d.Length >= 10)
		{
			try
			{
				int ano = d[3] | (d[4] << 8);
				Quando = new DateTime(ano, d[5], d[6], d[7], d[8], d[9]);
			}
			catch { }
		}
	}

	// Canal 2a9c — Body Composition Measurement (composição).
	public void LerComposicao(byte[] d)
	{
		if (d == null || d.Length < 4) return;
		int flags = d[0] | (d[1] << 8);
		int i = 2;

		int U16()
		{
			int v = d[i] | (d[i + 1] << 8);
			i += 2;
			return v;
		}

		GorduraPct = U16() * 0.1;            // sempre presente

		if ((flags & 0x0002) != 0) i += 7;   // timestamp
		if ((flags & 0x0004) != 0) { UsuarioId = d[i]; i += 1; }   // user id
		if ((flags & 0x0008) != 0) BasalKcal = U16() / 4.184;   // basal metabolism (kJ→kcal)
		if ((flags & 0x0010) != 0) MusculoPct = U16() * 0.1;    // muscle %
		if ((flags & 0x0020) != 0) i += 2;   // muscle mass
		if ((flags & 0x0040) != 0) i += 2;   // fat free mass
		if ((flags & 0x0080) != 0) MassaMagraKg = U16() * FATOR_MASSA; // soft lean mass
		if ((flags & 0x0100) != 0) AguaMassaKg = U16() * FATOR_MASSA;  // body water mass
		if ((flags & 0x0200) != 0) ImpedanciaOhm = U16() * 0.1;        // impedance
	}

	public void CalcularImc(int alturaCm)
	{
		if (PesoKg is > 0 && alturaCm > 0)
		{
			double m = alturaCm / 100.0;
			ImcCalculado = PesoKg / (m * m);
		}
	}

	public bool TemAlgo =>
		PesoKg.HasValue || GorduraPct.HasValue || MusculoPct.HasValue
		|| AguaMassaKg.HasValue || MassaMagraKg.HasValue || BasalKcal.HasValue;
}
