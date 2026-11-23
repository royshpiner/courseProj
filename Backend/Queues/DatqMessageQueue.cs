namespace Backend.Queues;

public sealed class DatqMessageQueue : IMessageQueue
{
    private readonly string _server;

    public DatqMessageQueue(string server, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        _server = server;
    }

    public void Send(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Console.WriteLine(
            $"[DATQ simulation] Sent {message.Length} characters to {_server}.");
    }
}