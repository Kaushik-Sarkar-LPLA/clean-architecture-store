using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Application.Users.Commands.TokenLogin;
using CleanArchitecture.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
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
    private Mock<UserManager<ApplicationUser>> _userManagerMock = null!;
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

        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _jwtTokenService = new JwtTokenService(_configuration, _userManagerMock.Object);
    }

    [Test]
    public async Task GenerateAccessTokenAsync_ValidUserWithAdminRole_ReturnsValidJwtWithExpectedClaims()
    {
        // Arrange
        var userId = "user-123";
        var email = "admin@pixilens.online";
        var roles = new List<string> { "Administrator" };

        // Act
        var tokenString = await _jwtTokenService.GenerateAccessTokenAsync(userId, email, roles);

        // Assert
        tokenString.ShouldNotBeNullOrWhiteSpace();

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

        var principal = handler.ValidateToken(tokenString, validationParameters, out var validatedToken);
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

        identityServiceMock
            .Setup(x => x.AuthenticateAsync("admin@pixilens.online", "WrongPassword!"))
            .ReturnsAsync((false, string.Empty, string.Empty, Array.Empty<string>()));

        var handler = new TokenLoginCommandHandler(
            identityServiceMock.Object,
            jwtServiceMock.Object,
            _configuration);

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
    }
}
