using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaReservation.Api.Data;
using CinemaReservation.Api.DTOs.Reports;
using CinemaReservation.Api.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaReservation.Tests;

[Collection("Integration")]
public class ReportIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ReportIntegrationTests(
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
    public async Task GetSummary_AsRegularUser_ReturnsForbidden()
    {
        var token =
            await CreateUserTokenAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/reports/summary" +
            "?from=2026-09-01T00:00:00Z" +
            "&to=2026-10-01T00:00:00Z");

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
    public async Task GetSummary_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response =
            await _client.GetAsync(
                "/api/reports/summary" +
                "?from=2026-09-01T00:00:00Z" +
                "&to=2026-10-01T00:00:00Z");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_AsAdmin_ReturnsCorrectAggregates()
    {
        var adminToken =
            await CreateAdminTokenAsync();

        var (showtimeId, firstSeatId, secondSeatId) =
            await CreateReportShowtimeAsync();

        var firstUserToken =
            await CreateUserTokenAsync();

        var secondUserToken =
            await CreateUserTokenAsync();

        var firstReservationId =
            await CreateReservationAsync(
                firstUserToken,
                showtimeId,
                firstSeatId);

        await CreateReservationAsync(
            secondUserToken,
            showtimeId,
            secondSeatId);

        await CancelReservationAsync(
            firstUserToken,
            firstReservationId);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/reports/summary" +
            $"?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))}" +
            $"&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(30).ToString("O"))}" +
            $"&showtimeId={showtimeId}");
        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var report =
            await response.Content
                .ReadFromJsonAsync<ReservationSummaryReportResponse>();

        Assert.NotNull(report);

        Assert.Equal(
            2,
            report.TotalReservations);

        Assert.Equal(
            1,
            report.ConfirmedReservations);

        Assert.Equal(
            1,
            report.CancelledReservations);

        Assert.Equal(
            1,
            report.ActiveSeatsReserved);

        Assert.Equal(
            2,
            report.TotalCapacity);

        Assert.Equal(
            50m,
            report.OccupancyPercentage);

        Assert.Equal(
            12m,
            report.Revenue);
    }

    [Fact]
    public async Task GetShowtimes_AsAdmin_ReturnsCorrectShowtimeAggregates()
    {
        var adminToken =
            await CreateAdminTokenAsync();

        var (showtimeId, firstSeatId, secondSeatId) =
            await CreateReportShowtimeAsync();

        var firstUserToken =
            await CreateUserTokenAsync();

        var secondUserToken =
            await CreateUserTokenAsync();

        var firstReservationId =
            await CreateReservationAsync(
                firstUserToken,
                showtimeId,
                firstSeatId);

        await CreateReservationAsync(
            secondUserToken,
            showtimeId,
            secondSeatId);

        await CancelReservationAsync(
            firstUserToken,
            firstReservationId);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/reports/showtimes" +
            $"?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))}" +
            $"&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(30).ToString("O"))}" +
            $"&showtimeId={showtimeId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var report =
            await response.Content
                .ReadFromJsonAsync<List<ShowtimeReportResponse>>();

        Assert.NotNull(report);

        var row =
            Assert.Single(report);

        Assert.Equal(
            showtimeId,
            row.ShowtimeId);

        Assert.Equal(
            2,
            row.Capacity);

        Assert.Equal(
            1,
            row.ReservedSeats);

        Assert.Equal(
            1,
            row.AvailableSeats);

        Assert.Equal(
            50m,
            row.OccupancyPercentage);

        Assert.Equal(
            12m,
            row.Revenue);
    }

    [Fact]
    public async Task GetMovies_AsAdmin_ReturnsCorrectMovieAggregates()
    {
        var adminToken = await CreateAdminTokenAsync();

        var (showtimeId, firstSeatId, secondSeatId) =
            await CreateReportShowtimeAsync();

        var firstUserToken = await CreateUserTokenAsync();
        var secondUserToken = await CreateUserTokenAsync();

        var firstReservationId =
            await CreateReservationAsync(
                firstUserToken,
                showtimeId,
                firstSeatId);

        await CreateReservationAsync(
            secondUserToken,
            showtimeId,
            secondSeatId);

        await CancelReservationAsync(
            firstUserToken,
            firstReservationId);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/reports/movies" +
            $"?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))}" +
            $"&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(20).ToString("O"))}" +
            $"&showtimeId={showtimeId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        using var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var reports =
            await response.Content
                .ReadFromJsonAsync<List<MovieReportResponse>>();

        Assert.NotNull(reports);

        var report = Assert.Single(reports);

        Assert.Equal(1, report.ShowtimeCount);
        Assert.Equal(1, report.ConfirmedReservations);
        Assert.Equal(1, report.ReservedSeats);
        Assert.Equal(12m, report.Revenue);
    }

    private async Task<string> CreateUserTokenAsync()
    {
        var email =
            $"report-{Guid.NewGuid():N}@example.com";

        const string password = "Cinema1!";

        var registrationResponse =
            await _client.PostAsJsonAsync(
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

    private async Task<string> CreateAdminTokenAsync()
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
            await _client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    Email = "admin@example.com",
                    Password = adminPassword
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
                "Admin login response did not contain an access token.");
    }



    private async Task<(int ShowtimeId, int FirstSeatId, int SecondSeatId)>
    CreateReportShowtimeAsync()
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
            Title = $"Report Movie {suffix}",
            Description = "Integration-test movie.",
            DurationMinutes = 120,
            IsActive = true
        };

        var auditorium = new Auditorium
        {
            Name = $"Report Screen {suffix}",
            IsActive = true
        };

        context.Movies.Add(movie);
        context.Auditoriums.Add(auditorium);

        await context.SaveChangesAsync();

        var firstSeat = new Seat
        {
            AuditoriumId = auditorium.Id,
            Row = "A",
            Number = 1,
            IsActive = true
        };

        var secondSeat = new Seat
        {
            AuditoriumId = auditorium.Id,
            Row = "A",
            Number = 2,
            IsActive = true
        };

        context.Seats.AddRange(
            firstSeat,
            secondSeat);

        var startsAt =
            DateTimeOffset.UtcNow.AddDays(10);

        var showtime = new Showtime
        {
            MovieId = movie.Id,
            AuditoriumId = auditorium.Id,
            StartsAt = startsAt,
            EndsAt = startsAt.AddMinutes(movie.DurationMinutes),
            TicketPrice = 12m,
            Status = ShowtimeStatus.Scheduled
        };

        context.Showtimes.Add(showtime);

        await context.SaveChangesAsync();

        return (
            showtime.Id,
            firstSeat.Id,
            secondSeat.Id);
    }

    private async Task<int> CreateReservationAsync(
    string token,
    int showtimeId,
    int seatId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/reservations");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        request.Content = JsonContent.Create(
            new
            {
                showtimeId,
                seatIds = new[] { seatId }
            });

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var reservation =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        return reservation
            .GetProperty("id")
            .GetInt32();
    }

    private async Task CancelReservationAsync(
    string token,
    int reservationId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/reservations/{reservationId}/cancel");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }


}