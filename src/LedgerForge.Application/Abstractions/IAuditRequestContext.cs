namespace LedgerForge.Application.Abstractions;

public interface IAuditRequestContext
{
    string Actor { get; }
    string CorrelationId { get; }
    string? RequestMethod { get; }
    string? RequestPath { get; }
    string? RemoteAddress { get; }
    string? UserAgent { get; }
}
