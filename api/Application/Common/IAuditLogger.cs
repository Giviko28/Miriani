namespace Application.Common;

/// <summary>
/// Records notable actions (logins, uploads, AI requests) to the audit trail. Supports the
/// accountability the proposal calls for when AI touches business operations.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(Guid orgId, Guid? userId, string action, string? detail = null, CancellationToken ct = default);
}
