namespace CleanArchitecture.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime ExpiryDateUtc { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByToken { get; set; }

    public bool IsActive => !IsRevoked && DateTime.UtcNow < ExpiryDateUtc;
}
