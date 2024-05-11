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
using snowcoreBlog.ApplicationLaunch;
using snowcoreBlog.ApplicationLaunch.Interfaces;
using snowcoreBlog.Console.App.Interfaces;
using snowcoreBlog.Console.App.Models;
using snowcoreBlog.Console.App.Services;
using snowcoreBlog.ConsoleHandling.Interfaces;
using snowcoreBlog.ConsoleHandling.Services;
using snowcoreBlog.ServiceDefaults.Extensions;
using snowcoreBlog.LocalStorage.Interfaces;
using snowcoreBlog.LocalStorage.Services;
using snowcoreBlog.ResourceLoading.Interfaces;
using snowcoreBlog.ResourceLoading.Models;
using snowcoreBlog.ResourceLoading.Services;
using snowcoreBlog.TelemetryHandling.Interfaces;
using snowcoreBlog.TelemetryHandling.Services;
using snowcoreBlog.VersionTracking.Interfaces;
using snowcoreBlog.VersionTracking.Services;
using snowcoreBlog.HttpClientInterception.Extensions;
using snowcoreBlog.HttpClientInterception.Interfaces;
using snowcoreBlog.Console.App.Screens;
using snowcoreBlog.Console.App.Steps.Launch.EveryTime;

var builder = Host.CreateApplicationBuilder(args);

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

builder.Services.AddSingleton<ILocalStorage>(sp =>
    new LocalStorage(new LocalStorageConfiguration { AutoLoad = true, AutoSave = true }));
builder.Services.AddSingleton<ILocalStorageService>(sp =>
    new SingleFileLocalStorageService(sp.GetRequiredService<ILocalStorage>()));
builder.Services.AddSingleton<IResourceService>(sp =>
    new JsonLocalResourceService(sp.GetRequiredService<IOptions<ResourceSettings>>()));
builder.Services.AddSingleton<IConsoleService>(sp => new StandardConsoleService());
builder.Services.AddSingleton<IConsoleNavigationService>(sp =>
    new ConsoleNavigationService(sp.GetRequiredService<IOptions<KnownScreens>>(),
        sp.GetRequiredService<IServiceProvider>()));
builder.Services.AddSingleton<IApplicationLaunchService>(sp =>
    new ConsoleApplicationLaunchService(sp, sp.GetRequiredService<IVersionTrackingService>()));
builder.Services.AddSingleton<IConsoleApplicationService>(sp =>
    new ConsoleApplicationService(sp.GetRequiredService<IHostApplicationLifetime>(),
        sp.GetRequiredService<IConsoleApplicationInfrastructureService>(), sp.GetRequiredService<ITelemetryService>(),
        sp.GetRequiredService<IVersionTrackingService>()));
builder.Services.AddSingleton<IConsoleApplicationInfrastructureService>(sp =>
    new ConsoleApplicationInfrastructureService(sp.GetRequiredService<IConsoleNavigationService>(),
        sp.GetRequiredService<IConsoleService>(), sp.GetRequiredService<IResourceService>()));
builder.Services.AddSingleton<IVersionTrackingService>(sp =>
    new LocalVersionTrackingService(sp.GetRequiredService<ILocalStorageService>()));
builder.Services.AddSingleton<TelemetryClient>();
builder.Services.AddSingleton<ITelemetryService>(sp =>
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

builder.Services.AddSingleton(sp => new HandleLaunchErrorsStep(sp.GetRequiredService<IConsoleApplicationService>()));
builder.Services.AddSingleton(sp => new SampleInterceptHttpClientWithHeaderStep(
    sp.GetRequiredService<IHttpClientInterceptor>()));
builder.Services.AddSingleton(sp => new NavigateToRootScreenStep(sp.GetRequiredService<IConsoleNavigationService>()));

builder.Services.AddSingleton(sp => new ScreenBase(sp.GetRequiredService<IConsoleApplicationService>()));
builder.Services.AddSingleton(sp => new MainScreen(sp.GetRequiredService<IConsoleApplicationService>()));

builder.Services.Configure<ResourceSettings>(rs =>
{
    rs.UseBase = true;
});

builder.Services.Configure<KnownScreens>(ks =>
{
    ks.KnownScreenTypes.Add("_default", ks.RootScreenType = typeof(MainScreen));
});

builder.Services.AddOptions();

builder.Services.AddHostedService(sp =>
    new ProgramWorker(sp.GetRequiredService<IHostApplicationLifetime>(),
        sp.GetRequiredService<IApplicationLaunchService>()));

await builder.Build().RunAsync();