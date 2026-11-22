using CleanArchitecture.Application.Common.Interfaces;
using MediatR;

namespace CleanArchitecture.Application.Users.Commands.TokenLogin;

public record TokenLoginCommand : IRequest<TokenResponseDto>
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public class TokenLoginCommandHandler(
    IIdentityService identityService,
    IJwtTokenService jwtTokenService)
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

        var tokenResult = await jwtTokenService.GenerateAccessTokenAsync(userId, email, roles);

        return new TokenResponseDto
        {
            AccessToken = tokenResult.AccessToken,
            TokenType = "Bearer",
            ExpiresIn = tokenResult.ExpiresIn,
            RefreshToken = string.Empty
        };
    }
}
