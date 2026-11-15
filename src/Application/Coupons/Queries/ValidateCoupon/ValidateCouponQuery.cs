namespace CleanArchitecture.Application.Coupons.Queries.ValidateCoupon;

public record ValidateCouponQuery : IRequest<CouponValidationResult>
{
    public string Code { get; init; } = string.Empty;
    public decimal OrderSubtotal { get; init; }
}
