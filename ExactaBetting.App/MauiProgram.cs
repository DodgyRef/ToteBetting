using ExactaBetting.App.Services;
using ExactaBetting.App.ViewModels;
using ExactaBetting.Core.Services;
using Microsoft.Extensions.Logging;

namespace ExactaBetting.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<IToteApiService, SampleToteApiService>();
		builder.Services.AddSingleton<IValueCalculator, ValueCalculator>();
		builder.Services.AddSingleton<ValueBetService>();
		builder.Services.AddTransient<MainViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
