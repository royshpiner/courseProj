using Backend.Queues;
using Backend.Audit;
using Xunit;

namespace Backend.Tests
{
    public class ExchangeMessageSenderTests
    {
        [Fact]
        public void DisconnectedSessionDoesNotSendToQueue()
        {
            ExchangeSession session = new ExchangeSession();
            InMemoryMessageQueue queue = new InMemoryMessageQueue();
            InMemoryAuditLog auditLog = new InMemoryAuditLog();
            ExchangeMessageSender sender = new ExchangeMessageSender(session, queue, auditLog);

            bool sent = sender.TrySend("Test message");

            Assert.False(sent);
            Assert.Empty(queue.Messages);
            Assert.Empty(auditLog.Entries);
        }

        [Fact]
        public void ActiveSessionSendsAndAuditsMessages()
        {
            ExchangeSession session = new ExchangeSession();
            Assert.True(session.TryTransitionTo(SessionState.Connecting));
            Assert.True(session.TryTransitionTo(SessionState.LoggingIn));
            Assert.True(session.TryTransitionTo(SessionState.Active));

            InMemoryMessageQueue queue = new InMemoryMessageQueue();
            InMemoryAuditLog auditLog = new InMemoryAuditLog();
            ExchangeMessageSender sender = new ExchangeMessageSender(session, queue, auditLog);

            string firstMessage = "Buy";
            string secondMessage = "Cancel";
            DateTimeOffset beforeSending = DateTimeOffset.UtcNow;

            Assert.True(sender.TrySend(firstMessage));
            Assert.True(sender.TrySend(secondMessage));

            DateTimeOffset afterSending = DateTimeOffset.UtcNow;

            Assert.Equal(2, queue.Messages.Count);
            Assert.Equal(firstMessage, queue.Messages[0]);
            Assert.Equal(secondMessage, queue.Messages[1]);

            Assert.Equal(2, auditLog.Entries.Count);
            AuditEntry firstEntry = auditLog.Entries[0];
            AuditEntry secondEntry = auditLog.Entries[1];

            Assert.Equal(firstMessage.Length, firstEntry.MessageLength);
            Assert.Equal(secondMessage.Length, secondEntry.MessageLength);
            Assert.Equal(1L, firstEntry.SequenceNumber);
            Assert.Equal(2L, secondEntry.SequenceNumber);

            Assert.Equal(TimeSpan.Zero, firstEntry.TimestampUtc.Offset);
            Assert.Equal(TimeSpan.Zero, secondEntry.TimestampUtc.Offset);
            Assert.InRange(firstEntry.TimestampUtc, beforeSending, afterSending);
            Assert.InRange(secondEntry.TimestampUtc, beforeSending, afterSending);
        }

        [Fact]
        public void SuspendedSessionRejectsSend()
        {
            ExchangeSession session = new ExchangeSession();
            Assert.True(session.TryTransitionTo(SessionState.Connecting));
            Assert.True(session.TryTransitionTo(SessionState.LoggingIn));
            Assert.True(session.TryTransitionTo(SessionState.Active));
            Assert.True(session.TryTransitionTo(SessionState.Suspended));

            InMemoryMessageQueue queue = new InMemoryMessageQueue();
            InMemoryAuditLog auditLog = new InMemoryAuditLog();
            ExchangeMessageSender sender = new ExchangeMessageSender(session, queue, auditLog);

            bool sent = sender.TrySend("Test message");

            Assert.False(sent);
            Assert.Empty(queue.Messages);
            Assert.Empty(auditLog.Entries);

            Assert.True(session.TryTransitionTo(SessionState.Active));
            Assert.Empty(queue.Messages);
            Assert.Empty(auditLog.Entries);
        }
    }

}