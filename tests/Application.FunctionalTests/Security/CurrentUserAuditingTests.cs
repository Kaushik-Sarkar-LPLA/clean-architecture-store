using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Application.TodoLists.Commands.CreateTodoList;
using CleanArchitecture.Application.TodoLists.Commands.UpdateTodoList;
using CleanArchitecture.Domain.Constants;
using CleanArchitecture.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Application.FunctionalTests.Security;

public class CurrentUserAuditingTests : TestBase
{
    [Test]
    public async Task CreateAndUpdateAuditableEntity_WithAuthenticatedJwt_PersistsCreatedByAndLastModifiedBy()
    {
        // Arrange
        var userId = await TestApp.RunAsUserAsync("auditor@pixilens.online", "Auditor1234!", [Roles.Customer]);
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var tokenResult = await jwtTokenService.GenerateAccessTokenAsync(
            userId,
            "auditor@pixilens.online",
            [Roles.Customer]);

        using var client = FunctionalTestSetup.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

        // Act 1: Create an auditable TodoList
        var createResponse = await client.PostAsJsonAsync(
            "/api/TodoLists",
            new CreateTodoListCommand { Title = "Audited Shopping List" });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var listId = await createResponse.Content.ReadFromJsonAsync<int>();
        var createdList = await TestApp.FindAsync<TodoList>(listId);

        // Assert 1: CreatedBy and LastModifiedBy equal the JWT user ID
        createdList.ShouldNotBeNull();
        createdList.CreatedBy.ShouldBe(userId);
        createdList.LastModifiedBy.ShouldBe(userId);

        // Act 2: Modify the auditable TodoList as a second authenticated user
        var modifierId = await TestApp.RunAsUserAsync("modifier@pixilens.online", "Modifier1234!", [Roles.Customer]);
        var modifierToken = await jwtTokenService.GenerateAccessTokenAsync(
            modifierId,
            "modifier@pixilens.online",
            [Roles.Customer]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", modifierToken.AccessToken);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/TodoLists/{listId}",
            new UpdateTodoListCommand { Id = listId, Title = "Updated Audited List" });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var updatedList = await TestApp.FindAsync<TodoList>(listId);

        // Assert 2: CreatedBy remains original creator; LastModifiedBy updates to modifierId
        updatedList.ShouldNotBeNull();
        updatedList.CreatedBy.ShouldBe(userId);
        updatedList.LastModifiedBy.ShouldBe(modifierId);
    }
}
