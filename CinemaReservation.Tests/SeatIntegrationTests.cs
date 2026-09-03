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

// Validates physical-seat workflows through the real API pipeline
// against the isolated PostgreSQL integration-test database.
[Collection("Integration")]
public class SeatIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public SeatIntegrationTests(
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
    public async Task GetSeats_WithExistingAuditorium_ReturnsSeatsOrderedByRowAndNumber()
    {
        int auditoriumId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var auditorium = new Auditorium
            {
                Name = $"Seat Order Screen {Guid.NewGuid():N}"
            };

            auditorium.Seats.Add(
                new Seat
                {
                    Row = "B",
                    Number = 1
                });

            auditorium.Seats.Add(
                new Seat
                {
                    Row = "A",
                    Number = 2
                });

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

        var response =
            await _client.GetAsync(
                $"/api/auditoriums/{auditoriumId}/seats");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var seats =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        var positions =
            seats.EnumerateArray()
                .Select(seat =>
                    $"{seat.GetProperty("row").GetString()}-" +
                    $"{seat.GetProperty("number").GetInt32()}")
                .ToArray();

        Assert.Equal(
            new[]
            {
                "A-1",
                "A-2",
                "B-1"
            },
            positions);
    }

    [Fact]
    public async Task GetSeats_WithExistingEmptyAuditorium_ReturnsEmptyList()
    {
        int auditoriumId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var auditorium = new Auditorium
            {
                Name = $"Empty Seat Screen {Guid.NewGuid():N}"
            };

            context.Auditoriums.Add(auditorium);

            await context.SaveChangesAsync();

            auditoriumId = auditorium.Id;
        }

        var response =
            await _client.GetAsync(
                $"/api/auditoriums/{auditoriumId}/seats");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var seats =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            0,
            seats.GetArrayLength());
    }

    [Fact]
    public async Task GetSeats_WithUnknownAuditorium_ReturnsNotFound()
    {
        var response =
            await _client.GetAsync(
                $"/api/auditoriums/{int.MaxValue}/seats");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateSeat_WithAdminToken_NormalizesRowAndReturnsCreated()
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
                        Row = "  a  ",
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
            HttpStatusCode.Created,
            response.StatusCode);

        var seat =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            auditoriumId,
            seat.GetProperty("auditoriumId").GetInt32());

        Assert.Equal(
            "A",
            seat.GetProperty("row").GetString());

        Assert.Equal(
            1,
            seat.GetProperty("number").GetInt32());

        // Row normalization must also be reflected in persisted physical
        // seat identity, not only in the API response.
        await using var verificationScope =
            _factory.Services.CreateAsyncScope();

        var verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var seatId =
            seat.GetProperty("id").GetInt32();

        var persistedSeat =
            await verificationContext.Seats
                .AsNoTracking()
                .SingleAsync(
                    persisted =>
                        persisted.Id == seatId);

        Assert.Equal(
            "A",
            persistedSeat.Row);
    }

    [Fact]
    public async Task CreateSeat_WithDuplicatePosition_ReturnsConflict()
    {
        int auditoriumId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var auditorium = new Auditorium
            {
                Name = $"Duplicate Seat Screen {Guid.NewGuid():N}"
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
                HttpMethod.Post,
                $"/api/auditoriums/{auditoriumId}/seats")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Row = "a",
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
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateSeat_WithUnknownAuditorium_ReturnsNotFound()
    {
        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/auditoriums/{int.MaxValue}/seats")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Row = "A",
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
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateSeat_WithAdminToken_UpdatesPhysicalPosition()
    {
        int auditoriumId;
        int seatId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var auditorium = new Auditorium
            {
                Name = $"Update Seat Screen {Guid.NewGuid():N}"
            };

            var seat = new Seat
            {
                Row = "A",
                Number = 1
            };

            auditorium.Seats.Add(seat);

            context.Auditoriums.Add(auditorium);

            await context.SaveChangesAsync();

            auditoriumId = auditorium.Id;
            seatId = seat.Id;
        }

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/auditoriums/{auditoriumId}/seats/{seatId}")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Row = " b ",
                        Number = 4
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

        var updatedSeat =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "B",
            updatedSeat.GetProperty("row").GetString());

        Assert.Equal(
            4,
            updatedSeat.GetProperty("number").GetInt32());

        await using var verificationScope =
            _factory.Services.CreateAsyncScope();

        var verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var persistedSeat =
            await verificationContext.Seats
                .AsNoTracking()
                .SingleAsync(
                    seat =>
                        seat.Id == seatId);

        Assert.Equal("B", persistedSeat.Row);
        Assert.Equal(4, persistedSeat.Number);
    }

    [Fact]
    public async Task UpdateSeat_ThroughWrongAuditorium_ReturnsNotFound()
    {
        int firstAuditoriumId;
        int secondAuditoriumId;
        int seatId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var firstAuditorium = new Auditorium
            {
                Name = $"Seat Parent A {Guid.NewGuid():N}"
            };

            var secondAuditorium = new Auditorium
            {
                Name = $"Seat Parent B {Guid.NewGuid():N}"
            };

            var seat = new Seat
            {
                Row = "A",
                Number = 1
            };

            firstAuditorium.Seats.Add(seat);

            context.Auditoriums.AddRange(
                firstAuditorium,
                secondAuditorium);

            await context.SaveChangesAsync();

            firstAuditoriumId = firstAuditorium.Id;
            secondAuditoriumId = secondAuditorium.Id;
            seatId = seat.Id;
        }

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/auditoriums/{secondAuditoriumId}/seats/{seatId}")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Row = "B",
                        Number = 2
                    })
            };

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        // The nested-route boundary must prevent cross-auditorium mutation.
        await using var verificationScope =
            _factory.Services.CreateAsyncScope();

        var verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var persistedSeat =
            await verificationContext.Seats
                .AsNoTracking()
                .SingleAsync(
                    seat =>
                        seat.Id == seatId);

        Assert.Equal(firstAuditoriumId, persistedSeat.AuditoriumId);
        Assert.Equal("A", persistedSeat.Row);
        Assert.Equal(1, persistedSeat.Number);
    }

    [Fact]
    public async Task DeleteSeat_WithAdminToken_RemovesSeat()
    {
        int auditoriumId;
        int seatId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var auditorium = new Auditorium
            {
                Name = $"Delete Seat Screen {Guid.NewGuid():N}"
            };

            var seat = new Seat
            {
                Row = "A",
                Number = 1
            };

            auditorium.Seats.Add(seat);

            context.Auditoriums.Add(auditorium);

            await context.SaveChangesAsync();

            auditoriumId = auditorium.Id;
            seatId = seat.Id;
        }

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/auditoriums/{auditoriumId}/seats/{seatId}");

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
            await verificationContext.Seats
                .AnyAsync(
                    seat =>
                        seat.Id == seatId));
    }

    [Fact]
    public async Task DeleteSeat_ThroughWrongAuditorium_ReturnsNotFound()
    {
        int firstAuditoriumId;
        int secondAuditoriumId;
        int seatId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var firstAuditorium = new Auditorium
            {
                Name = $"Delete Parent A {Guid.NewGuid():N}"
            };

            var secondAuditorium = new Auditorium
            {
                Name = $"Delete Parent B {Guid.NewGuid():N}"
            };

            var seat = new Seat
            {
                Row = "A",
                Number = 1
            };

            firstAuditorium.Seats.Add(seat);

            context.Auditoriums.AddRange(
                firstAuditorium,
                secondAuditorium);

            await context.SaveChangesAsync();

            firstAuditoriumId = firstAuditorium.Id;
            secondAuditoriumId = secondAuditorium.Id;
            seatId = seat.Id;
        }

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/auditoriums/{secondAuditoriumId}/seats/{seatId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        await using var verificationScope =
            _factory.Services.CreateAsyncScope();

        var verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var persistedSeat =
            await verificationContext.Seats
                .AsNoTracking()
                .SingleAsync(
                    seat =>
                        seat.Id == seatId);

        Assert.Equal(
            firstAuditoriumId,
            persistedSeat.AuditoriumId);
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
            Name = $"Seat Test Screen {Guid.NewGuid():N}"
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