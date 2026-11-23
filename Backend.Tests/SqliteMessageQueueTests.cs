using Backend.Queues;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Backend.Tests;

public class SqliteMessageQueueTests
{
    [Fact]
    public void RetryingAfterReopeningKeepsOneReceipt()
    {
        string databasePath = Path.GetTempFileName();

        try
        {
            Guid messageId = Guid.NewGuid();
            string message = "Buy 'ABC'";
            SqliteMessageQueue queue = new SqliteMessageQueue(databasePath);
            Assert.Null(queue.GetReceivedAt(messageId));
            queue.Send(messageId, message);
            DateTimeOffset firstReceiptTime = Assert.IsType<DateTimeOffset>(queue.GetReceivedAt(messageId));
            Assert.Equal(TimeSpan.Zero, firstReceiptTime.Offset);

            SqliteMessageQueue reopenedQueue = new SqliteMessageQueue(databasePath);
            reopenedQueue.Send(messageId, message);
            Assert.Throws<InvalidOperationException>(() => reopenedQueue.Send(messageId, "Different payload"));
            Assert.Equal(firstReceiptTime, reopenedQueue.GetReceivedAt(messageId));

            SqliteConnectionStringBuilder settings = new SqliteConnectionStringBuilder();
            settings.DataSource = databasePath;
            settings.Pooling = false;
            using SqliteConnection connection = new SqliteConnection(settings.ConnectionString);
            connection.Open();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT MessageId, Payload FROM ReceivedMessages;";
            using SqliteDataReader reader = command.ExecuteReader();

            Assert.True(reader.Read());
            Assert.Equal(messageId.ToString(), reader.GetString(0));
            Assert.Equal(message, reader.GetString(1));
            Assert.False(reader.Read());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }
}
