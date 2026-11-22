using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CleanArchitecture.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CleanArchitecture.Infrastructure.Identity;

public class JwtTokenService(
    IConfiguration configuration,
    UserManager<ApplicationUser> userManager) : IJwtTokenService
{
    public async Task<string> GenerateAccessTokenAsync(string userId, string email, IEnumerable<string> roles)
    {
        var roleList = roles.ToList();

        if (roleList.Count == 0)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is not null)
            {
                roleList = (await userManager.GetRolesAsync(user)).ToList();
            }
        }

        var secret = configuration["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("JwtSettings:Secret is not configured.");
        var issuer = configuration["JwtSettings:Issuer"]
            ?? throw new InvalidOperationException("JwtSettings:Issuer is not configured.");
        var audience = configuration["JwtSettings:Audience"]
            ?? throw new InvalidOperationException("JwtSettings:Audience is not configured.");
        var expiryMinutes = configuration.GetValue<int>("JwtSettings:ExpiryMinutes", 60);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roleList)
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

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
