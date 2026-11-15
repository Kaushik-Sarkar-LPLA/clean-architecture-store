using System.Linq.Expressions;
using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Application.Coupons.Queries.ValidateCoupon;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Enums;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CleanArchitecture.Application.UnitTests.Coupons.Queries;

[TestFixture]
public class ValidateCouponTests
{
    private Mock<IApplicationDbContext> _contextMock = null!;
    private ValidateCouponQueryHandler _handler = null!;
    private ValidateCouponQueryValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _handler = new ValidateCouponQueryHandler(_contextMock.Object);
        _validator = new ValidateCouponQueryValidator();
    }

    [Test]
    public async Task Handle_ActivePercentageCoupon_CalculatesCorrectDiscountAndTotal()
    {
        var coupon = new Coupon
        {
            Id = 1,
            Code = "SUMMER20",
            Type = DiscountType.Percentage,
            DiscountValue = 20m,
            MinOrderSubtotal = 50m,
            ExpiryDateUtc = DateTime.UtcNow.AddDays(1),
            IsActive = true
        };

        SetupCoupons(coupon);

        var query = new ValidateCouponQuery { Code = "SUMMER20", OrderSubtotal = 100m };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsValid.ShouldBeTrue();
        result.DiscountAmount.ShouldBe(20.00m);
        result.FinalTotal.ShouldBe(80.00m);
        result.FailureReason.ShouldBeNull();
    }

    [Test]
    public async Task Handle_ExpiredCoupon_ReturnsInvalidWithExpiredReason()
    {
        var coupon = new Coupon
        {
            Id = 1,
            Code = "EXPIRED10",
            Type = DiscountType.Percentage,
            DiscountValue = 10m,
            MinOrderSubtotal = 0m,
            ExpiryDateUtc = DateTime.UtcNow.AddDays(-1),
            IsActive = true
        };

        SetupCoupons(coupon);

        var query = new ValidateCouponQuery { Code = "EXPIRED10", OrderSubtotal = 100m };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
        result.DiscountAmount.ShouldBe(0m);
        result.FinalTotal.ShouldBe(100m);
        result.FailureReason.ShouldBe("Coupon has expired");
    }

    [Test]
    public async Task Handle_SubtotalBelowMinimum_ReturnsInvalid()
    {
        var coupon = new Coupon
        {
            Id = 1,
            Code = "MIN100",
            Type = DiscountType.Percentage,
            DiscountValue = 15m,
            MinOrderSubtotal = 100m,
            ExpiryDateUtc = DateTime.UtcNow.AddDays(5),
            IsActive = true
        };

        SetupCoupons(coupon);

        var query = new ValidateCouponQuery { Code = "MIN100", OrderSubtotal = 75m };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
        result.DiscountAmount.ShouldBe(0m);
        result.FinalTotal.ShouldBe(75m);
        result.FailureReason.ShouldNotBeNull();
        result.FailureReason.ShouldContain("minimum cart subtotal");
    }

    [Test]
    public async Task Handle_PercentageDiscountExceedsCap_ClampsToMaxDiscount()
    {
        var coupon = new Coupon
        {
            Id = 1,
            Code = "HALF50",
            Type = DiscountType.Percentage,
            DiscountValue = 50m,
            MinOrderSubtotal = 50m,
            MaxDiscountAmount = 25m,
            ExpiryDateUtc = DateTime.UtcNow.AddDays(5),
            IsActive = true
        };

        SetupCoupons(coupon);

        var query = new ValidateCouponQuery { Code = "HALF50", OrderSubtotal = 200m };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsValid.ShouldBeTrue();
        result.DiscountAmount.ShouldBe(25.00m);
        result.FinalTotal.ShouldBe(175.00m);
        result.FailureReason.ShouldBeNull();
    }

    [Test]
    public async Task Handle_FixedAmountCoupon_CalculatesDiscountAndClampsToSubtotal()
    {
        var coupon = new Coupon
        {
            Id = 1,
            Code = "FLAT30",
            Type = DiscountType.FixedAmount,
            DiscountValue = 30m,
            MinOrderSubtotal = 10m,
            ExpiryDateUtc = DateTime.UtcNow.AddDays(5),
            IsActive = true
        };

        SetupCoupons(coupon);

        var query = new ValidateCouponQuery { Code = "FLAT30", OrderSubtotal = 20m };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsValid.ShouldBeTrue();
        result.DiscountAmount.ShouldBe(20.00m);
        result.FinalTotal.ShouldBe(0.00m);
    }

    [Test]
    public async Task Handle_CouponNotFound_ReturnsInvalid()
    {
        SetupCoupons();

        var query = new ValidateCouponQuery { Code = "DOESNOTEXIST", OrderSubtotal = 100m };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("Coupon code does not exist");
    }

    [Test]
    public async Task Handle_InactiveCoupon_ReturnsInvalid()
    {
        var coupon = new Coupon
        {
            Id = 1,
            Code = "DISABLED10",
            Type = DiscountType.Percentage,
            DiscountValue = 10m,
            MinOrderSubtotal = 0m,
            ExpiryDateUtc = DateTime.UtcNow.AddDays(5),
            IsActive = false
        };

        SetupCoupons(coupon);

        var query = new ValidateCouponQuery { Code = "DISABLED10", OrderSubtotal = 100m };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("Coupon is disabled");
    }

    [Test]
    public async Task Handle_UsageLimitReached_ReturnsInvalid()
    {
        var coupon = new Coupon
        {
            Id = 1,
            Code = "LIMITED5",
            Type = DiscountType.Percentage,
            DiscountValue = 10m,
            MinOrderSubtotal = 0m,
            ExpiryDateUtc = DateTime.UtcNow.AddDays(5),
            IsActive = true,
            UsageCount = 5,
            MaxUsageLimit = 5
        };

        SetupCoupons(coupon);

        var query = new ValidateCouponQuery { Code = "LIMITED5", OrderSubtotal = 100m };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("Coupon usage limit exceeded");
    }

    [Test]
    public async Task Handle_LowercaseCodeWithWhitespace_NormalizesAndValidates()
    {
        var coupon = new Coupon
        {
            Id = 1,
            Code = "SUMMER20",
            Type = DiscountType.Percentage,
            DiscountValue = 20m,
            MinOrderSubtotal = 50m,
            ExpiryDateUtc = DateTime.UtcNow.AddDays(1),
            IsActive = true
        };

        SetupCoupons(coupon);

        var query = new ValidateCouponQuery { Code = "  summer20  ", OrderSubtotal = 100m };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsValid.ShouldBeTrue();
        result.DiscountAmount.ShouldBe(20.00m);
        result.FinalTotal.ShouldBe(80.00m);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("THISCOUPONCODEISWAYTOOLONG21")]
    public void Validator_InvalidCode_ShouldHaveValidationError(string invalidCode)
    {
        var query = new ValidateCouponQuery { Code = invalidCode, OrderSubtotal = 50m };
        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [TestCase(0)]
    [TestCase(-10)]
    public void Validator_InvalidOrderSubtotal_ShouldHaveValidationError(decimal invalidSubtotal)
    {
        var query = new ValidateCouponQuery { Code = "SAVE20", OrderSubtotal = invalidSubtotal };
        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.OrderSubtotal);
    }

    private void SetupCoupons(params Coupon[] coupons)
    {
        var mockSet = CreateMockDbSet(coupons.ToList());
        _contextMock.Setup(c => c.Coupons).Returns(mockSet.Object);
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
