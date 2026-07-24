namespace CorpoSync;

public partial class PerfilEditPage : ContentPage
{
	readonly string? _id;
	Perfil _perfil;

	public PerfilEditPage(string? id)
	{
		InitializeComponent();
		_id = id;

		var lista = PerfilStore.Carregar();
		_perfil = lista.FirstOrDefault(p => p.Id == id) ?? new Perfil();

		ExcluirBtn.IsVisible = id != null;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		NomeEntry.Text = _perfil.Nome;
		SexoPicker.SelectedIndex = _perfil.Sexo == 1 ? 1 : 0;
		try { NascPicker.MinimumDate = new DateTime(1900, 1, 1); NascPicker.MaximumDate = DateTime.Today; } catch { }
		try { NascPicker.Date = new DateTime(_perfil.AnoNasc, _perfil.MesNasc, _perfil.DiaNasc); } catch { NascPicker.Date = new DateTime(1990, 1, 1); }
		AlturaEntry.Text = _perfil.AlturaCm.ToString();
		EmailEntry.Text = _perfil.GarminEmail;

		if (_id != null)
			SenhaEntry.Text = await PerfilStore.LerSenhaAsync(_id);
	}

	async void OnSalvarClicked(object sender, EventArgs e)
	{
		var nome = (NomeEntry.Text ?? "").Trim();
		if (string.IsNullOrWhiteSpace(nome))
		{
			await DisplayAlert("Nome", "Dê um nome ao perfil.", "OK");
			return;
		}

		if (!int.TryParse(AlturaEntry.Text, out int altura) || altura < 80 || altura > 250)
		{
			await DisplayAlert("Altura", "Digite a altura em cm (ex.: 170).", "OK");
			return;
		}

		var nasc = NascPicker.Date ?? new DateTime(1990, 1, 1);
		_perfil.Nome = nome;
		_perfil.Sexo = SexoPicker.SelectedIndex == 1 ? 1 : 0;
		_perfil.AnoNasc = nasc.Year;
		_perfil.MesNasc = nasc.Month;
		_perfil.DiaNasc = nasc.Day;
		_perfil.AlturaCm = altura;
		_perfil.GarminEmail = (EmailEntry.Text ?? "").Trim();

		var lista = PerfilStore.Carregar();
		var existente = lista.FirstOrDefault(p => p.Id == _perfil.Id);
		if (existente == null)
			lista.Add(_perfil);
		else
		{
			existente.Nome = _perfil.Nome;
			existente.Sexo = _perfil.Sexo;
			existente.AnoNasc = _perfil.AnoNasc;
			existente.MesNasc = _perfil.MesNasc;
			existente.DiaNasc = _perfil.DiaNasc;
			existente.AlturaCm = _perfil.AlturaCm;
			existente.GarminEmail = _perfil.GarminEmail;
		}
		PerfilStore.Salvar(lista);
		await PerfilStore.SalvarSenhaAsync(_perfil.Id, SenhaEntry.Text ?? "");

		if (string.IsNullOrEmpty(PerfilStore.AtivoId))
			PerfilStore.AtivoId = _perfil.Id;

		await Navigation.PopAsync();
	}

	async void OnExcluirClicked(object sender, EventArgs e)
	{
		if (_id == null) return;
		bool ok = await DisplayAlert("Excluir", $"Excluir o perfil \"{_perfil.Nome}\"?", "Excluir", "Cancelar");
		if (!ok) return;

		var lista = PerfilStore.Carregar();
		lista.RemoveAll(p => p.Id == _id);
		PerfilStore.Salvar(lista);

		if (PerfilStore.AtivoId == _id)
			PerfilStore.AtivoId = lista.FirstOrDefault()?.Id ?? "";

		await Navigation.PopAsync();
	}
}
