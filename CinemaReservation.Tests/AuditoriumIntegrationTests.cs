using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaReservation.Api.Data;
using CinemaReservation.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaReservation.Tests;

// Validates auditorium-management behavior through the real API pipeline
// against the isolated PostgreSQL integration-test database.
[Collection("Integration")]
public class AuditoriumIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuditoriumIntegrationTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;

        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
    }

    [Fact]
    public async Task GetAuditoriums_ReturnsAuditoriumsOrderedByName()
    {
        var suffix = Guid.NewGuid().ToString("N");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            context.Auditoriums.AddRange(
                new Auditorium
                {
                    Name = $"Screen B {suffix}"
                },
                new Auditorium
                {
                    Name = $"Screen A {suffix}"
                });

            await context.SaveChangesAsync();
        }

        var response =
            await _client.GetAsync("/api/auditoriums");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var auditoriums =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        var matchingNames =
            auditoriums.EnumerateArray()
                .Select(auditorium =>
                    auditorium.GetProperty("name").GetString())
                .Where(name =>
                    name is not null &&
                    name.EndsWith(
                        suffix,
                        StringComparison.Ordinal))
                .ToArray();

        Assert.Equal(
            new[]
            {
                $"Screen A {suffix}",
                $"Screen B {suffix}"
            },
            matchingNames);
    }

    [Fact]
    public async Task GetAuditoriumById_WithUnknownAuditorium_ReturnsNotFound()
    {
        var response =
            await _client.GetAsync(
                $"/api/auditoriums/{int.MaxValue}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateAuditorium_WithAdminToken_ReturnsCreated()
    {
        var adminToken =
            await GetAdminAccessTokenAsync();

        var name =
            $"  Screen {Guid.NewGuid():N}  ";

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
            HttpStatusCode.Created,
            response.StatusCode);

        var auditorium =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        var auditoriumId =
            auditorium.GetProperty("id").GetInt32();

        Assert.True(auditoriumId > 0);

        // Names are normalized before persistence so incidental whitespace
        // supplied by clients does not become part of physical room identity.
        Assert.Equal(
            name.Trim(),
            auditorium.GetProperty("name").GetString());

        Assert.NotNull(response.Headers.Location);

        Assert.EndsWith(
            $"/api/auditoriums/{auditoriumId}",
            response.Headers.Location.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAuditorium_WithDuplicateName_ReturnsConflict()
    {
        var name =
            $"Duplicate Screen {Guid.NewGuid():N}";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            context.Auditoriums.Add(
                new Auditorium
                {
                    Name = name
                });

            await context.SaveChangesAsync();
        }

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
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateAuditorium_WithAdminToken_UpdatesName()
    {
        int auditoriumId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var auditorium = new Auditorium
            {
                Name = $"Original Screen {Guid.NewGuid():N}"
            };

            context.Auditoriums.Add(auditorium);

            await context.SaveChangesAsync();

            auditoriumId = auditorium.Id;
        }

        var adminToken =
            await GetAdminAccessTokenAsync();

        var updatedName =
            $"Updated Screen {Guid.NewGuid():N}";

        using var request =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/auditoriums/{auditoriumId}")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Name = updatedName
                    })
            };

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        await using var verificationScope =
            _factory.Services.CreateAsyncScope();

        var verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var persistedAuditorium =
            await verificationContext.Auditoriums
                .AsNoTracking()
                .SingleAsync(
                    auditorium =>
                        auditorium.Id == auditoriumId);

        Assert.Equal(
            updatedName,
            persistedAuditorium.Name);
    }

    [Fact]
    public async Task DeleteAuditorium_WithSeats_ReturnsConflict()
    {
        int auditoriumId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var auditorium = new Auditorium
            {
                Name = $"Occupied Screen {Guid.NewGuid():N}"
            };

            auditorium.Seats.Add(
                new Seat
                {
                    Row = "A",
                    Number = 1
                });

            context.Auditoriums.Add(auditorium);

            await context.SaveChangesAsync();

            auditoriumId = auditorium.Id;
        }

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/auditoriums/{auditoriumId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

    [Fact]
    public async Task DeleteEmptyAuditorium_WithAdminToken_ReturnsNoContent()
    {
        int auditoriumId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var auditorium = new Auditorium
            {
                Name = $"Empty Screen {Guid.NewGuid():N}"
            };

            context.Auditoriums.Add(auditorium);

            await context.SaveChangesAsync();

            auditoriumId = auditorium.Id;
        }

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/auditoriums/{auditoriumId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);

        await using var verificationScope =
            _factory.Services.CreateAsyncScope();

        var verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        Assert.False(
            await verificationContext.Auditoriums
                .AnyAsync(
                    auditorium =>
                        auditorium.Id == auditoriumId));
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