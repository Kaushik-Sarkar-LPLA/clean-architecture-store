using System.Security.Claims;
using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Application.Users.Commands.TokenLogin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RefreshTokenEntity = CleanArchitecture.Domain.Entities.RefreshToken;

namespace CleanArchitecture.Application.Identity.Commands.RefreshToken;

public record RefreshTokenCommand : IRequest<TokenResponseDto>
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
}

public class RefreshTokenCommandHandler(
    IApplicationDbContext context,
    IJwtTokenService jwtTokenService,
    IIdentityService identityService)
    : IRequestHandler<RefreshTokenCommand, TokenResponseDto>
{
    public async Task<TokenResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var principal = jwtTokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("Invalid access token.");
        }

        var storedToken = await context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, cancellationToken);

        if (storedToken is null || storedToken.UserId != userId)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        if (storedToken.IsRevoked)
        {
            var now = DateTime.UtcNow;
            var activeTokens = await context.RefreshTokens
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var activeToken in activeTokens)
            {
                activeToken.IsRevoked = true;
                activeToken.RevokedAtUtc = now;
            }

            await context.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Refresh token reuse detected. All sessions have been revoked.");
        }

        if (storedToken.ExpiryDateUtc < DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Refresh token expired");
        }

        var (found, email, roles) = await identityService.GetUserDetailsAsync(userId);
        if (!found)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        var newAccessToken = await jwtTokenService.GenerateAccessTokenAsync(userId, email, roles);
        var newRefreshTokenValue = jwtTokenService.GenerateRefreshToken();

        storedToken.IsRevoked = true;
        storedToken.RevokedAtUtc = DateTime.UtcNow;
        storedToken.ReplacedByToken = newRefreshTokenValue;

        var replacementToken = new RefreshTokenEntity
        {
            Token = newRefreshTokenValue,
            UserId = userId,
            ExpiryDateUtc = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        context.RefreshTokens.Add(replacementToken);
        await context.SaveChangesAsync(cancellationToken);

        return new TokenResponseDto
        {
            AccessToken = newAccessToken.AccessToken,
            TokenType = "Bearer",
            ExpiresIn = newAccessToken.ExpiresIn,
            RefreshToken = newRefreshTokenValue
        };
    }
}
