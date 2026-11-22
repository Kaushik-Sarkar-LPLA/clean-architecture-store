namespace CleanArchitecture.Application.Users.Commands.TokenLogin;

public record TokenResponseDto
{
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresIn { get; init; }
    public string RefreshToken { get; init; } = string.Empty;
}
