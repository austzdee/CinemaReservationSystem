using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaReservation.Api.Data;
using CinemaReservation.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaReservation.Tests;

// Verifies request-shape validation at the HTTP boundary before business
// logic or persistence is allowed to process invalid auditorium/seat input.
[Collection("Integration")]
public class AuditoriumSeatValidationIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuditoriumSeatValidationIntegrationTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;

        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAuditorium_WithInvalidName_ReturnsBadRequest(
        string name)
    {
        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/auditoriums")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Name = name
                    })
            };

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateAuditorium_WithNameOverMaximumLength_ReturnsBadRequest()
    {
        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/auditoriums")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Name = new string('A', 101)
                    })
            };

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateSeat_WithInvalidRow_ReturnsBadRequest(
        string row)
    {
        var auditoriumId =
            await CreateAuditoriumAsync();

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/auditoriums/{auditoriumId}/seats")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Row = row,
                        Number = 1
                    })
            };

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateSeat_WithRowOverMaximumLength_ReturnsBadRequest()
    {
        var auditoriumId =
            await CreateAuditoriumAsync();

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/auditoriums/{auditoriumId}/seats")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Row = new string('A', 11),
                        Number = 1
                    })
            };

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateSeat_WithNonPositiveNumber_ReturnsBadRequest(
        int number)
    {
        var auditoriumId =
            await CreateAuditoriumAsync();

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/auditoriums/{auditoriumId}/seats")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Row = "A",
                        Number = number
                    })
            };

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    private async Task<int> CreateAuditoriumAsync()
    {
        await using var scope =
            _factory.Services.CreateAsyncScope();

        var context =
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var auditorium = new Auditorium
        {
            Name = $"Validation Screen {Guid.NewGuid():N}"
        };

        context.Auditoriums.Add(auditorium);

        await context.SaveChangesAsync();

        return auditorium.Id;
    }

    private async Task<string> GetAdminAccessTokenAsync()
    {
        var adminPassword =
            Environment.GetEnvironmentVariable(
                "TEST_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "TEST_ADMIN_PASSWORD is not configured.");
        }

        var response =
            await _client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    Email = "admin@example.com",
                    Password = adminPassword
                });

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        return json
            .GetProperty("accessToken")
            .GetString()
            ?? throw new InvalidOperationException(
                "Login response did not contain an access token.");
    }
}