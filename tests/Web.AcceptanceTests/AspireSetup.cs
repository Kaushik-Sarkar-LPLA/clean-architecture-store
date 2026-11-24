using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CleanArchitecture.Web.AcceptanceTests;

[SetUpFixture]
public class AspireSetup
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(90);

    public static IDistributedApplicationTestingBuilder Builder { get; private set; } = null!;
    public static DistributedApplication App { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        Environment.SetEnvironmentVariable("JwtSettings__Secret", "SuperSecretTestKeyForAcceptanceTests2026Minimum32Chars!");
        Environment.SetEnvironmentVariable("JwtSettings__Issuer", "https://store.pixilens.online");
        Environment.SetEnvironmentVariable("JwtSettings__Audience", "https://store.pixilens.online");

        var cts = new CancellationTokenSource(DefaultTimeout);
        var cancellationToken = cts.Token;

        Builder = await DistributedApplicationTestingBuilder
             .CreateAsync<Projects.AppHost>(
                args: [],
                configureBuilder: (options, _) =>
                {
                    options.DisableDashboard = false; // Enable the dashboard for testing purposes
                });

        Builder.Configuration["ASPIRE_ALLOW_UNSECURED_TRANSPORT"] = "true";

        var webApi = Builder.Resources.OfType<IResourceWithEnvironment>()
            .FirstOrDefault(r => r.Name == Services.WebApi);
        if (webApi is not null)
        {
            Builder.CreateResourceBuilder(webApi)
                .WithEnvironment("JwtSettings__Secret", "SuperSecretTestKeyForAcceptanceTests2026Minimum32Chars!")
                .WithEnvironment("JwtSettings__Issuer", "https://store.pixilens.online")
                .WithEnvironment("JwtSettings__Audience", "https://store.pixilens.online");
        }

        Builder.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            // Override the logging filters from the app's configuration
            logging.AddFilter(Builder.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
        });

        Builder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        App = await Builder
            .BuildAsync(cancellationToken)
            .WaitAsync(cancellationToken);

        await App
            .StartAsync(cancellationToken)
            .WaitAsync(cancellationToken);

        await Task.WhenAll(
            App.ResourceNotifications.WaitForResourceHealthyAsync(Services.WebApi, cancellationToken).WaitAsync(cancellationToken),
            App.ResourceNotifications.WaitForResourceHealthyAsync(Services.WebFrontend, cancellationToken).WaitAsync(cancellationToken));
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await App.DisposeAsync();
    }
}
