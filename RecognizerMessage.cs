namespace Scarlett;

public class RecognizerMessage
{
    public enum MessageType
    {
        Start,
        Stop,
        Resume,
        Restart,
        Shutdown
    }

    public MessageType Type { get; }
    public TaskCompletionSource<bool>? CompletionSource { get; }

    public RecognizerMessage(MessageType type, TaskCompletionSource<bool>? completionSource = null)
    {
        Type = type;
        CompletionSource = completionSource;
    }
}