namespace CorpoSync;

public partial class PerfisPage : ContentPage
{
	public PerfisPage()
	{
		InitializeComponent();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Lista.SelectedItem = null;
		Lista.ItemsSource = PerfilStore.Carregar();
	}

	async void OnNovoClicked(object sender, EventArgs e)
	{
		await Navigation.PushAsync(new PerfilEditPage(null));
	}

	async void OnSelecionado(object sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is Perfil p)
		{
			Lista.SelectedItem = null;
			await Navigation.PushAsync(new PerfilEditPage(p.Id));
		}
	}
}
