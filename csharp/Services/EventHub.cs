namespace ScalanceLogs.Services;

public record RawSyslogMessage(string Timestamp, string SrcIp, string Label, int Severity, string Message, string Line);

public static class EventHub
{
    public static event Action<RawSyslogMessage>? MessageReceived;
    public static void Publish(RawSyslogMessage msg) => MessageReceived?.Invoke(msg);
}
