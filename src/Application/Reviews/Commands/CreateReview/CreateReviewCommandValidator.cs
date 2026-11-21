using FluentValidation;

namespace CleanArchitecture.Application.Reviews.Commands.CreateReview;

public class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator()
    {
        RuleFor(v => v.ProductId)
            .GreaterThan(0);

        RuleFor(v => v.Rating)
            .InclusiveBetween(1, 5);

        RuleFor(v => v.ReviewerName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(v => v.Comment)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(2000);
    }
}
