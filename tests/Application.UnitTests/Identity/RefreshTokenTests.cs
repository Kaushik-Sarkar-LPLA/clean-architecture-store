using System.Linq.Expressions;
using System.Security.Claims;
using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Application.Identity.Commands.RefreshToken;
using CleanArchitecture.Application.Users.Commands.TokenLogin;
using CleanArchitecture.Domain.Entities;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CleanArchitecture.Application.UnitTests.Identity;

[TestFixture]
public class RefreshTokenTests
{
    private Mock<IApplicationDbContext> _contextMock = null!;
    private Mock<IJwtTokenService> _jwtTokenServiceMock = null!;
    private Mock<IIdentityService> _identityServiceMock = null!;
    private RefreshTokenCommandHandler _handler = null!;
    private RefreshTokenCommandValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();
        _identityServiceMock = new Mock<IIdentityService>();

        _handler = new RefreshTokenCommandHandler(
            _contextMock.Object,
            _jwtTokenServiceMock.Object,
            _identityServiceMock.Object);

        _validator = new RefreshTokenCommandValidator();
    }

    [Test]
    public async Task Handle_ValidActiveRefreshToken_ShouldRotateAndRevokeOldToken()
    {
        const string userId = "user-123";
        const string oldRefreshTokenValue = "old-refresh-token-abc";
        const string newRefreshTokenValue = "new-refresh-token-xyz";

        var existingToken = new RefreshToken
        {
            Id = 1,
            Token = oldRefreshTokenValue,
            UserId = userId,
            ExpiryDateUtc = DateTime.UtcNow.AddDays(5),
            IsRevoked = false
        };

        var refreshTokens = new List<RefreshToken> { existingToken };
        var tokensMock = CreateMockDbSet(refreshTokens);

        RefreshToken? addedReplacement = null;
        tokensMock.Setup(t => t.Add(It.IsAny<RefreshToken>()))
            .Callback<RefreshToken>(t => addedReplacement = t);

        _contextMock.Setup(c => c.RefreshTokens).Returns(tokensMock.Object);
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _jwtTokenServiceMock
            .Setup(s => s.GetPrincipalFromExpiredToken("expired-access-token"))
            .Returns(CreatePrincipal(userId));

        _identityServiceMock
            .Setup(s => s.GetUserDetailsAsync(userId))
            .ReturnsAsync((true, "priyanka@example.com", new List<string> { "Customer" }));

        _jwtTokenServiceMock
            .Setup(s => s.GenerateAccessTokenAsync(userId, "priyanka@example.com", It.IsAny<IList<string>>()))
            .ReturnsAsync(new JwtTokenResult("new-access-token", 900));

        _jwtTokenServiceMock
            .Setup(s => s.GenerateRefreshToken())
            .Returns(newRefreshTokenValue);

        var command = new RefreshTokenCommand
        {
            AccessToken = "expired-access-token",
            RefreshToken = oldRefreshTokenValue
        };

        var response = await _handler.Handle(command, CancellationToken.None);

        response.AccessToken.ShouldBe("new-access-token");
        response.RefreshToken.ShouldBe(newRefreshTokenValue);
        response.TokenType.ShouldBe("Bearer");
        response.ExpiresIn.ShouldBe(900);

        existingToken.IsRevoked.ShouldBeTrue();
        existingToken.RevokedAtUtc.ShouldNotBeNull();
        existingToken.ReplacedByToken.ShouldBe(newRefreshTokenValue);

        addedReplacement.ShouldNotBeNull();
        addedReplacement!.Token.ShouldBe(newRefreshTokenValue);
        addedReplacement.UserId.ShouldBe(userId);
        addedReplacement.IsRevoked.ShouldBeFalse();
        addedReplacement.ExpiryDateUtc.ShouldBeGreaterThan(DateTime.UtcNow.AddDays(6));

        tokensMock.Verify(t => t.Add(It.IsAny<RefreshToken>()), Times.Once);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_AlreadyRevokedTokenReuse_ShouldRevokeAllActiveTokensForUserAndThrow()
    {
        const string userId = "user-123";

        var compromisedRevokedToken = new RefreshToken
        {
            Id = 1,
            Token = "stolen-revoked-token",
            UserId = userId,
            ExpiryDateUtc = DateTime.UtcNow.AddDays(4),
            IsRevoked = true,
            RevokedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            ReplacedByToken = "legit-active-token"
        };

        var legitActiveToken = new RefreshToken
        {
            Id = 2,
            Token = "legit-active-token",
            UserId = userId,
            ExpiryDateUtc = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        var otherUserToken = new RefreshToken
        {
            Id = 3,
            Token = "other-user-token",
            UserId = "other-user-999",
            ExpiryDateUtc = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        var tokensMock = CreateMockDbSet(new List<RefreshToken>
        {
            compromisedRevokedToken,
            legitActiveToken,
            otherUserToken
        });

        _contextMock.Setup(c => c.RefreshTokens).Returns(tokensMock.Object);
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _jwtTokenServiceMock
            .Setup(s => s.GetPrincipalFromExpiredToken("expired-access-token"))
            .Returns(CreatePrincipal(userId));

        var command = new RefreshTokenCommand
        {
            AccessToken = "expired-access-token",
            RefreshToken = "stolen-revoked-token"
        };

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _handler.Handle(command, CancellationToken.None));

        legitActiveToken.IsRevoked.ShouldBeTrue();
        legitActiveToken.RevokedAtUtc.ShouldNotBeNull();
        otherUserToken.IsRevoked.ShouldBeFalse();

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_ExpiredRefreshToken_ShouldThrowUnauthorizedAccessException()
    {
        const string userId = "user-123";

        var expiredToken = new RefreshToken
        {
            Id = 1,
            Token = "expired-refresh-token",
            UserId = userId,
            ExpiryDateUtc = DateTime.UtcNow.AddMinutes(-1),
            IsRevoked = false
        };

        var tokensMock = CreateMockDbSet(new List<RefreshToken> { expiredToken });
        _contextMock.Setup(c => c.RefreshTokens).Returns(tokensMock.Object);

        _jwtTokenServiceMock
            .Setup(s => s.GetPrincipalFromExpiredToken("expired-access-token"))
            .Returns(CreatePrincipal(userId));

        var command = new RefreshTokenCommand
        {
            AccessToken = "expired-access-token",
            RefreshToken = "expired-refresh-token"
        };

        var ex = await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _handler.Handle(command, CancellationToken.None));

        ex.Message.ShouldBe("Refresh token expired");
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_TokenBelongingToDifferentUser_ShouldThrowUnauthorizedAccessException()
    {
        var tokenForUserA = new RefreshToken
        {
            Id = 1,
            Token = "user-a-refresh-token",
            UserId = "user-A",
            ExpiryDateUtc = DateTime.UtcNow.AddDays(5),
            IsRevoked = false
        };

        var tokensMock = CreateMockDbSet(new List<RefreshToken> { tokenForUserA });
        _contextMock.Setup(c => c.RefreshTokens).Returns(tokensMock.Object);

        _jwtTokenServiceMock
            .Setup(s => s.GetPrincipalFromExpiredToken("user-b-access-token"))
            .Returns(CreatePrincipal("user-B"));

        var command = new RefreshTokenCommand
        {
            AccessToken = "user-b-access-token",
            RefreshToken = "user-a-refresh-token"
        };

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _handler.Handle(command, CancellationToken.None));

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UnknownRefreshToken_ShouldThrowUnauthorizedAccessException()
    {
        var tokensMock = CreateMockDbSet(new List<RefreshToken>());
        _contextMock.Setup(c => c.RefreshTokens).Returns(tokensMock.Object);

        _jwtTokenServiceMock
            .Setup(s => s.GetPrincipalFromExpiredToken("expired-access-token"))
            .Returns(CreatePrincipal("user-123"));

        var command = new RefreshTokenCommand
        {
            AccessToken = "expired-access-token",
            RefreshToken = "non-existent-token"
        };

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _handler.Handle(command, CancellationToken.None));

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void Validator_EmptyTokens_ShouldHaveValidationErrors()
    {
        var command = new RefreshTokenCommand
        {
            AccessToken = "",
            RefreshToken = ""
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.AccessToken);
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }

    private static ClaimsPrincipal CreatePrincipal(string userId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        }, "TestAuth");

        return new ClaimsPrincipal(identity);
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
