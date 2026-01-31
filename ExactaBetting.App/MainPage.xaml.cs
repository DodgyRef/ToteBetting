using Microsoft.Extensions.DependencyInjection;

namespace ExactaBetting.App;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	protected override void OnHandlerChanged()
	{
		base.OnHandlerChanged();
		if (Handler?.MauiContext?.Services is IServiceProvider services && BindingContext is null)
			BindingContext = services.GetRequiredService<ViewModels.MainViewModel>();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (BindingContext is ViewModels.MainViewModel vm && vm.AvailableRaces.Count == 0 && !vm.IsBusy)
			_ = vm.LoadRacesCommand.ExecuteAsync(default);
	}
}
