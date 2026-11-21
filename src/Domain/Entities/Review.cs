using CleanArchitecture.Domain.Common;

namespace CleanArchitecture.Domain.Entities;

public class Review : BaseAuditableEntity
{
    public int ProductId { get; set; }
    public int Rating { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Comment { get; set; } = string.Empty;
    public bool IsVerifiedPurchase { get; set; } = true;

    public Product Product { get; set; } = null!;
}
