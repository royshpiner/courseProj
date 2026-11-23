namespace Backend.Audit
{
    public interface IAuditLog
    {
        void Record(AuditEntry entry);
    }
}