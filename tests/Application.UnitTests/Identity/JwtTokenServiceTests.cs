using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Application.Users.Commands.TokenLogin;
using CleanArchitecture.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CleanArchitecture.Application.UnitTests.Identity;

[TestFixture]
public class JwtTokenServiceTests
{
    private const string TestSecret = "SuperSecretKeyForCleanStoreApp2026Minimum32Chars!";
    private const string TestIssuer = "https://store.pixilens.online";
    private const string TestAudience = "https://store.pixilens.online";

    private IConfiguration _configuration = null!;
    private JwtTokenService _jwtTokenService = null!;

    [SetUp]
    public void SetUp()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = TestSecret,
            ["JwtSettings:Issuer"] = TestIssuer,
            ["JwtSettings:Audience"] = TestAudience,
            ["JwtSettings:ExpiryMinutes"] = "60"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _jwtTokenService = new JwtTokenService(_configuration);
    }

    [Test]
    public async Task GenerateAccessTokenAsync_ValidUserWithAdminRole_ReturnsValidJwtWithExpectedClaims()
    {
        // Arrange
        var userId = "user-123";
        var email = "admin@pixilens.online";
        var roles = new List<string> { "Administrator" };

        // Act
        var tokenResult = await _jwtTokenService.GenerateAccessTokenAsync(userId, email, roles);

        // Assert
        tokenResult.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokenResult.ExpiresIn.ShouldBe(3600);

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = TestIssuer,
            ValidateAudience = true,
            ValidAudience = TestAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = handler.ValidateToken(tokenResult.AccessToken, validationParameters, out var validatedToken);
        var jwtToken = validatedToken.ShouldBeOfType<JwtSecurityToken>();

        jwtToken.Header.Alg.ShouldBe(SecurityAlgorithms.HmacSha256);
        principal.FindFirstValue(ClaimTypes.NameIdentifier).ShouldBe(userId);
        principal.FindFirstValue(ClaimTypes.Email).ShouldBe(email);
        principal.FindFirstValue(JwtRegisteredClaimNames.Jti).ShouldNotBeNullOrWhiteSpace();
        principal.IsInRole("Administrator").ShouldBeTrue();

        var expectedExpiry = DateTime.UtcNow.AddMinutes(60);
        jwtToken.ValidTo.ShouldBe(expectedExpiry, TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task TokenLoginCommandHandler_InvalidPassword_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var identityServiceMock = new Mock<IIdentityService>();
        var jwtServiceMock = new Mock<IJwtTokenService>();
        var contextMock = new Mock<IApplicationDbContext>();

        identityServiceMock
            .Setup(x => x.AuthenticateAsync("admin@pixilens.online", "WrongPassword!"))
            .ReturnsAsync((false, string.Empty, string.Empty, Array.Empty<string>()));

        var handler = new TokenLoginCommandHandler(
            identityServiceMock.Object,
            jwtServiceMock.Object,
            contextMock.Object);

        var command = new TokenLoginCommand
        {
            Email = "admin@pixilens.online",
            Password = "WrongPassword!"
        };

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => handler.Handle(command, CancellationToken.None));

        jwtServiceMock.Verify(
            x => x.GenerateAccessTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>()),
            Times.Never);
        contextMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task AuthenticateAsync_LockedOutAccount_EnablesLockoutOnFailureAndReturnsFailedResult()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-locked-1",
            UserName = "locked@pixilens.online",
            Email = "locked@pixilens.online"
        };

        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        userManagerMock
            .Setup(x => x.FindByEmailAsync(user.Email))
            .ReturnsAsync(user);

        var contextAccessorMock = new Mock<IHttpContextAccessor>();
        var claimsFactoryMock = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var optionsMock = new Mock<IOptions<IdentityOptions>>();
        var loggerMock = new Mock<ILogger<SignInManager<ApplicationUser>>>();
        var schemesMock = new Mock<IAuthenticationSchemeProvider>();
        var confirmationMock = new Mock<IUserConfirmation<ApplicationUser>>();

        var signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            userManagerMock.Object,
            contextAccessorMock.Object,
            claimsFactoryMock.Object,
            optionsMock.Object,
            loggerMock.Object,
            schemesMock.Object,
            confirmationMock.Object);

        signInManagerMock
            .Setup(x => x.CheckPasswordSignInAsync(user, "AnyPassword!", true))
            .ReturnsAsync(SignInResult.LockedOut);

        var authorizationServiceMock = new Mock<IAuthorizationService>();
        var identityService = new IdentityService(
            userManagerMock.Object,
            signInManagerMock.Object,
            claimsFactoryMock.Object,
            authorizationServiceMock.Object);

        // Act
        var result = await identityService.AuthenticateAsync(user.Email, "AnyPassword!");

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.UserId.ShouldBeEmpty();
        result.Email.ShouldBeEmpty();
        result.Roles.ShouldBeEmpty();

        signInManagerMock.Verify(
            x => x.CheckPasswordSignInAsync(user, "AnyPassword!", true),
            Times.Once);
        userManagerMock.Verify(
            x => x.GetRolesAsync(It.IsAny<ApplicationUser>()),
            Times.Never);
    }
}
