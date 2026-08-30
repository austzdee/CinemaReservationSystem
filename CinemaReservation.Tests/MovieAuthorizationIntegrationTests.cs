using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CinemaReservation.Tests;

[Collection("Integration")]
public class MovieAuthorizationIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MovieAuthorizationIntegrationTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("POST", "/api/movies")]
    [InlineData("PUT", "/api/movies/999999")]
    [InlineData("DELETE", "/api/movies/999999")]
    public async Task MovieManagementEndpoints_WithoutAuthentication_ReturnUnauthorized(
        string method,
        string url)
    {
        using var client = CreateClient();

        using var request =
            CreateMovieManagementRequest(
                method,
                url);

        var response =
            await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/movies")]
    [InlineData("PUT", "/api/movies/999999")]
    [InlineData("DELETE", "/api/movies/999999")]
    public async Task MovieManagementEndpoints_WithRegularUser_ReturnForbidden(
        string method,
        string url)
    {
        using var client = CreateClient();

        var token =
            await RegisterAndLoginRegularUserAsync(client);

        using var request =
            CreateMovieManagementRequest(
                method,
                url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/movies", HttpStatusCode.BadRequest)]
    [InlineData("PUT", "/api/movies/999999", HttpStatusCode.NotFound)]
    [InlineData("DELETE", "/api/movies/999999", HttpStatusCode.NotFound)]
    public async Task MovieManagementEndpoints_WithAdmin_ReachesBusinessLogic(
        string method,
        string url,
        HttpStatusCode expectedStatusCode)
    {
        using var client = CreateClient();

        var token =
            await LoginAdminAsync(client);

        using var request =
            CreateMovieManagementRequest(
                method,
                url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await client.SendAsync(request);

        Assert.Equal(
            expectedStatusCode,
            response.StatusCode);
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress =
                    new Uri("https://localhost")
            });
    }

    private static HttpRequestMessage CreateMovieManagementRequest(
        string method,
        string url)
    {
        var request =
            new HttpRequestMessage(
                new HttpMethod(method),
                url);

        if (method is "POST" or "PUT")
        {
            request.Content =
                JsonContent.Create(new
                {
                    title = "Authorization Test",
                    description = "Authorization boundary test.",
                    durationMinutes = 120,

                    // Deliberately invalid reference. An administrator should
                    // reach the application boundary rather than be rejected
                    // by authentication or role authorization.
                    genreIds = new[] { int.MaxValue }
                });
        }

        return request;
    }

    private static async Task<string> RegisterAndLoginRegularUserAsync(
        HttpClient client)
    {
        var email =
            $"movie-auth-{Guid.NewGuid():N}@example.com";

        const string password = "Cinema1!";

        var registrationResponse =
            await client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    Email = email,
                    Password = password
                });

        Assert.Equal(
            HttpStatusCode.Created,
            registrationResponse.StatusCode);

        var loginResponse =
            await client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    Email = email,
                    Password = password
                });

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode);

        return await ReadAccessTokenAsync(
            loginResponse);
    }

    private static async Task<string> LoginAdminAsync(
        HttpClient client)
    {
        var adminPassword =
            Environment.GetEnvironmentVariable(
                "TEST_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "TEST_ADMIN_PASSWORD is not configured.");
        }

        var loginResponse =
            await client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    Email = "admin@example.com",
                    Password = adminPassword
                });

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode);

        return await ReadAccessTokenAsync(
            loginResponse);
    }

    private static async Task<string> ReadAccessTokenAsync(
        HttpResponseMessage response)
    {
        var json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        if (!json.TryGetProperty(
                "accessToken",
                out var tokenElement))
        {
            throw new InvalidOperationException(
                "Login response did not contain an access token.");
        }

        return tokenElement.GetString()
            ?? throw new InvalidOperationException(
                "Access token was empty.");
    }
}