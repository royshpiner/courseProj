namespace Backend.Queues;

public sealed class InMemoryMessageQueue : IMessageQueue
{
    private readonly List<string> _messages = new();

    public IReadOnlyList<string> Messages => _messages.AsReadOnly();

    private readonly HashSet<Guid> _receivedIds = new HashSet<Guid>();

    public void Send(Guid messageId, string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_receivedIds.Add(messageId))
        {
            _messages.Add(message);
        }
    }
}
