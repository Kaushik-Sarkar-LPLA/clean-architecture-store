using System.Security.Claims;
using CleanArchitecture.Web.Services;
using Microsoft.AspNetCore.Http;

namespace CleanArchitecture.Application.FunctionalTests.Security;

public class CurrentUserTests
{
    [Test]
    public void CurrentUser_WithAuthenticatedClaims_ExtractsIdAndRoles()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Email, "customer@pixilens.online"),
            new Claim(ClaimTypes.Role, "Customer")
        };
        var identity = new ClaimsIdentity(claims, authenticationType: "Bearer");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var user = new CurrentUser(httpContextAccessor.Object);

        var id = user.Id;
        var email = user.Email;
        var roles = user.Roles.ToList();
        var isAuthenticated = user.IsAuthenticated;

        id.ShouldBe("test-user-id");
        email.ShouldBe("customer@pixilens.online");
        roles.ShouldContain("Customer");
        isAuthenticated.ShouldBeTrue();
    }

    [Test]
    public void CurrentUser_NoHttpContext_ReturnsNullGracefully()
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var user = new CurrentUser(httpContextAccessor.Object);

        var id = user.Id;
        var email = user.Email;
        var roles = user.Roles;
        var isAuthenticated = user.IsAuthenticated;

        id.ShouldBeNull();
        email.ShouldBeNull();
        roles.ShouldBeEmpty();
        isAuthenticated.ShouldBeFalse();
    }
}
