using System.Text;
using System.Text.Json;
using Azure.Identity;
using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Infrastructure.Data;
using CleanArchitecture.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddWebServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddScoped<IUser, CurrentUser>();

        builder.Services.AddHttpContextAccessor();

        builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

        // Customise default API behaviour
        builder.Services.Configure<ApiBehaviorOptions>(options =>
            options.SuppressModelStateInvalidFilter = true);

        var isOpenApiBuild = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

        var jwtSecret = builder.Configuration["JwtSettings:Secret"]
            ?? (isOpenApiBuild
                ? "OpenApiBuildTimePlaceholderSecretKeyMinimum32Chars!"
                : throw new InvalidOperationException("JwtSettings:Secret is not configured."));
        var jwtIssuer = builder.Configuration["JwtSettings:Issuer"]
            ?? (isOpenApiBuild
                ? "https://store.pixilens.online"
                : throw new InvalidOperationException("JwtSettings:Issuer is not configured."));
        var jwtAudience = builder.Configuration["JwtSettings:Audience"]
            ?? (isOpenApiBuild
                ? "https://store.pixilens.online"
                : throw new InvalidOperationException("JwtSettings:Audience is not configured."));

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ClockSkew = TimeSpan.FromSeconds(5)
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtBearerAuthentication");

                        logger.LogWarning(
                            context.Exception,
                            "JWT authentication failed: {Message}",
                            context.Exception.Message);

                        if (context.Exception is SecurityTokenExpiredException)
                        {
                            context.HttpContext.Items["TokenExpired"] = true;
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        var isExpired = context.HttpContext.Items.ContainsKey("TokenExpired");
                        var detail = isExpired
                            ? "The access token has expired."
                            : !string.IsNullOrWhiteSpace(context.ErrorDescription)
                                ? context.ErrorDescription
                                : "A valid Bearer token is required to access this resource.";

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";

                        var problemDetails = new ProblemDetails
                        {
                            Status = StatusCodes.Status401Unauthorized,
                            Title = "Unauthorized",
                            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                            Detail = detail,
                            Instance = context.Request.Path
                        };

                        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
                    }
                };
            });

        builder.Services.AddAuthorization();

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddOpenApi(options =>
        {
            options.AddOperationTransformer<ApiExceptionOperationTransformer>();
            options.AddOperationTransformer<IdentityApiOperationTransformer>();
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Name = "Authorization",
                    Description = "Bearer {token}"
                };

                document.Security ??= [];
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });

                return Task.CompletedTask;
            });
        });

        builder.Services.AddCors();
    }

    public static void AddKeyVaultIfConfigured(this IHostApplicationBuilder builder)
    {
        var keyVaultUri = builder.Configuration["AZURE_KEY_VAULT_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            builder.Configuration.AddAzureKeyVault(
                new Uri(keyVaultUri),
                new DefaultAzureCredential());
        }
    }
}
