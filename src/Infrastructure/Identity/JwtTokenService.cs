using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CleanArchitecture.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CleanArchitecture.Infrastructure.Identity;

public class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public Task<JwtTokenResult> GenerateAccessTokenAsync(string userId, string email, IEnumerable<string> roles)
    {
        var secret = GetRequiredSetting("JwtSettings:Secret");
        var issuer = GetRequiredSetting("JwtSettings:Issuer");
        var audience = GetRequiredSetting("JwtSettings:Audience");
        var expiryMinutes = configuration.GetValue<int>("JwtSettings:ExpiryMinutes", 60);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return Task.FromResult(new JwtTokenResult(accessToken, expiryMinutes * 60));
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    public ClaimsPrincipal GetPrincipalFromExpiredToken(string accessToken)
    {
        var secret = GetRequiredSetting("JwtSettings:Secret");
        var issuer = GetRequiredSetting("JwtSettings:Issuer");
        var audience = GetRequiredSetting("JwtSettings:Audience");

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateLifetime = false
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(accessToken, tokenValidationParameters, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new UnauthorizedAccessException("Invalid token algorithm.");
            }

            return principal;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException("Invalid access token.");
        }
    }

    private string GetRequiredSetting(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key} is not configured.");
}
