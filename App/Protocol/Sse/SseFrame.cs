namespace snowcoreBlog.Console.App.Protocol.Sse;

/// <summary>
/// One parsed server-sent events frame: the event name and the joined data payload.
/// Console-local copy of snowcoreBlog.Frontend.Infrastructure's SseFrame.
/// </summary>
public readonly record struct SseFrame(string EventName, string Data);
