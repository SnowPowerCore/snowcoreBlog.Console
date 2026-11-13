using System.Text.Json;
using snowcoreBlog.PublicApi.Utilities.Sse;

namespace snowcoreBlog.Console.App.Protocol.Sse;

/// <summary>
/// Reader for step-progress server-sent events streams emitted by the backend
/// stepified processes. Console-local copy of the frontend StepProgressStreamReader:
/// forwards every intermediate step code to the consumer and returns the terminal
/// event (or null when the stream was aborted mid-way).
/// </summary>
public static class StepProgressStreamReader
{
    public static readonly JsonSerializerOptions FrameDataOptions = new(JsonSerializerDefaults.Web);

    public static async Task<StepProgressFinalEvent?> ReadAsync(
        IAsyncEnumerable<SseFrame> frames,
        Action<string> onStepProgress,
        CancellationToken cancellationToken = default)
    {
        StepProgressFinalEvent? finalEvent = null;

        await foreach (var frame in frames.WithCancellation(cancellationToken))
        {
            switch (frame.EventName)
            {
                case StepProgressEventConstants.StepProgressEvent:
                    var progress = JsonSerializer.Deserialize<StepProgressEvent>(frame.Data, FrameDataOptions);
                    if (progress is not default(StepProgressEvent))
                    {
                        onStepProgress(progress.Step);
                    }
                    break;

                case StepProgressEventConstants.FinalResultEvent:
                    finalEvent = JsonSerializer.Deserialize<StepProgressFinalEvent>(frame.Data, FrameDataOptions);
                    break;
            }
        }

        return finalEvent;
    }
}
