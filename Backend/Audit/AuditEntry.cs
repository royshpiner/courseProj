using System;

namespace Backend.Audit
{
    public sealed class AuditEntry
    {
        public DateTimeOffset TimestampUtc { get; }
        public int MessageLength { get; }
        public long SequenceNumber { get; }

        public AuditEntry(DateTimeOffset timestampUtc, int messageLength, long sequenceNumber)
        {
            TimestampUtc = timestampUtc;
            MessageLength = messageLength;
            SequenceNumber = sequenceNumber;
        }
    }
}