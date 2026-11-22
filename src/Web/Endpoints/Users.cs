using CleanArchitecture.Application.Users.Commands.TokenLogin;
using CleanArchitecture.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CleanArchitecture.Web.Endpoints;

public class Users : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapIdentityApi<ApplicationUser>();

        groupBuilder.MapPost("/token-login", TokenLogin)
            .WithName("TokenLogin")
            .AllowAnonymous();

        groupBuilder.MapPost(Logout, "logout").RequireAuthorization();
    }

    [EndpointSummary("Authenticate user and issue JWT access token")]
    [EndpointDescription("Validates user email and password credentials and returns a signed 60-minute JWT Bearer access token.")]
    public static async Task<Ok<TokenResponseDto>> TokenLogin(
        ISender sender,
        TokenLoginCommand command)
    {
        var result = await sender.Send(command);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Log out")]
    [EndpointDescription("Logs out the current user by clearing the authentication cookie.")]
    public static async Task<Results<Ok, UnauthorizedHttpResult>> Logout(SignInManager<ApplicationUser> signInManager, [FromBody] object empty)
    {
        if (empty != null)
        {
            await signInManager.SignOutAsync();
            return TypedResults.Ok();
        }

        return TypedResults.Unauthorized();
    }
}
