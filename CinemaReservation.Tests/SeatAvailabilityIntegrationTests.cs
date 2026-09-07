using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaReservation.Api.Data;
using CinemaReservation.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaReservation.Tests;

[Collection("Integration")]
public class SeatAvailabilityIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public SeatAvailabilityIntegrationTests(
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
    public async Task GetSeatAvailability_WithValidShowtime_ReturnsSeats()
    {
        var (showtimeId, auditoriumId) =
            await CreateShowtimeWithSeatsAsync();

        var response =
            await _client.GetAsync(
                $"/api/showtimes/{showtimeId}/seats");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var availability =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            showtimeId,
            availability
                .GetProperty("showtimeId")
                .GetInt32());

        Assert.Equal(
            auditoriumId,
            availability
                .GetProperty("auditoriumId")
                .GetInt32());

        var seats =
            availability
                .GetProperty("seats")
                .EnumerateArray()
                .ToList();

        Assert.NotEmpty(seats);
    }



    [Fact]
    public async Task GetSeatAvailability_ReturnsCapacityAndAvailableSeatCount()
    {
        var (showtimeId, _) =
            await CreateShowtimeWithSeatsAsync();

        var response =
            await _client.GetAsync(
                $"/api/showtimes/{showtimeId}/seats");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var availability =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            3,
            availability
                .GetProperty("capacity")
                .GetInt32());

        Assert.Equal(
            3,
            availability
                .GetProperty("availableSeatCount")
                .GetInt32());
    }



    [Fact]
    public async Task GetSeatAvailability_ReturnsSeatsOrderedByRowThenNumber()
    {
        var (showtimeId, _) =
            await CreateShowtimeWithSeatsAsync();

        var response =
            await _client.GetAsync(
                $"/api/showtimes/{showtimeId}/seats");

        var availability =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        var seats =
            availability
                .GetProperty("seats")
                .EnumerateArray()
                .ToList();

        Assert.Collection(
            seats,
            seat =>
            {
                Assert.Equal(
                    "A",
                    seat.GetProperty("row").GetString());

                Assert.Equal(
                    1,
                    seat.GetProperty("number").GetInt32());
            },
            seat =>
            {
                Assert.Equal(
                    "A",
                    seat.GetProperty("row").GetString());

                Assert.Equal(
                    2,
                    seat.GetProperty("number").GetInt32());
            },
            seat =>
            {
                Assert.Equal(
                    "B",
                    seat.GetProperty("row").GetString());

                Assert.Equal(
                    1,
                    seat.GetProperty("number").GetInt32());
            });
    }

    [Fact]
    public async Task GetSeatAvailability_WithInactiveAuditorium_ReturnsNotFound()
    {
        var (showtimeId, auditoriumId) =
            await CreateShowtimeWithSeatsAsync();

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var context =
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var auditorium =
            await context.Auditoriums.FindAsync(auditoriumId);

        Assert.NotNull(auditorium);

        auditorium.IsActive = false;

        await context.SaveChangesAsync();

        var response =
            await _client.GetAsync(
                $"/api/showtimes/{showtimeId}/seats");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetSeatAvailability_WithUnknownShowtime_ReturnsNotFound()
    {
        var response =
            await _client.GetAsync(
                $"/api/showtimes/{int.MaxValue}/seats");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetSeatAvailability_ReturnsOnlySeatsFromShowtimeAuditorium()
    {
        var (showtimeId, _) =
            await CreateShowtimeWithSeatsAsync();

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var context =
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var suffix =
            Guid.NewGuid().ToString("N");

        var otherAuditorium = new Auditorium
        {
            Name = $"Other Screen {suffix}",
            IsActive = true
        };

        context.Auditoriums.Add(otherAuditorium);

        await context.SaveChangesAsync();

        context.Seats.Add(
            new Seat
            {
                AuditoriumId = otherAuditorium.Id,
                Row = "Z",
                Number = 99,
                IsActive = true
            });

        await context.SaveChangesAsync();

        var response =
            await _client.GetAsync(
                $"/api/showtimes/{showtimeId}/seats");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var availability =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        var seats =
            availability
                .GetProperty("seats")
                .EnumerateArray()
                .ToList();

        Assert.DoesNotContain(
            seats,
            seat =>
                seat.GetProperty("row").GetString() == "Z" &&
                seat.GetProperty("number").GetInt32() == 99);
    }


    [Fact]
    public async Task GetSeatAvailability_ExcludesInactiveSeats()
    {
        var (showtimeId, auditoriumId) =
            await CreateShowtimeWithSeatsAsync();

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var context =
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        context.Seats.Add(
            new Seat
            {
                AuditoriumId = auditoriumId,
                Row = "C",
                Number = 1,
                IsActive = false
            });

        await context.SaveChangesAsync();

        var response =
            await _client.GetAsync(
                $"/api/showtimes/{showtimeId}/seats");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var availability =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            3,
            availability
                .GetProperty("capacity")
                .GetInt32());

        var seats =
            availability
                .GetProperty("seats")
                .EnumerateArray()
                .ToList();

        Assert.DoesNotContain(
            seats,
            seat =>
                seat.GetProperty("row").GetString() == "C" &&
                seat.GetProperty("number").GetInt32() == 1);
    }

    [Fact]
    public async Task GetSeatAvailability_WithCancelledShowtime_ReturnsNotFound()
    {
        var (showtimeId, _) =
            await CreateShowtimeWithSeatsAsync();

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var context =
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var showtime =
            await context.Showtimes.FindAsync(showtimeId);

        Assert.NotNull(showtime);

        showtime.Status =
            ShowtimeStatus.Cancelled;

        await context.SaveChangesAsync();

        var response =
            await _client.GetAsync(
                $"/api/showtimes/{showtimeId}/seats");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetSeatAvailability_WithInactiveMovie_ReturnsNotFound()
    {
        var (showtimeId, _) =
            await CreateShowtimeWithSeatsAsync();

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var context =
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var showtime =
            await context.Showtimes.FindAsync(showtimeId);

        Assert.NotNull(showtime);

        var movie =
            await context.Movies.FindAsync(showtime.MovieId);

        Assert.NotNull(movie);

        movie.IsActive = false;

        await context.SaveChangesAsync();

        var response =
            await _client.GetAsync(
                $"/api/showtimes/{showtimeId}/seats");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    private async Task<(int ShowtimeId, int AuditoriumId)>
    CreateShowtimeWithSeatsAsync()
    {
        await using var scope =
            _factory.Services.CreateAsyncScope();

        var context =
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var suffix =
            Guid.NewGuid().ToString("N");

        var movie = new Movie
        {
            Title = $"Seat Availability Movie {suffix}",
            Description = "Integration-test movie.",
            DurationMinutes = 120,
            IsActive = true
        };

        var auditorium = new Auditorium
        {
            Name = $"Seat Availability Screen {suffix}",
            IsActive = true
        };

        context.Movies.Add(movie);
        context.Auditoriums.Add(auditorium);

        await context.SaveChangesAsync();

        var seats = new[]
        {
            new Seat
            {
                AuditoriumId = auditorium.Id,
                Row = "A",
                Number = 1,
                IsActive = true
            },
            new Seat
            {
                AuditoriumId = auditorium.Id,
                Row = "A",
                Number = 2,
                IsActive = true
            },
            new Seat
            {
                AuditoriumId = auditorium.Id,
                Row = "B",
                Number = 1,
                IsActive = true
            }
        };

        context.Seats.AddRange(seats);

        var startsAt =
            DateTimeOffset.UtcNow.AddDays(15);

        var showtime = new Showtime
        {
            MovieId = movie.Id,
            AuditoriumId = auditorium.Id,
            StartsAt = startsAt,
            EndsAt = startsAt.AddMinutes(movie.DurationMinutes),
            TicketPrice = 12.00m,
            Status = ShowtimeStatus.Scheduled
        };

        context.Showtimes.Add(showtime);

        await context.SaveChangesAsync();

        return (
            showtime.Id,
            auditorium.Id);
    }
}