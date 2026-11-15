namespace CleanArchitecture.Application.Coupons.Queries.ValidateCoupon;

public class ValidateCouponQueryValidator : AbstractValidator<ValidateCouponQuery>
{
    public ValidateCouponQueryValidator()
    {
        RuleFor(v => v.Code)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(v => v.OrderSubtotal)
            .GreaterThan(0);
    }
}
