using CleanArchitecture.Domain.Common;
using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Domain.Events;

public class ProductReviewCreatedEvent(Review review) : BaseEvent
{
    public Review Review { get; } = review;
}
