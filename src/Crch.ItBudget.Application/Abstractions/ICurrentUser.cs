namespace Crch.ItBudget.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string DomainIdentity { get; }
    string DisplayName { get; }
    IReadOnlyCollection<string> Roles { get; }
}
