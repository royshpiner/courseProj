namespace Backend.Queues;

public interface IMessageQueue
{
    void Send(string message);
}