namespace ExactaBetting.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute("AllValueCalculations", typeof(Views.AllValueCalculationsPage));
	}
}
