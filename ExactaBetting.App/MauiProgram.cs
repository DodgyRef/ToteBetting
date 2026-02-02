using System.Net.Http.Headers;
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

		builder.Services.AddHttpClient(ToteGraphQLApiService.HttpClientName, static client =>
		{
			client.BaseAddress = new Uri(ToteGraphQLApiService.ProductionEndpoint);
			// Tote API expects Authorization: Api-Key <key> (same as working Tote.slnx project)
			client.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue(ToteGraphQLApiService.ApiKeyScheme, ToteGraphQLApiService.ApiKeyValue);
		});
		builder.Services.AddSingleton<IToteApiService, ToteGraphQLApiService>();
		builder.Services.AddSingleton<IValueCalculator, ValueCalculator>();
		builder.Services.AddSingleton<ValueBetService>();
		builder.Services.AddSingleton<SpreadsheetExportService>();
		builder.Services.AddTransient<MainViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
