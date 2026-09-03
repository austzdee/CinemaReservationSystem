using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CinemaReservation.Tests;

// Verifies that auditorium and seat mutation endpoints enforce the Admin
// authorization boundary independently of their business behavior.
[Collection("Integration")]
public class AuditoriumSeatAuthorizationIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuditoriumSeatAuthorizationIntegrationTests(
        CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
    }

    public static TheoryData<HttpMethod, string, object?> ProtectedEndpoints =>
        new()
        {
            {
                HttpMethod.Post,
                "/api/auditoriums",
                new
                {
                    Name = "Authorization Test Screen"
                }
            },
            {
                HttpMethod.Put,
                "/api/auditoriums/1",
                new
                {
                    Name = "Authorization Test Screen"
                }
            },
            {
                HttpMethod.Delete,
                "/api/auditoriums/1",
                null
            },
            {
                HttpMethod.Post,
                "/api/auditoriums/1/seats",
                new
                {
                    Row = "A",
                    Number = 1
                }
            },
            {
                HttpMethod.Put,
                "/api/auditoriums/1/seats/1",
                new
                {
                    Row = "A",
                    Number = 1
                }
            },
            {
                HttpMethod.Delete,
                "/api/auditoriums/1/seats/1",
                null
            }
        };

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized(
        HttpMethod method,
        string path,
        object? body)
    {
        using var request =
            CreateRequest(
                method,
                path,
                body);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task ProtectedEndpoint_WithRegularUserToken_ReturnsForbidden(
        HttpMethod method,
        string path,
        object? body)
    {
        var token =
            await CreateRegularUserTokenAsync();

        using var request =
            CreateRequest(
                method,
                path,
                body);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task AuditoriumAndSeatReadEndpoints_RemainPublic()
    {
        var auditoriumResponse =
            await _client.GetAsync(
                "/api/auditoriums");

        Assert.Equal(
            HttpStatusCode.OK,
            auditoriumResponse.StatusCode);

        // A missing parent may legitimately return 404; the important point
        // is that the public read endpoint must not require authentication.
        var seatsResponse =
            await _client.GetAsync(
                $"/api/auditoriums/{int.MaxValue}/seats");

        Assert.Equal(
            HttpStatusCode.NotFound,
            seatsResponse.StatusCode);
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        object? body)
    {
        var request =
            new HttpRequestMessage(
                method,
                path);

        if (body is not null)
        {
            request.Content =
                JsonContent.Create(body);
        }

        return request;
    }

    private async Task<string> CreateRegularUserTokenAsync()
    {
        var email =
            $"phase5-user-{Guid.NewGuid():N}@example.com";

        const string password =
            "CinemaTest1!";

        var registerResponse =
            await _client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    Email = email,
                    Password = password
                });

        Assert.Equal(
            HttpStatusCode.Created,
            registerResponse.StatusCode);

        var loginResponse =
            await _client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    Email = email,
                    Password = password
                });

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode);

        var json =
            await loginResponse.Content
                .ReadFromJsonAsync<JsonElement>();

        return json
            .GetProperty("accessToken")
            .GetString()
            ?? throw new InvalidOperationException(
                "Login response did not contain an access token.");
    }
}