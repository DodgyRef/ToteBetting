namespace ExactaBetting.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute("ValueBetsResults", typeof(Views.ValueBetsResultsPage));
		Routing.RegisterRoute("AllValueCalculations", typeof(Views.AllValueCalculationsPage));
		Routing.RegisterRoute("AllTrifectaCalculations", typeof(Views.AllTrifectaCalculationsPage));
	}
}
