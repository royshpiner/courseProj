namespace Backend.Queues;

public sealed class InMemoryMessageQueue : IMessageQueue
{
    private readonly List<string> _messages = new();

    public IReadOnlyList<string> Messages => _messages.AsReadOnly();

    public void Send(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _messages.Add(message);
    }
}