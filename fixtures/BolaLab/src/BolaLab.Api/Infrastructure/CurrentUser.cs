using System.Security.Claims;
namespace BolaLab.Api.Infrastructure;

public interface ICurrentUser
{
    string UserId { get; }
    int TenantId { get; }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string UserId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    public int TenantId => int.TryParse(accessor.HttpContext?.User.FindFirstValue("tenant_id"), out var t) ? t : 0;
}
