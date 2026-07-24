namespace CorpoSync;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		MainPage = new NavigationPage(new MainPage())
		{
			BarBackgroundColor = Color.FromArgb("#2E7D6F"),
			BarTextColor = Colors.White
		};
	}
}
