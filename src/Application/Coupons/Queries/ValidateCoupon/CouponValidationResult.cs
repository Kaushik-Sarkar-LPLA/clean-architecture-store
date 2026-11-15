namespace CleanArchitecture.Application.Coupons.Queries.ValidateCoupon;

public record CouponValidationResult
{
    public bool IsValid { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal FinalTotal { get; init; }
    public string? FailureReason { get; init; }

    public static CouponValidationResult Invalid(decimal orderSubtotal, string reason) =>
        new()
        {
            IsValid = false,
            DiscountAmount = 0m,
            FinalTotal = orderSubtotal,
            FailureReason = reason
        };

    public static CouponValidationResult Valid(decimal discountAmount, decimal finalTotal) =>
        new()
        {
            IsValid = true,
            DiscountAmount = discountAmount,
            FinalTotal = finalTotal,
            FailureReason = null
        };
}
