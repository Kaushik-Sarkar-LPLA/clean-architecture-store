using CleanArchitecture.Domain.Common;
using CleanArchitecture.Domain.Enums;

namespace CleanArchitecture.Domain.Entities;

public class Coupon : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public DiscountType Type { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal MinOrderSubtotal { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public DateTime ExpiryDateUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public int UsageCount { get; set; }
    public int? MaxUsageLimit { get; set; }
}
