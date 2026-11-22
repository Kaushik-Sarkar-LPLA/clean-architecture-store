using CleanArchitecture.Application.Common.Exceptions;
using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Application.Reviews.Commands.CreateReview;

public record CreateReviewCommand : IRequest<int>
{
    public int ProductId { get; init; }
    public int Rating { get; init; }
    public string ReviewerName { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string Comment { get; init; } = string.Empty;
}

public class CreateReviewCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateReviewCommand, int>
{
    private const int MaxConcurrencyRetries = 3;

    public async Task<int> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            var product = await context.Products
                .Include(p => p.Reviews)
                .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

            if (product is null)
            {
                throw new NotFoundException(nameof(Product), request.ProductId.ToString());
            }

            var entity = new Review
            {
                ProductId = request.ProductId,
                Rating = request.Rating,
                ReviewerName = request.ReviewerName,
                Title = request.Title,
                Comment = request.Comment,
                IsVerifiedPurchase = true
            };

            entity.AddDomainEvent(new ProductReviewCreatedEvent(entity));

            // Recalculate AverageRating and TotalReviewsCount including the new review
            var existingSum = product.Reviews.Sum(r => r.Rating);
            var newCount = product.Reviews.Count + 1;

            product.TotalReviewsCount = newCount;
            product.AverageRating = Math.Round((decimal)(existingSum + request.Rating) / newCount, 2);

            context.Reviews.Add(entity);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return entity.Id;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                if (context is DbContext dbContext)
                {
                    dbContext.ChangeTracker.Clear();
                }
            }
        }

        throw new DbUpdateConcurrencyException(
            $"Failed to save review for Product {request.ProductId} after {MaxConcurrencyRetries} attempts due to concurrent updates.");
    }
}
