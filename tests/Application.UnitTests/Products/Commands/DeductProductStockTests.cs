using System.Linq.Expressions;
using Ardalis.GuardClauses;
using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Application.Products.Commands.DeductProductStock;
using CleanArchitecture.Domain.Entities;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CleanArchitecture.Application.UnitTests.Products.Commands;

[TestFixture]
public class DeductProductStockTests
{
    private Mock<IApplicationDbContext> _contextMock = null!;
    private DeductProductStockCommandHandler _handler = null!;
    private DeductProductStockCommandValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _handler = new DeductProductStockCommandHandler(_contextMock.Object);
        _validator = new DeductProductStockCommandValidator();
    }

    [Test]
    public async Task Handle_ValidRequest_ShouldDeductStockAndReturnSuccess()
    {
        var product = new Product
        {
            Id = 1,
            Sku = "SKU-001",
            Name = "Wireless Mouse",
            Price = 29.99m,
            StockQuantity = 10,
            IsActive = true
        };

        var mockSet = CreateMockDbSet(new List<Product> { product });
        _contextMock.Setup(c => c.Products).Returns(mockSet.Object);

        var command = new DeductProductStockCommand { ProductId = 1, QuantityToDeduct = 3 };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.RemainingStock.ShouldBe(7);
        result.ErrorMessage.ShouldBeNull();
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_InsufficientStock_ShouldReturnFailureWithoutDeducting()
    {
        var product = new Product
        {
            Id = 1,
            Sku = "SKU-001",
            Name = "Wireless Mouse",
            Price = 29.99m,
            StockQuantity = 2,
            IsActive = true
        };

        var mockSet = CreateMockDbSet(new List<Product> { product });
        _contextMock.Setup(c => c.Products).Returns(mockSet.Object);

        var command = new DeductProductStockCommand { ProductId = 1, QuantityToDeduct = 5 };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.RemainingStock.ShouldBe(2);
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Insufficient stock");
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_ProductNotFound_ShouldThrowNotFoundException()
    {
        var mockSet = CreateMockDbSet(new List<Product>());
        _contextMock.Setup(c => c.Products).Returns(mockSet.Object);

        var command = new DeductProductStockCommand { ProductId = 999, QuantityToDeduct = 1 };

        await Should.ThrowAsync<NotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_ConcurrencyConflict_ShouldReturnFailure()
    {
        var product = new Product
        {
            Id = 1,
            Sku = "SKU-001",
            Name = "Wireless Mouse",
            Price = 29.99m,
            StockQuantity = 10,
            IsActive = true
        };

        var mockSet = CreateMockDbSet(new List<Product> { product });
        _contextMock.Setup(c => c.Products).Returns(mockSet.Object);
        _contextMock
            .Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict"));

        var command = new DeductProductStockCommand { ProductId = 1, QuantityToDeduct = 3 };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Concurrent");
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Validator_InvalidQuantity_ShouldHaveValidationError(int invalidQuantity)
    {
        var command = new DeductProductStockCommand { ProductId = 1, QuantityToDeduct = invalidQuantity };
        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.QuantityToDeduct);
    }

    [TestCase(0)]
    [TestCase(-5)]
    public void Validator_InvalidProductId_ShouldHaveValidationError(int invalidProductId)
    {
        var command = new DeductProductStockCommand { ProductId = invalidProductId, QuantityToDeduct = 1 };
        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ProductId);
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
