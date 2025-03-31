using Hanssens.Net;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using snowcoreBlog.ApplicationLaunch.Implementations.BackgroundServices;
using snowcoreBlog.ApplicationLaunch.Interfaces;
using snowcoreBlog.Console.App.Interfaces;
using snowcoreBlog.Console.App.Models;
using snowcoreBlog.Console.App.Options;
using snowcoreBlog.Console.App.Screens;
using snowcoreBlog.Console.App.Services;
using snowcoreBlog.Console.App.Steps.Launch.EveryTime;
using snowcoreBlog.ConsoleHandling.Implementations.Services;
using snowcoreBlog.ConsoleHandling.Interfaces;
using snowcoreBlog.HttpClientInterception.Implementations.Extensions;
using snowcoreBlog.HttpClientInterception.Interfaces;
using snowcoreBlog.LocalStorage.Implementations.Services;
using snowcoreBlog.LocalStorage.Interfaces;
using snowcoreBlog.ServiceDefaults.Extensions;
using snowcoreBlog.TelemetryHandling.Implementations.Services;
using snowcoreBlog.TelemetryHandling.Interfaces;
using snowcoreBlog.VersionTracking.Implementations.Services;
using snowcoreBlog.VersionTracking.Interfaces;

var builder = Host.CreateApplicationBuilder(args);
builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
{
    ValidateScopes = true,
    ValidateOnBuild = true
}));

builder.Configuration.SetFileProvider(new EmbeddedFileProvider(typeof(Program).Assembly));
var jsonFiles = Directory.EnumerateFiles("Config", "*.json", SearchOption.AllDirectories);
foreach (var path in jsonFiles)
    builder.Configuration.AddJsonFile(path, optional: true);
builder.Configuration.AddJsonFile("appsettings.json", optional: false);

var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .Or<TimeoutRejectedException>() // Thrown by Polly's TimeoutPolicy if the inner call gets timeout.
    .WaitAndRetryAsync([
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10)
    ]);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<ILocalStorage>(static sp =>
    new LocalStorage(new LocalStorageConfiguration { AutoLoad = true, AutoSave = true }));
builder.Services.AddSingleton<ILocalStorageService>(static sp =>
    new SingleFileLocalStorageService(sp.GetRequiredService<ILocalStorage>()));
builder.Services.AddSingleton<IConsoleService>(static sp => new StandardConsoleService());
builder.Services.AddSingleton<IConsoleNavigationService>(static sp =>
    new ConsoleNavigationService(sp.GetRequiredService<IOptions<KnownScreenOptions>>(),
        sp.GetRequiredService<IServiceProvider>()));
builder.Services.AddSingleton<IApplicationLaunchService>(static sp =>
    new ConsoleApplicationLaunchService(sp, sp.GetRequiredService<IVersionTrackingService>()));
builder.Services.AddSingleton<IConsoleApplicationService>(static sp =>
    new ConsoleApplicationService(sp.GetRequiredService<IHostApplicationLifetime>(),
        sp.GetRequiredService<IConsoleApplicationInfrastructureService>(), sp.GetRequiredService<ITelemetryService>(),
        sp.GetRequiredService<IVersionTrackingService>()));
builder.Services.AddSingleton<IConsoleApplicationInfrastructureService>(static sp =>
    new ConsoleApplicationInfrastructureService(sp.GetRequiredService<IConsoleNavigationService>(),
        sp.GetRequiredService<IConsoleService>()));
builder.Services.AddSingleton<IVersionTrackingService>(static sp =>
    new LocalVersionTrackingService(sp.GetRequiredService<ILocalStorageService>()));
builder.Services.AddSingleton<TelemetryClient>();
builder.Services.AddSingleton<ITelemetryService>(static sp =>
    new ApplicationInsightsTelemetryService(sp.GetRequiredService<TelemetryClient>()));
// builder.Services
//     .AddRefitClient<ISampleApi>()
//     .ConfigureHttpClient((sp, c) =>
//     {
//         c.BaseAddress = new Uri("https://loripsum.net");
//         c.EnableIntercept(sp.GetRequiredService<HttpClientInterceptor>());
//     })
//     .AddPolicyHandler(retryPolicy);
builder.Services.AddHttpClientInterceptor();

builder.Services.AddSingleton(static sp => new HandleLaunchErrorsStep(sp.GetRequiredService<IConsoleApplicationService>()));
builder.Services.AddSingleton(static sp => new SampleInterceptHttpClientWithHeaderStep(
    sp.GetRequiredService<IHttpClientInterceptor>()));
builder.Services.AddSingleton(static sp => new NavigateToRootScreenStep(sp.GetRequiredService<IConsoleNavigationService>()));

builder.Services.AddSingleton(static sp => new ScreenBase(sp.GetRequiredService<IConsoleApplicationService>()));
builder.Services.AddSingleton(static sp => new MainScreen(sp.GetRequiredService<IConsoleApplicationService>()));

builder.Services.Configure<KnownScreenOptions>(static ks =>
{
    ks.KnownScreenTypes.Add("_default", ks.RootScreenType = typeof(MainScreen));
});

builder.Services.AddOptions();

builder.Services.AddHostedService(static sp =>
    new ApplicationLaunchWorker(sp.GetRequiredService<IHostApplicationLifetime>(),
        sp.GetRequiredService<IApplicationLaunchService>()));

await builder.Build().RunAsync();