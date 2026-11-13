using Apizr;
using FluentValidation;
using Hanssens.Net;
using Ixnas.AltchaNet;
using MediatR;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MinimalStepifiedSystem.Extensions;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using RazorConsole.Core;
using Refit;
using snowcoreBlog.ApplicationLaunch.Implementations.BackgroundServices;
using snowcoreBlog.ApplicationLaunch.Interfaces;
using snowcoreBlog.Console.App.Components;
using snowcoreBlog.Console.App.Interfaces;
using snowcoreBlog.Console.App.Options;
using snowcoreBlog.Console.App.Protocol.Sse;
using snowcoreBlog.Console.App.Services;
using snowcoreBlog.Console.App.Steps.Launch.EveryTime;
using snowcoreBlog.ConsoleHandling.Implementations.Services;
using snowcoreBlog.ConsoleHandling.Interfaces;
using snowcoreBlog.HttpClientInterception.Implementations.Extensions;
using snowcoreBlog.HttpClientInterception.Interfaces;
using snowcoreBlog.LocalStorage.Implementations.Services;
using snowcoreBlog.LocalStorage.Interfaces;
using snowcoreBlog.PublicApi.Api;
using snowcoreBlog.PublicApi.Extensions;
using snowcoreBlog.PublicApi.Validation.Dto;
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

// RazorConsole hosts the terminal UI; the root component (Components.App) renders
// the router, the layout and every screen. Mouse events are enabled so the
// razorconsole tooling can drive the UI during development.
builder.UseRazorConsole<App>(configure: config =>
{
    config.Services.Configure<ConsoleAppOptions>(options =>
    {
        options.EnableTerminalResizing = true;
        options.ConsoleLiveDisplayOptions.EnableMouseEvents = true;
    });
});

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

// Gateway-bound API surface (console counterpart of the frontend Apizr managers):
// one shared, application-wide cookie jar carries the reader token pair across
// every service, mirroring the browser behaviour of the Blazor frontend.
builder.Services.Configure<ConsoleApiOptions>(builder.Configuration.GetSection(ConsoleApiOptions.SectionName));
var gatewayBaseUrl = builder.Configuration.GetSection(ConsoleApiOptions.SectionName).Get<ConsoleApiOptions>()?.GatewayBaseUrl
    ?? new ConsoleApiOptions().GatewayBaseUrl;

builder.Services.AddSingleton<ConsoleAuthCookies>();

static Uri GatewayUri(IServiceProvider serviceProvider, string service) =>
    new($"{serviceProvider.GetRequiredService<IOptions<ConsoleApiOptions>>().Value.GatewayBaseUrl}/api/{service}");

builder.Services.ConfigureSnowcoreBlogBackendArticlesApizrManagers(options => options
    .WithBaseAddress($"{gatewayBaseUrl}/api/articles")
    .WithRefitSettings(new RefitSettings
    {
        ContentSerializer = new SystemTextJsonContentSerializer(SystemTextJsonContentSerializer.GetDefaultJsonSerializerOptions())
    })
    .WithRequestTimeout(TimeSpan.FromMinutes(1))
    .WithOperationTimeout(TimeSpan.FromMinutes(3))
    .ConfigureHttpClientBuilder(static http => http
        .ConfigurePrimaryHttpMessageHandler(static sp => sp.GetRequiredService<ConsoleAuthCookies>().CreateHandler())));

builder.Services.ConfigureSnowcoreBlogBackendReadersManagementApizrManagers(options => options
    .WithBaseAddress($"{gatewayBaseUrl}/api/readers")
    .WithRefitSettings(new RefitSettings
    {
        ContentSerializer = new SystemTextJsonContentSerializer(SystemTextJsonContentSerializer.GetDefaultJsonSerializerOptions())
    })
    .WithRequestTimeout(TimeSpan.FromMinutes(1))
    .WithOperationTimeout(TimeSpan.FromMinutes(3))
    .ConfigureHttpClientBuilder(static http => http
        .ConfigurePrimaryHttpMessageHandler(static sp => sp.GetRequiredService<ConsoleAuthCookies>().CreateHandler())));

builder.Services.ConfigureSnowcoreBlogBackendAuthorsManagementApizrManagers(options => options
    .WithBaseAddress($"{gatewayBaseUrl}/api/authors")
    .WithRefitSettings(new RefitSettings
    {
        ContentSerializer = new SystemTextJsonContentSerializer(SystemTextJsonContentSerializer.GetDefaultJsonSerializerOptions())
    })
    .WithRequestTimeout(TimeSpan.FromMinutes(1))
    .WithOperationTimeout(TimeSpan.FromMinutes(3))
    .ConfigureHttpClientBuilder(static http => http
        .ConfigurePrimaryHttpMessageHandler(static sp => sp.GetRequiredService<ConsoleAuthCookies>().CreateHandler())));

builder.Services.ConfigureSnowcoreBlogBackendNotificationsApizrManagers(options => options
    .WithBaseAddress($"{gatewayBaseUrl}/api/notifications")
    .WithRefitSettings(new RefitSettings
    {
        ContentSerializer = new SystemTextJsonContentSerializer(SystemTextJsonContentSerializer.GetDefaultJsonSerializerOptions())
    })
    .WithRequestTimeout(TimeSpan.FromMinutes(1))
    .WithOperationTimeout(TimeSpan.FromMinutes(3))
    .ConfigureHttpClientBuilder(static http => http
        .ConfigurePrimaryHttpMessageHandler(static sp => sp.GetRequiredService<ConsoleAuthCookies>().CreateHandler())));

// Streaming SSE client for the stepified registration processes (reader account
// creation + authenticator confirmation), same role as the frontend
// ReadersManagementSseClient.
builder.Services.AddHttpClient(SseClientNames.ReadersManagement, static (sp, c) =>
{
    c.BaseAddress = GatewayUri(sp, "readers/");
    c.Timeout = TimeSpan.FromMinutes(3);
}).ConfigurePrimaryHttpMessageHandler(static sp => sp.GetRequiredService<ConsoleAuthCookies>().CreateHandler());

// ALTCHA proof-of-work solver: the backend validates a solved challenge on every
// login / registration request (the frontend solves it server-side in its host;
// the terminal app solves it locally).
builder.Services.AddSingleton(static _ => Altcha.CreateSolver());

// Reuse the PublicApi FluentValidation validators directly (same assemblies the
// frontend validates against).
builder.Services.AddValidatorsFromAssemblyContaining<RequestCreateReaderAccountValidator>();

// The generated Apizr registrations enable mediation, so the Apizr mediators need
// an IMediator instance to resolve (the frontend gets one from TimeWarp.State).
builder.Services.AddMediatR(static cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

builder.Services.AddSingleton<ILocalStorage>(static sp =>
    new LocalStorage(new LocalStorageConfiguration { AutoLoad = true, AutoSave = true }));
builder.Services.AddSingleton<ILocalStorageService>(static sp =>
    new SingleFileLocalStorageService(sp.GetRequiredService<ILocalStorage>()));
builder.Services.AddSingleton<IConsoleService>(static sp => new StandardConsoleService());
builder.Services.AddSingleton<IConsoleNavigationService>(static sp =>
    new ConsoleNavigationService(sp.GetRequiredService<IOptions<KnownScreenOptions>>()));
builder.Services.AddSingleton<IApplicationLaunchService>(static sp =>
    new ConsoleApplicationLaunchService(sp.GetRequiredService<IVersionTrackingService>()));
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
builder.Services.AddHttpClientInterceptor();

// Client-facing services (console counterparts of the frontend module services).
builder.Services.AddSingleton<ConsoleAntiforgeryService>();
builder.Services.AddSingleton<ConsoleAccountService>();
builder.Services.AddSingleton<ArticlesConsoleService>();
builder.Services.AddSingleton<AuthorsConsoleService>();
builder.Services.AddSingleton<NotificationsConsoleService>();
builder.Services.AddSingleton<ReadersRegistrationConsoleService>();

builder.Services.AddSingleton(static sp => new HandleLaunchErrorsStep(sp.GetRequiredService<IConsoleApplicationService>()));
builder.Services.AddSingleton(static sp => new SampleInterceptHttpClientWithHeaderStep(
    sp.GetRequiredService<IHttpClientInterceptor>()));
builder.Services.AddSingleton(static sp => new PrefetchClientStateStep(
    sp.GetRequiredService<ConsoleAntiforgeryService>(),
    sp.GetRequiredService<ConsoleAccountService>(),
    sp.GetRequiredService<NotificationsConsoleService>()));
builder.Services.AddSingleton(static sp => new NavigateToRootScreenStep(sp.GetRequiredService<IConsoleNavigationService>()));

builder.Services.Configure<KnownScreenOptions>(static ks =>
{
    ks.KnownScreenRoutes.Add("_default", ks.RootRoute = "/");
    ks.KnownScreenRoutes.Add("about", "/about");
    ks.KnownScreenRoutes.Add("login", "/login");
    ks.KnownScreenRoutes.Add("register", "/register");
    ks.KnownScreenRoutes.Add("register-confirm", "/register/info/confirm");
    ks.KnownScreenRoutes.Add("register-complete", "/register/complete");
    ks.KnownScreenRoutes.Add("become-author", "/author/become");
    ks.KnownScreenRoutes.Add("create-article", "/articles/create");
});

builder.Services.AddOptions();

builder.Services.AddHostedService(static sp =>
    new ApplicationLaunchWorker(sp.GetRequiredService<IHostApplicationLifetime>(),
        sp.GetRequiredService<IApplicationLaunchService>()));

var host = builder.Build();
host.UseStepifiedSystem();
await host.RunAsync();