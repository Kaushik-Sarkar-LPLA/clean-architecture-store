using CleanArchitecture.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace CleanArchitecture.Application.Users.Commands.TokenLogin;

public record TokenLoginCommand : IRequest<TokenResponseDto>
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public class TokenLoginCommandHandler(
    IIdentityService identityService,
    IJwtTokenService jwtTokenService,
    IConfiguration configuration)
    : IRequestHandler<TokenLoginCommand, TokenResponseDto>
{
    public async Task<TokenResponseDto> Handle(TokenLoginCommand request, CancellationToken cancellationToken)
    {
        var (succeeded, userId, email, roles) = await identityService.AuthenticateAsync(
            request.Email,
            request.Password);

        if (!succeeded)
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var accessToken = await jwtTokenService.GenerateAccessTokenAsync(userId, email, roles);
        var expiryMinutes = configuration.GetValue<int>("JwtSettings:ExpiryMinutes", 60);

        return new TokenResponseDto
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = expiryMinutes * 60,
            RefreshToken = string.Empty
        };
    }
}
