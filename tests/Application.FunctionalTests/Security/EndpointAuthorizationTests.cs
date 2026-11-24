using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Domain.Constants;
using CleanArchitecture.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace CleanArchitecture.Application.FunctionalTests.Security;

public class EndpointAuthorizationTests : TestBase
{
    [Test]
    public async Task Request_WithoutToken_Returns401Unauthorized()
    {
        // Arrange
        using var client = FunctionalTestSetup.CreateClient();

        // Act
        var response = await client.GetAsync("/api/TodoLists");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(401);
    }

    [Test]
    public async Task Request_WithValidCustomerToken_Returns200OK()
    {
        // Arrange
        var userId = await TestApp.RunAsUserAsync("customer@pixilens.online", "Customer1234!", [Roles.Customer]);
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var tokenResult = await jwtTokenService.GenerateAccessTokenAsync(
            userId,
            "customer@pixilens.online",
            [Roles.Customer]);

        using var client = FunctionalTestSetup.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

        // Act
        var response = await client.GetAsync("/api/TodoLists");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Request_CustomerCallingAdminEndpoint_Returns403Forbidden()
    {
        // Arrange
        var userId = await TestApp.RunAsUserAsync("customer2@pixilens.online", "Customer1234!", [Roles.Customer]);
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var tokenResult = await jwtTokenService.GenerateAccessTokenAsync(
            userId,
            "customer2@pixilens.online",
            [Roles.Customer]);

        using var client = FunctionalTestSetup.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

        // Act
        var response = await client.DeleteAsync("/api/Products/1");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Request_AdministratorCallingDeleteProduct_DeletesProductAndReturns204NoContent()
    {
        // Arrange
        var product = new Product
        {
            Name = "Mechanical Keyboard",
            Sku = "KB-MECH-001",
            Price = 129.99m,
            StockQuantity = 10,
            IsActive = true
        };
        await TestApp.AddAsync(product);

        var adminId = await TestApp.RunAsAdministratorAsync();
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var tokenResult = await jwtTokenService.GenerateAccessTokenAsync(
            adminId,
            "administrator@local",
            [Roles.Administrator]);

        using var client = FunctionalTestSetup.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

        // Act
        var response = await client.DeleteAsync($"/api/Products/{product.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var deleted = await TestApp.FindAsync<Product>(product.Id);
        deleted.ShouldBeNull();
    }

    [Test]
    public async Task Request_WithIdentityCookie_AuthenticatesCoexistingSession()
    {
        // Arrange
        await TestApp.RunAsUserAsync("cookieuser@pixilens.online", "CookieUser1234!", [Roles.Customer]);

        using var client = FunctionalTestSetup.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/Users/login?useCookies=true",
            new { email = "cookieuser@pixilens.online", password = "CookieUser1234!" });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        var response = await client.GetAsync("/api/TodoLists");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Request_WithExpiredToken_Returns401UnauthorizedWithExpiredDescription()
    {
        // Arrange
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("SuperSecretTestKeyForFunctionalTests2026Minimum32Chars!"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiredJwt = new JwtSecurityToken(
            issuer: "https://store.pixilens.online",
            audience: "https://store.pixilens.online",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, "expired-user-1"),
                new Claim(ClaimTypes.Email, "expired@pixilens.online"),
                new Claim(ClaimTypes.Role, Roles.Customer)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: DateTime.UtcNow.AddMinutes(-1),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(expiredJwt);

        using var client = FunctionalTestSetup.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);

        // Act
        var response = await client.GetAsync("/api/TodoLists");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("expired", Case.Insensitive);
    }
}
