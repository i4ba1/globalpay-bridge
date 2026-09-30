using System.Security.Claims;
using GlobalPay.Application.Abstractions;

namespace GlobalPay.Api.Identity;

internal sealed class HttpCurrentUser : ICurrentUser
{
    private const string SubjectClaim = "sub";

    private const string PreferredUsernameClaim = "preferred_username";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    /// <summary>
    /// Human-readable actor used for maker-checker comparisons and audit records, preferring the
    /// username claim and falling back to the stable subject claim.
    /// </summary>
    public string? UserId =>
        Principal?.FindFirst(PreferredUsernameClaim)?.Value
        ?? Principal?.FindFirst(SubjectClaim)?.Value
        ?? Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray() ?? [];

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool HasRole(string role) => Principal?.IsInRole(role) ?? false;
}
