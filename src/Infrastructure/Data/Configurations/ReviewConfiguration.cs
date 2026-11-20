using CleanArchitecture.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchitecture.Infrastructure.Data.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.Property(r => r.ReviewerName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Title)
            .HasMaxLength(200);

        builder.Property(r => r.Comment)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(r => r.IsVerifiedPurchase)
            .HasDefaultValue(true);

        builder.HasOne(r => r.Product)
            .WithMany(p => p.Reviews)
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
