using LedgerForge.Application.Abstractions;

namespace LedgerForge.Web.Auditing;

public sealed class HttpAuditRequestContext(IHttpContextAccessor httpContextAccessor) : IAuditRequestContext
{
    private HttpContext? Context => httpContextAccessor.HttpContext;

    public string Actor => Context?.User?.Identity?.Name ?? "system";
    public string CorrelationId => Context?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
    public string? RequestMethod => Context?.Request.Method;
    public string? RequestPath => Context?.Request.Path.Value;
    public string? RemoteAddress => Context?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString();
}
