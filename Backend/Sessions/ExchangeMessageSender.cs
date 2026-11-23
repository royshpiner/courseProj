using Backend.Audit;
using Backend.Persistence;
using Backend.Queues;

namespace Backend
{
    public sealed class ExchangeMessageSender
    {
        private readonly ExchangeSession _session;
        private readonly IMessageQueue _queue;
        private readonly IAuditLog _auditLog;
        private readonly SqliteOutbox? _outbox;
        private long _sequenceNumber;

        public ExchangeMessageSender(
            ExchangeSession session, IMessageQueue queue, IAuditLog auditLog, SqliteOutbox? outbox = null)
        {
            _session = session;
            _queue = queue;
            _auditLog = auditLog;
            _outbox = outbox;
        }

        public bool TrySend(string message)
        {
            if (_session.State != SessionState.Active)
            {
                return false;
            }

            ArgumentNullException.ThrowIfNull(message);

            Guid messageId = Guid.NewGuid();

            if (_outbox != null)
            {
                _outbox.SavePending(messageId, message);
            }

            SendAndAudit(messageId, message);
            return true;
        }

        internal int RecoverUnconfirmedSends()
        {
            if (_session.State != SessionState.Active)
            {
                throw new InvalidOperationException("The session must be active to recover unconfirmed sends.");
            }

            if (_outbox == null || _queue is not SqliteMessageQueue)
            {
                throw new InvalidOperationException("Automatic recovery requires an outbox and a durable simulation queue.");
            }

            IReadOnlyList<(Guid MessageId, string Payload)> unconfirmedMessages = _outbox.GetUnconfirmedMessages();
            foreach ((Guid messageId, string payload) in unconfirmedMessages)
            {
                SendAndAudit(messageId, payload);
            }

            return unconfirmedMessages.Count;
        }

        private void SendAndAudit(Guid messageId, string message)
        {
            DateTimeOffset sentAt = SendToQueue(messageId, message);
            if (_outbox != null && !_outbox.TryMarkSent(messageId, sentAt))
            {
                if (_outbox.GetSentAt(messageId) == null)
                {
                    throw new InvalidOperationException($"Could not confirm outbox message {messageId}.");
                }

                return;
            }

            _sequenceNumber = _sequenceNumber + 1;
            AuditEntry entry = new AuditEntry(sentAt, message.Length, _sequenceNumber);
            _auditLog.Record(entry);
        }

        private DateTimeOffset SendToQueue(Guid messageId, string message)
        {
            _queue.Send(messageId, message);

            if (_queue is SqliteMessageQueue sqliteQueue)
            {
                return sqliteQueue.GetReceivedAt(messageId)
                    ?? throw new InvalidOperationException($"Could not read the receipt for message {messageId}.");
            }

            return DateTimeOffset.UtcNow;
        }
    }
}
