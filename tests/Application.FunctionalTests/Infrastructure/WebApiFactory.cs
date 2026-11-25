using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CleanArchitecture.Application.FunctionalTests.Infrastructure;

public class WebApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseSetting("ConnectionStrings:CleanArchitectureDb", connectionString)
            .UseSetting("JwtSettings:Secret", "SuperSecretTestKeyForFunctionalTests2026Minimum32Chars!")
            .UseSetting("JwtSettings:Issuer", "https://store.pixilens.online")
            .UseSetting("JwtSettings:Audience", "https://store.pixilens.online");

        builder.ConfigureTestServices(services =>
        {
            services
                .RemoveAll<IUser>()
                .AddScoped<IUser>(provider =>
                {
                    var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
                    var realUser = new CurrentUser(httpContextAccessor);
                    if (realUser.IsAuthenticated)
                    {
                        return realUser;
                    }

                    var mock = new Mock<IUser>();
                    mock.SetupGet(x => x.Roles).Returns(TestApp.GetRoles() ?? []);
                    mock.SetupGet(x => x.Id).Returns(TestApp.GetUserId());
                    mock.SetupGet(x => x.IsAuthenticated).Returns(TestApp.GetUserId() is not null);
                    return mock.Object;
                });
        });
    }
}
