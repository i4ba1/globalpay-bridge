namespace GlobalPay.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    string? UserId { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool HasRole(string role);
}
