namespace CorpoSync;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		UserAppTheme = AppTheme.Dark;

		MainPage = new NavigationPage(new MainPage())
		{
			BarBackgroundColor = Color.FromArgb("#15221F"),
			BarTextColor = Colors.White
		};
	}
}
