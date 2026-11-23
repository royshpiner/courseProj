using System;
using System.Collections.Generic;

namespace Backend.Audit
{
    public sealed class InMemoryAuditLog : IAuditLog
    {
        private readonly List<AuditEntry> _entries = new List<AuditEntry>();

        public IReadOnlyList<AuditEntry> Entries
        {
            get
            {
                return _entries.AsReadOnly();
            }
        }

        public void Record(AuditEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            _entries.Add(entry);
        }
    }
}