using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using Refit;

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

builder.Services.AddSingleton<ILocalStorage>(sp =>
    new LocalStorage(new LocalStorageConfiguration { AutoLoad = true, AutoSave = true }));
builder.Services.AddSingleton<ILocalStorageService>(sp =>
    new LocalStorageService(sp.GetRequiredService<ILocalStorage>()));
builder.Services.AddSingleton<IResourceService>(sp =>
    new ResourceService(sp.GetRequiredService<IOptions<ResourceSettings>>()));
builder.Services.AddSingleton<IConsoleService>(sp => new ConsoleService());
builder.Services.AddSingleton<INavigationService>(sp =>
    new NavigationService(sp.GetRequiredService<IOptions<KnownScreens>>(),
        sp.GetRequiredService<IServiceProvider>()));
builder.Services.AddSingleton<IApplicationInitService>(sp =>
    new ApplicationInitService(sp, sp.GetRequiredService<IVersionTrackingService>()));
builder.Services.AddSingleton<IApplicationService>(sp =>
    new ApplicationService(sp.GetRequiredService<IHostApplicationLifetime>(),
        sp.GetRequiredService<IApplicationInfrastructureService>(), sp.GetRequiredService<TelemetryClient>(),
        sp.GetRequiredService<IVersionTrackingService>()));
builder.Services.AddSingleton<IApplicationInfrastructureService>(sp =>
    new ApplicationInfrastructureService(sp.GetRequiredService<INavigationService>(),
        sp.GetRequiredService<IConsoleService>(), sp.GetRequiredService<IResourceService>()));
builder.Services.AddSingleton<IVersionTrackingService>(sp =>
    new VersionTrackingService(sp.GetRequiredService<ILocalStorageService>()));
builder.Services.AddSingleton<TelemetryClient>();
builder.Services.AddSingleton<ISampleBusinessLogicService>(sp =>
    new SampleBusinessLogicService(sp.GetRequiredService<IServiceProvider>()));
builder.Services
    .AddRefitClient<ISampleApi>()
    .ConfigureHttpClient((sp, c) =>
    {
        c.BaseAddress = new Uri("https://loripsum.net");
        c.EnableIntercept(sp.GetRequiredService<HttpClientInterceptor>());
    })
    .AddPolicyHandler(retryPolicy);
builder.Services.AddHttpClientInterceptor();

builder.Services.AddSingleton(sp => new HandleLaunchErrorsStep(sp.GetRequiredService<IApplicationService>()));
builder.Services.AddSingleton(sp => new SampleInterceptHttpClientWithHeaderStep(
    sp.GetRequiredService<IHttpClientInterceptor>()));
builder.Services.AddSingleton(sp => new NavigateToRootScreenStep(sp.GetRequiredService<INavigationService>()));
builder.Services.AddSingleton(sp => new RetrieveDataStep(sp.GetRequiredService<IApplicationService>(),
    sp.GetRequiredService<ISampleApi>()));

builder.Services.AddSingleton(sp => new ScreenBase(sp.GetRequiredService<IApplicationService>()));
builder.Services.AddSingleton(sp => new MainScreen(sp.GetRequiredService<IApplicationService>(),
    sp.GetRequiredService<ISampleBusinessLogicService>()));

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
        sp.GetRequiredService<IApplicationInitService>()));

await builder.Build().RunAsync();