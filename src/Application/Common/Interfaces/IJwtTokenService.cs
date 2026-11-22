namespace CleanArchitecture.Application.Common.Interfaces;

public interface IJwtTokenService
{
    Task<string> GenerateAccessTokenAsync(string userId, string email, IEnumerable<string> roles);
}
