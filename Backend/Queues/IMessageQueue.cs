namespace Backend.Queues;

public interface IMessageQueue
{
    void Send(Guid messageId, string message);
}