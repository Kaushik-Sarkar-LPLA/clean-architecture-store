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
        var jwtCreatorId = await TestApp.RunAsUserAsync("jwt-creator@pixilens.online", "Creator1234!", [Roles.Customer]);
        var jwtModifierId = await TestApp.RunAsUserAsync("jwt-modifier@pixilens.online", "Modifier1234!", [Roles.Customer]);
        var ambientMockUserId = await TestApp.RunAsUserAsync("ambient-user@pixilens.online", "Ambient1234!", [Roles.Customer]);
        TestApp.GetUserId().ShouldBe(ambientMockUserId);

        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var creatorToken = await jwtTokenService.GenerateAccessTokenAsync(
            jwtCreatorId,
            "jwt-creator@pixilens.online",
            [Roles.Customer]);

        using var client = FunctionalTestSetup.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", creatorToken.AccessToken);

        var createResponse = await client.PostAsJsonAsync(
            "/api/TodoLists",
            new CreateTodoListCommand { Title = "Audited Shopping List" });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var listId = await createResponse.Content.ReadFromJsonAsync<int>();
        var createdList = await TestApp.FindAsync<TodoList>(listId);

        createdList.ShouldNotBeNull();
        createdList.CreatedBy.ShouldBe(jwtCreatorId);
        createdList.CreatedBy.ShouldNotBe(ambientMockUserId);
        createdList.LastModifiedBy.ShouldBe(jwtCreatorId);

        var modifierToken = await jwtTokenService.GenerateAccessTokenAsync(
            jwtModifierId,
            "jwt-modifier@pixilens.online",
            [Roles.Customer]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", modifierToken.AccessToken);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/TodoLists/{listId}",
            new UpdateTodoListCommand { Id = listId, Title = "Updated Audited List" });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var updatedList = await TestApp.FindAsync<TodoList>(listId);

        updatedList.ShouldNotBeNull();
        updatedList.CreatedBy.ShouldBe(jwtCreatorId);
        updatedList.LastModifiedBy.ShouldBe(jwtModifierId);
        updatedList.LastModifiedBy.ShouldNotBe(ambientMockUserId);
    }
}
