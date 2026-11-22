using System.Security.Claims;

namespace CleanArchitecture.Application.Common.Interfaces;

public record JwtTokenResult(string AccessToken, int ExpiresIn);

public interface IJwtTokenService
{
    Task<JwtTokenResult> GenerateAccessTokenAsync(string userId, string email, IEnumerable<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal GetPrincipalFromExpiredToken(string accessToken);
}
