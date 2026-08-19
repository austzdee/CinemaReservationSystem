using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CinemaReservation.Tests;

public class CustomWebApplicationFactory
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(
            (_, configurationBuilder) =>
            {
                var configuration =
                    configurationBuilder.Build();

                var developmentConnectionString =
                    configuration.GetConnectionString(
                        "DefaultConnection")
                    ?? throw new InvalidOperationException(
                        "Default database connection is not configured.");

                // Preserve the locally secured PostgreSQL credentials while
                // redirecting integration tests to the isolated test database.
                var testConnectionString =
                    new NpgsqlConnectionStringBuilder(
                        developmentConnectionString)
                    {
                        Database = "cinema_reservation_test"
                    };

                configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] =
                            testConnectionString.ConnectionString
                    });
            });
    }
}