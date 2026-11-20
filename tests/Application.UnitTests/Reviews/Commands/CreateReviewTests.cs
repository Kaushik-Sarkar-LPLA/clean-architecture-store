using System.Linq.Expressions;

using Ardalis.GuardClauses;
using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Application.Reviews.Commands.CreateReview;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Events;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CleanArchitecture.Application.UnitTests.Reviews.Commands;

[TestFixture]
public class CreateReviewTests
{
    private Mock<IApplicationDbContext> _contextMock = null!;
    private CreateReviewCommandHandler _handler = null!;
    private CreateReviewCommandValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _handler = new CreateReviewCommandHandler(_contextMock.Object);
        _validator = new CreateReviewCommandValidator();
    }

    [Test]
    public async Task Handle_ValidReview_ShouldCreateReviewAndCalculateCorrectAverage()
    {
        var existingReview1 = new Review { Id = 1, ProductId = 1, Rating = 5, ReviewerName = "Alice", Comment = "Great product overall!" };
        var existingReview2 = new Review { Id = 2, ProductId = 1, Rating = 3, ReviewerName = "Bob", Comment = "Decent quality for the price." };

        var product = new Product
        {
            Id = 1,
            Name = "Wireless Mechanical Keyboard",
            AverageRating = 4.00m,
            TotalReviewsCount = 2,
            Reviews = new List<Review> { existingReview1, existingReview2 }
        };

        var productsMock = CreateMockDbSet(new List<Product> { product });
        var reviewsMock = CreateMockDbSet(new List<Review> { existingReview1, existingReview2 });

        Review? capturedReview = null;
        reviewsMock.Setup(r => r.Add(It.IsAny<Review>()))
            .Callback<Review>(r =>
            {
                r.Id = 3;
                capturedReview = r;
            });

        _contextMock.Setup(c => c.Products).Returns(productsMock.Object);
        _contextMock.Setup(c => c.Reviews).Returns(reviewsMock.Object);
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new CreateReviewCommand
        {
            ProductId = 1,
            Rating = 4,
            ReviewerName = "Priyanka",
            Title = "Solid build",
            Comment = "Excellent build quality and fast shipping!"
        };

        var resultId = await _handler.Handle(command, CancellationToken.None);

        resultId.ShouldBe(3);
        product.AverageRating.ShouldBe(4.00m);
        product.TotalReviewsCount.ShouldBe(3);

        capturedReview.ShouldNotBeNull();
        capturedReview!.ProductId.ShouldBe(1);
        capturedReview.Rating.ShouldBe(4);
        capturedReview.ReviewerName.ShouldBe("Priyanka");
        capturedReview.Comment.ShouldBe("Excellent build quality and fast shipping!");
        capturedReview.IsVerifiedPurchase.ShouldBeTrue();
        capturedReview.DomainEvents.ShouldContain(e => e is ProductReviewCreatedEvent);

        reviewsMock.Verify(r => r.Add(It.IsAny<Review>()), Times.Once);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(6)]
    public void Validator_RatingOutOfRange_ShouldHaveValidationError(int invalidRating)
    {
        var command = new CreateReviewCommand
        {
            ProductId = 1,
            Rating = invalidRating,
            ReviewerName = "Priyanka",
            Comment = "Excellent build quality and fast shipping!"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Rating);
    }

    [TestCase("")]
    [TestCase("Too short")]
    public void Validator_CommentTooShort_ShouldHaveValidationError(string shortComment)
    {
        var command = new CreateReviewCommand
        {
            ProductId = 1,
            Rating = 5,
            ReviewerName = "Priyanka",
            Comment = shortComment
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Comment);
    }

    [Test]
    public async Task Handle_NonExistentProduct_ShouldThrowNotFoundException()
    {
        var emptyProductsMock = CreateMockDbSet(new List<Product>());
        _contextMock.Setup(c => c.Products).Returns(emptyProductsMock.Object);

        var command = new CreateReviewCommand
        {
            ProductId = 999,
            Rating = 5,
            ReviewerName = "Priyanka",
            Comment = "Excellent build quality and fast shipping!"
        };

        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<DbSet<T>> CreateMockDbSet<T>(List<T> data) where T : class
    {
        var queryable = data.AsQueryable();
        var mockSet = new Mock<DbSet<T>>();

        mockSet.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));

        mockSet.As<IQueryable<T>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<T>(queryable.Provider));

        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());

        return mockSet;
    }

    private sealed class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        internal TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

        public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<TEntity>(expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);

        public object? Execute(Expression expression) => _inner.Execute(expression);

        public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var resultType = typeof(TResult).GetGenericArguments()[0];
            var executionResult = typeof(IQueryProvider)
                .GetMethod(nameof(IQueryProvider.Execute), 1, new[] { typeof(Expression) })!
                .MakeGenericMethod(resultType)
                .Invoke(_inner, new object[] { expression });

            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, new[] { executionResult })!;
        }
    }

    private sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(Expression expression) : base(expression) { }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }

    private sealed class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;

        public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

        public T Current => _inner.Current;

        public ValueTask DisposeAsync()
        {
            _inner.Dispose();
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> MoveNextAsync() => new(_inner.MoveNext());
    }
}
