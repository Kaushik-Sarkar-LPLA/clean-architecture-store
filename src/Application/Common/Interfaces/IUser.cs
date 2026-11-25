namespace CleanArchitecture.Application.Common.Interfaces;

public interface IUser
{
    string? Id { get; }

    string? Email { get; }

    IEnumerable<string> Roles { get; }

    bool IsAuthenticated { get; }
}
