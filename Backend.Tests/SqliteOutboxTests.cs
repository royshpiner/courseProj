using System;
using System.IO;
using Backend.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Backend.Tests
{
    public class SqliteOutboxTests
    {
        [Fact]
        public void PendingMessageSurvivesReopeningDatabase()
        {
            string databasePath = Path.GetTempFileName();

            try
            {
                Guid messageId = Guid.NewGuid();
                string message = "Buy 'ABC'";
                SqliteOutbox outbox = new SqliteOutbox(databasePath);
                outbox.SavePending(messageId, message);

                SqliteConnectionStringBuilder settings = new SqliteConnectionStringBuilder();
                settings.DataSource = databasePath;
                settings.Pooling = false;

                using (SqliteConnection connection = new SqliteConnection(settings.ConnectionString))
                {
                    connection.Open();

                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            SELECT COUNT(*) FROM Outbox
                            WHERE MessageId = $id AND Payload = $message
                              AND SentUtc IS NULL;";
                        command.Parameters.AddWithValue("$id", messageId.ToString());
                        command.Parameters.AddWithValue("$message", message);

                        long count = Assert.IsType<long>(command.ExecuteScalar());
                        Assert.Equal(1L, count);
                    }
                }
            }
            finally
            {
                File.Delete(databasePath);
            }
        }

        [Fact]
        public void SecondConfirmationPreservesFirstSendTime()
        {
            string databasePath = Path.GetTempFileName();

            try
            {
                Guid messageId = Guid.NewGuid();
                SqliteOutbox outbox = new SqliteOutbox(databasePath);
                outbox.SavePending(messageId, "Buy ABC");

                DateTimeOffset firstSendTime =
                    new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(2));

                Assert.True(outbox.TryMarkSent(messageId, firstSendTime));
                Assert.False(outbox.TryMarkSent(messageId, firstSendTime.AddMinutes(1)));

                SqliteConnectionStringBuilder settings = new SqliteConnectionStringBuilder();
                settings.DataSource = databasePath;
                settings.Pooling = false;

                using (SqliteConnection connection = new SqliteConnection(settings.ConnectionString))
                {
                    connection.Open();

                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT SentUtc FROM Outbox WHERE MessageId = $id;";
                        command.Parameters.AddWithValue("$id", messageId.ToString());

                        string savedTime = Assert.IsType<string>(command.ExecuteScalar());
                        Assert.Equal(firstSendTime.ToUniversalTime().ToString("O"), savedTime);
                    }
                }
            }
            finally
            {
                File.Delete(databasePath);
            }
        }

        [Fact]
        public void ReopenedOutboxReturnsOnlyUnconfirmedMessagesInQueueOrder()
        {
            string databasePath = Path.GetTempFileName();

            try
            {
                Guid firstId = Guid.NewGuid();
                Guid confirmedId = Guid.NewGuid();
                Guid lastId = Guid.NewGuid();
                SqliteOutbox outbox = new SqliteOutbox(databasePath);
                Assert.Empty(outbox.GetUnconfirmedMessages());

                outbox.SavePending(firstId, "Buy 'ABC'");
                outbox.SavePending(confirmedId, "Already sent");
                outbox.SavePending(lastId, "Cancel ABC");
                Assert.True(outbox.TryMarkSent(confirmedId, DateTimeOffset.UtcNow));

                SqliteOutbox reopenedOutbox = new SqliteOutbox(databasePath);
                IReadOnlyList<(Guid MessageId, string Payload)> messages = reopenedOutbox.GetUnconfirmedMessages();

                Assert.Equal(2, messages.Count);
                Assert.Equal((firstId, "Buy 'ABC'"), messages[0]);
                Assert.Equal((lastId, "Cancel ABC"), messages[1]);
            }
            finally
            {
                File.Delete(databasePath);
            }
        }
    }
}
