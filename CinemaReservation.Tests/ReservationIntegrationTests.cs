using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using CinemaReservation.Api.Data;
using CinemaReservation.Api.DTOs;
using CinemaReservation.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace CinemaReservation.Tests;

[Collection("Integration")]
public class ReservationIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ReservationIntegrationTests(
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
    public async Task CreateReservation_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/reservations",
            new
            {
                showtimeId = 1,
                seatIds = new[] { 1 }
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithValidRequest_ReturnsCreated()
    {
        var (token, userId) =
            await CreateUserTokenAsync();

        var (showtimeId, seatId) =
            await CreateReservableShowtimeAsync();

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

        var response = await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var reservation =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        var reservationId =
            reservation
                .GetProperty("id")
                .GetInt32();

        Assert.Equal(
            showtimeId,
            reservation
                .GetProperty("showtimeId")
                .GetInt32());

        Assert.Equal(
            12.00m,
            reservation
                .GetProperty("totalPrice")
                .GetDecimal());

        var seats =
            reservation
                .GetProperty("seats")
                .EnumerateArray()
                .ToList();

        Assert.Single(seats);

        Assert.Equal(
            seatId,
            seats[0]
                .GetProperty("seatId")
                .GetInt32());

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var context =
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var persistedReservation =
            await context.Reservations
                .Include(reservation => reservation.ReservationSeats)
                .SingleAsync(reservation =>
                    reservation.Id == reservationId);

        Assert.Equal(
            showtimeId,
            persistedReservation.ShowtimeId);

        Assert.Equal(
            userId,
            persistedReservation.UserId);

        Assert.Single(
            persistedReservation.ReservationSeats);

        Assert.Equal(
            seatId,
            persistedReservation.ReservationSeats
                .Single()
                .SeatId);
    }

    [Fact]
    public async Task GetReservationById_AsOwner_ReturnsOk()
    {
        var (token, _) =
            await CreateUserTokenAsync();

        var (showtimeId, seatId) =
            await CreateReservableShowtimeAsync();

        var reservationId =
            await CreateReservationAsync(
                token,
                showtimeId,
                seatId);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/reservations/{reservationId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var reservation =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            reservationId,
            reservation
                .GetProperty("id")
                .GetInt32());

        Assert.Equal(
            showtimeId,
            reservation
                .GetProperty("showtimeId")
                .GetInt32());

        var seats =
            reservation
                .GetProperty("seats")
                .EnumerateArray()
                .ToList();

        Assert.Single(seats);

        Assert.Equal(
            seatId,
            seats[0]
                .GetProperty("seatId")
                .GetInt32());
    }

    [Fact]
    public async Task GetReservationById_AsDifferentUser_ReturnsNotFound()
    {
        var (ownerToken, _) =
            await CreateUserTokenAsync();

        var (otherUserToken, _) =
            await CreateUserTokenAsync();

        var (showtimeId, seatId) =
            await CreateReservableShowtimeAsync();

        var reservationId =
            await CreateReservationAsync(
                ownerToken,
                showtimeId,
                seatId);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/reservations/{reservationId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                otherUserToken);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetReservationById_AsAdmin_ReturnsOk()
    {
        var (ownerToken, _) =
            await CreateUserTokenAsync();

        var adminToken =
            await CreateAdminTokenAsync();

        var (showtimeId, seatId) =
            await CreateReservableShowtimeAsync();

        var reservationId =
            await CreateReservationAsync(
                ownerToken,
                showtimeId,
                seatId);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/reservations/{reservationId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var reservation =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            reservationId,
            reservation
                .GetProperty("id")
                .GetInt32());

        Assert.Equal(
            showtimeId,
            reservation
                .GetProperty("showtimeId")
                .GetInt32());
    }

    [Fact]
    public async Task CreateReservation_WhenSeatAlreadyReserved_ReturnsConflict()
    {
        var (firstUserToken, _) =
            await CreateUserTokenAsync();

        var (secondUserToken, _) =
            await CreateUserTokenAsync();

        var (showtimeId, seatId) =
            await CreateReservableShowtimeAsync();

        await CreateReservationAsync(
            firstUserToken,
            showtimeId,
            seatId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/reservations");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                secondUserToken);

        request.Content = JsonContent.Create(
            new
            {
                showtimeId,
                seatIds = new[] { seatId }
            });

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithDuplicateSeatIds_ReturnsBadRequest()
    {
        var (token, _) =
            await CreateUserTokenAsync();

        var (showtimeId, seatId) =
            await CreateReservableShowtimeAsync();

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
                seatIds = new[]
                {
                seatId,
                seatId
                }
            });

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithNoSeats_ReturnsBadRequest()
    {
        var (token, _) =
            await CreateUserTokenAsync();

        var (showtimeId, _) =
            await CreateReservableShowtimeAsync();

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
                seatIds = Array.Empty<int>()
            });

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithNullSeatIds_ReturnsBadRequest()
    {
        var (token, _) =
            await CreateUserTokenAsync();

        var (showtimeId, _) =
            await CreateReservableShowtimeAsync();

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
                seatIds = (int[]?)null
            });

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithUnknownShowtime_ReturnsNotFound()
    {
        var (token, _) =
            await CreateUserTokenAsync();

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
                showtimeId = int.MaxValue,
                seatIds = new[] { 1 }
            });

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithUnknownSeat_ReturnsNotFound()
    {
        var (token, _) =
            await CreateUserTokenAsync();

        var (showtimeId, _) =
            await CreateReservableShowtimeAsync();

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
                seatIds = new[] { int.MaxValue }
            });

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithInactiveSeat_ReturnsBadRequest()
    {
        var (token, _) =
            await CreateUserTokenAsync();

        var (showtimeId, seatId) =
            await CreateReservableShowtimeAsync();

        await using (var scope =
            _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var seat =
                await context.Seats
                    .SingleAsync(seat =>
                        seat.Id == seatId);

            seat.IsActive = false;

            await context.SaveChangesAsync();
        }

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
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithSeatFromDifferentAuditorium_ReturnsBadRequest()
    {
        var (token, _) =
            await CreateUserTokenAsync();

        var (showtimeId, _) =
            await CreateReservableShowtimeAsync();

        int foreignSeatId;

        await using (var scope =
            _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var suffix =
                Guid.NewGuid().ToString("N");

            var auditorium = new Auditorium
            {
                Name = $"Foreign Screen {suffix}",
                IsActive = true
            };

            context.Auditoriums.Add(auditorium);

            await context.SaveChangesAsync();

            var seat = new Seat
            {
                AuditoriumId = auditorium.Id,
                Row = "B",
                Number = 1,
                IsActive = true
            };

            context.Seats.Add(seat);

            await context.SaveChangesAsync();

            foreignSeatId = seat.Id;
        }

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
                seatIds = new[] { foreignSeatId }
            });

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithCancelledShowtime_ReturnsBadRequest()
    {
        var (token, _) =
            await CreateUserTokenAsync();

        var (showtimeId, seatId) =
            await CreateReservableShowtimeAsync();

        await using (var scope =
            _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var showtime =
                await context.Showtimes
                    .SingleAsync(showtime =>
                        showtime.Id == showtimeId);

            showtime.Status = ShowtimeStatus.Cancelled;

            await context.SaveChangesAsync();
        }

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
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithPastShowtime_ReturnsBadRequest()
    {
        var (token, _) =
            await CreateUserTokenAsync();

        var (showtimeId, seatId) =
            await CreateReservableShowtimeAsync();

        await using (var scope =
            _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var showtime =
                await context.Showtimes
                    .SingleAsync(showtime =>
                        showtime.Id == showtimeId);

            showtime.StartsAt =
                DateTimeOffset.UtcNow.AddHours(-2);

            showtime.EndsAt =
                DateTimeOffset.UtcNow.AddHours(-1);

            await context.SaveChangesAsync();
        }

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
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task GetReservationById_AfterShowtimePriceChanges_PreservesOriginalPrice()
    {
        var (token, _) =
            await CreateUserTokenAsync();

        var (showtimeId, seatId) =
            await CreateReservableShowtimeAsync();

        var reservationId =
            await CreateReservationAsync(
                token,
                showtimeId,
                seatId);

        await using (var scope =
            _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var showtime =
                await context.Showtimes
                    .SingleAsync(showtime =>
                        showtime.Id == showtimeId);

            showtime.TicketPrice = 25m;

            await context.SaveChangesAsync();
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/reservations/{reservationId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var reservation =
            await response.Content
                .ReadFromJsonAsync<ReservationResponse>();

        Assert.NotNull(reservation);
        Assert.Single(reservation.Seats);
        Assert.Equal(12m, reservation.Seats[0].UnitPrice);
        Assert.Equal(12m, reservation.TotalPrice);
    }

    [Fact]
    public async Task CreateReservation_WhenTwoUsersReserveSameSeatConcurrently_AllowsOnlyOne()
    {
        var (showtimeId, seatId) = await CreateReservableShowtimeAsync();

        var (firstToken, _) = await CreateUserTokenAsync();
        var (secondToken, _) = await CreateUserTokenAsync();

        async Task<HttpResponseMessage> ReserveAsync(string token)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/reservations")
            {
                Content = JsonContent.Create(
                    new CreateReservationRequest
                    {
                        ShowtimeId = showtimeId,
                        SeatIds = [seatId]
                    })
            };

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            return await _client.SendAsync(request);
        }

        // Submit both requests without awaiting either one first so they can
        // compete for the same active seat allocation at the database boundary.
        var firstReservationTask = ReserveAsync(firstToken);
        var secondReservationTask = ReserveAsync(secondToken);

        var responses = await Task.WhenAll(
            firstReservationTask,
            secondReservationTask);

        Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.Created);

        Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.Conflict);

        await using var scope = _factory.Services.CreateAsyncScope();

        var context =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var activeAllocations =
            await context.ReservationSeats
                .AsNoTracking()
                .CountAsync(
                    reservationSeat =>
                        reservationSeat.ShowtimeId == showtimeId &&
                        reservationSeat.SeatId == seatId &&
                        reservationSeat.ReleasedAt == null);

        Assert.Equal(1, activeAllocations);
    }

    private async Task<(string Token, string UserId)> CreateUserTokenAsync()
    {
        var email =
            $"reservation-{Guid.NewGuid():N}@example.com";

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

        var registeredUser =
            await registrationResponse.Content
                .ReadFromJsonAsync<JsonElement>();

        var userId =
            registeredUser
                .GetProperty("id")
                .GetString()
            ?? throw new InvalidOperationException(
                "Registration response did not contain a user ID.");

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

        var token =
            json
                .GetProperty("accessToken")
                .GetString()
            ?? throw new InvalidOperationException(
                "Login response did not contain an access token.");

        return (token, userId);
    }

    private async Task<(int ShowtimeId, int SeatId)>
    CreateReservableShowtimeAsync()
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
            Title = $"Reservation Movie {suffix}",
            Description = "Integration-test movie.",
            DurationMinutes = 120,
            IsActive = true
        };

        var auditorium = new Auditorium
        {
            Name = $"Reservation Screen {suffix}",
            IsActive = true
        };

        context.Movies.Add(movie);
        context.Auditoriums.Add(auditorium);

        await context.SaveChangesAsync();

        var seat = new Seat
        {
            AuditoriumId = auditorium.Id,
            Row = "A",
            Number = 1,
            IsActive = true
        };

        context.Seats.Add(seat);

        var startsAt =
            DateTimeOffset.UtcNow.AddDays(10);

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
            seat.Id);
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


}