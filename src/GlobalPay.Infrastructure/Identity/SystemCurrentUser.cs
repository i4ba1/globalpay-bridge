using GlobalPay.Application.Abstractions;

namespace GlobalPay.Infrastructure.Identity;

public sealed class SystemCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => true;

    public string? UserId => "system";

    public IReadOnlyCollection<string> Roles => [];

    public bool HasRole(string role) => false;
}
