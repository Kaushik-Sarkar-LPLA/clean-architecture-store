using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Domain.Enums;

namespace CleanArchitecture.Application.Coupons.Queries.ValidateCoupon;

public class ValidateCouponQueryHandler : IRequestHandler<ValidateCouponQuery, CouponValidationResult>
{
    private readonly IApplicationDbContext _context;

    public ValidateCouponQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CouponValidationResult> Handle(ValidateCouponQuery request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var coupon = await _context.Coupons
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == normalizedCode, cancellationToken);

        if (coupon is null)
        {
            return CouponValidationResult.Invalid(request.OrderSubtotal, "Coupon code does not exist");
        }

        if (!coupon.IsActive)
        {
            return CouponValidationResult.Invalid(request.OrderSubtotal, "Coupon is disabled");
        }

        if (coupon.ExpiryDateUtc <= DateTime.UtcNow)
        {
            return CouponValidationResult.Invalid(request.OrderSubtotal, "Coupon has expired");
        }

        if (request.OrderSubtotal < coupon.MinOrderSubtotal)
        {
            return CouponValidationResult.Invalid(
                request.OrderSubtotal,
                $"Requires minimum cart subtotal of ${coupon.MinOrderSubtotal}");
        }

        if (coupon.MaxUsageLimit.HasValue && coupon.UsageCount >= coupon.MaxUsageLimit.Value)
        {
            return CouponValidationResult.Invalid(request.OrderSubtotal, "Coupon usage limit exceeded");
        }

        decimal discountAmount = coupon.Type switch
        {
            DiscountType.Percentage => CalculatePercentageDiscount(
                request.OrderSubtotal,
                coupon.DiscountValue,
                coupon.MaxDiscountAmount),
            DiscountType.FixedAmount => Math.Min(coupon.DiscountValue, request.OrderSubtotal),
            _ => 0m
        };

        discountAmount = Math.Round(discountAmount, 2, MidpointRounding.AwayFromZero);
        var finalTotal = Math.Max(0m, request.OrderSubtotal - discountAmount);

        return CouponValidationResult.Valid(discountAmount, finalTotal);
    }

    private static decimal CalculatePercentageDiscount(
        decimal orderSubtotal,
        decimal discountValue,
        decimal? maxDiscountAmount)
    {
        var rawDiscount = orderSubtotal * (discountValue / 100m);
        return maxDiscountAmount.HasValue
            ? Math.Min(rawDiscount, maxDiscountAmount.Value)
            : rawDiscount;
    }
}
