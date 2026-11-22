using FluentValidation;

namespace CleanArchitecture.Application.Users.Commands.TokenLogin;

public class TokenLoginCommandValidator : AbstractValidator<TokenLoginCommand>
{
    public TokenLoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
