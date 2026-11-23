using Backend.Audit;
using Backend.Persistence;
using Backend.Queues;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Backend.Tests;

public class SendRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DurableSimRecoversBeforeAndAfterQueueReceipt(bool receivedBeforeRestart)
    {
        DirectoryInfo storage = Directory.CreateTempSubdirectory("courseProj-recovery-");

        try
        {
            string outboxPath = Path.Combine(storage.FullName, "DurableSim-outbox.sqlite");
            string receiptPath = Path.Combine(storage.FullName, "DurableSim-receipts.sqlite");
            Guid messageId = Guid.NewGuid();
            string payload = "Buy 'ABC'";
            SqliteOutbox outbox = new SqliteOutbox(outboxPath);
            outbox.SavePending(messageId, payload);

            DateTimeOffset? firstReceiptTime = null;
            if (receivedBeforeRestart)
            {
                SqliteMessageQueue queueBeforeRestart = new SqliteMessageQueue(receiptPath);
                queueBeforeRestart.Send(messageId, payload);
                firstReceiptTime = Assert.IsType<DateTimeOffset>(queueBeforeRestart.GetReceivedAt(messageId));
            }

            string[] args = new string[]
            {
                "--Queue:Type=DurableSim",
                $"--Storage:Directory={storage.FullName}"
            };
            using (IHost host = Program.BuildHost(args))
            {
                Program.RunSendDemo(host.Services);
            }

            SqliteOutbox reopenedOutbox = new SqliteOutbox(outboxPath);
            SqliteMessageQueue reopenedQueue = new SqliteMessageQueue(receiptPath);
            DateTimeOffset receiptTime = Assert.IsType<DateTimeOffset>(reopenedQueue.GetReceivedAt(messageId));
            Assert.Equal(receiptTime, reopenedOutbox.GetSentAt(messageId));
            Assert.Empty(reopenedOutbox.GetUnconfirmedMessages());

            if (firstReceiptTime.HasValue)
            {
                Assert.Equal(firstReceiptTime.Value, receiptTime);
            }

            Assert.Equal(1L, CountRows(outboxPath, "SELECT COUNT(*) FROM Outbox;"));
            Assert.Equal(1L, CountRows(receiptPath, "SELECT COUNT(*) FROM ReceivedMessages;"));
        }
        finally
        {
            storage.Delete(recursive: true);
        }
    }

    [Fact]
    public void DurableSimAuditsRecoveredMessagesOnceInOutboxOrder()
    {
        DirectoryInfo storage = Directory.CreateTempSubdirectory("courseProj-recovery-audit-");
        try
        {
            SqliteOutbox outbox = new SqliteOutbox(Path.Combine(storage.FullName, "outbox.sqlite"));
            SqliteMessageQueue queue = new SqliteMessageQueue(Path.Combine(storage.FullName, "receipts.sqlite"));
            Guid firstId = Guid.NewGuid();
            Guid secondId = Guid.NewGuid();
            string firstMessage = "Buy";
            string secondMessage = "Cancel ABC";
            outbox.SavePending(firstId, firstMessage);
            outbox.SavePending(secondId, secondMessage);
            queue.Send(secondId, secondMessage);
            DateTimeOffset originalReceipt = Assert.IsType<DateTimeOffset>(queue.GetReceivedAt(secondId));

            ExchangeSession session = new ExchangeSession();
            Assert.True(session.TryTransitionTo(SessionState.Connecting));
            Assert.True(session.TryTransitionTo(SessionState.LoggingIn));
            Assert.True(session.TryTransitionTo(SessionState.Active));
            InMemoryAuditLog audit = new InMemoryAuditLog();
            ExchangeMessageSender sender = new ExchangeMessageSender(session, queue, audit, outbox);

            Assert.Equal(2, sender.RecoverUnconfirmedSends());
            Assert.Equal(0, sender.RecoverUnconfirmedSends());

            DateTimeOffset firstReceipt = Assert.IsType<DateTimeOffset>(queue.GetReceivedAt(firstId));
            Assert.Collection(audit.Entries,
                entry =>
                {
                    Assert.Equal(firstReceipt, entry.TimestampUtc);
                    Assert.Equal(firstMessage.Length, entry.MessageLength);
                    Assert.Equal(1L, entry.SequenceNumber);
                },
                entry =>
                {
                    Assert.Equal(originalReceipt, entry.TimestampUtc);
                    Assert.Equal(secondMessage.Length, entry.MessageLength);
                    Assert.Equal(2L, entry.SequenceNumber);
                });
        }
        finally
        {
            storage.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("Sim", nameof(InMemoryMessageQueue))]
    [InlineData("Datq", nameof(DatqMessageQueue))]
    public void OtherQueueModesLeaveUnconfirmedSendsForManualReview(string queueType, string queueClass)
    {
        DirectoryInfo storage = Directory.CreateTempSubdirectory("courseProj-manual-recovery-");

        try
        {
            string outboxPath = Path.Combine(storage.FullName, $"{queueClass}.sqlite");
            Guid messageId = Guid.NewGuid();
            SqliteOutbox outbox = new SqliteOutbox(outboxPath);
            outbox.SavePending(messageId, "Prior send");

            string[] args = new string[]
            {
                $"--Queue:Type={queueType}",
                $"--Storage:Directory={storage.FullName}",
                "--Queue:Datq:Server=localhost",
                "--Queue:Datq:Password=demo-password"
            };
            using (IHost host = Program.BuildHost(args))
            {
                Program.RunSendDemo(host.Services);

                if (host.Services.GetRequiredService<IMessageQueue>() is InMemoryMessageQueue queue)
                {
                    Assert.Empty(queue.Messages);
                }
            }

            Assert.Equal(messageId, Assert.Single(outbox.GetUnconfirmedMessageIds()));
            Assert.Equal(1L, CountRows(outboxPath, "SELECT COUNT(*) FROM Outbox;"));
        }
        finally
        {
            storage.Delete(recursive: true);
        }
    }

    private static long CountRows(string databasePath, string query)
    {
        SqliteConnectionStringBuilder settings = new SqliteConnectionStringBuilder();
        settings.DataSource = databasePath;
        settings.Pooling = false;

        using SqliteConnection connection = new SqliteConnection(settings.ConnectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;
        return Assert.IsType<long>(command.ExecuteScalar());
    }
}
