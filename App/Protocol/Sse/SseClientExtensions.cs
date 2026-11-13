using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace snowcoreBlog.Console.App.Protocol.Sse;

/// <summary>
/// Generic consumption of server-sent events streams over POST requests.
/// Console-local copy of snowcoreBlog.Frontend.Infrastructure's SseClientExtensions:
/// understands event:/data: fields, tolerates comments / keep-alive lines and
/// yields one frame per event.
/// </summary>
public static class SseClientExtensions
{
    private const string MessageEventName = "message";

    private static readonly JsonSerializerOptions RequestSerializerOptions = new(JsonSerializerDefaults.Web);

    public static async IAsyncEnumerable<SseFrame> PostServerSentEventsAsync(
        this HttpClient httpClient,
        string? requestUri,
        object? requestBody,
        Action<HttpRequestMessage>? configureRequest = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        if (requestBody is not default(object))
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody, RequestSerializerOptions),
                Encoding.UTF8,
                MediaTypeNames.Application.Json);
        }

        configureRequest?.Invoke(request);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var eventStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(eventStream);

        var eventName = MessageEventName;
        var data = new StringBuilder();

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                if (data.Length > 0)
                {
                    yield return new SseFrame(eventName, data.ToString());
                    eventName = MessageEventName;
                    data.Clear();
                }

                continue;
            }

            if (line.StartsWith(':'))
            {
                continue;
            }

            if (line.StartsWith("data:"))
            {
                if (data.Length > 0)
                {
                    data.Append('\n');
                }

                data.Append(line.AsSpan("data:".Length).TrimStart());
            }
            else if (line.StartsWith("event:"))
            {
                eventName = line["event:".Length..].Trim();
            }
        }

        if (data.Length > 0)
        {
            yield return new SseFrame(eventName, data.ToString());
        }
    }
}
