using Backend.Queues;
using Backend.Audit;

namespace Backend
{
    public sealed class ExchangeMessageSender
    {
        private readonly ExchangeSession _session;
        private readonly IMessageQueue _queue;
        private readonly IAuditLog _auditLog;
        private long _sequenceNumber;

        public ExchangeMessageSender(ExchangeSession session, IMessageQueue queue, IAuditLog auditLog)
        {
            _session = session;
            _queue = queue;
            _auditLog = auditLog;
        }

        public bool TrySend(string message)
        {
            if (_session.State != SessionState.Active)
            {
                return false;
            }


            _queue.Send(message);

            _sequenceNumber = _sequenceNumber + 1;

            AuditEntry entry = new AuditEntry(DateTimeOffset.UtcNow, message.Length, _sequenceNumber);

            _auditLog.Record(entry);
            return true;
        }
    }
}