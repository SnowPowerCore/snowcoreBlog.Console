using MinimalStepifiedSystem.Interfaces;
using snowcoreBlog.ApplicationLaunch.Context;
using snowcoreBlog.ApplicationLaunch.Delegates;
using snowcoreBlog.HttpClientInterception.Interceptor.HttpClient;
using snowcoreBlog.HttpClientInterception.Interfaces;

namespace snowcoreBlog.Console.App.Steps.Launch.EveryTime;

public class SampleInterceptHttpClientWithHeaderStep : IStep<LaunchDelegate, LaunchContext>
{
    public SampleInterceptHttpClientWithHeaderStep(IHttpClientInterceptor httpClientInterceptor)
    {
        httpClientInterceptor.BeforeSendAsync += HttpClientBeforeSendAsync;
    }

    public Task InvokeAsync(LaunchContext context, LaunchDelegate next, CancellationToken token = default) =>
        next(context);

    private Task HttpClientBeforeSendAsync(object sender, HttpClientInterceptorEventArgs e)
    {
        e.Request.Headers.Add("X-Test", "test");
        return Task.CompletedTask;
    }
}