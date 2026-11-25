using System.Security.Claims;
using CleanArchitecture.Application.Common.Interfaces;

namespace CleanArchitecture.Web.Services;

public class CurrentUser : IUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? Id => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public IEnumerable<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(x => x.Value) ?? [];

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
