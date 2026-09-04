using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaReservation.Api.Data;
using CinemaReservation.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaReservation.Tests;

// Validates the showtime HTTP contract through the real API pipeline
// against the isolated PostgreSQL integration-test database.
[Collection("Integration")]
public class ShowtimeIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ShowtimeIntegrationTests(
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
    public async Task GetShowtimes_WithoutAuthentication_ReturnsOk()
    {
        var response =
            await _client.GetAsync("/api/showtimes");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task GetShowtimeById_WithUnknownShowtime_ReturnsNotFound()
    {
        var response =
            await _client.GetAsync(
                $"/api/showtimes/{int.MaxValue}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateShowtime_WithAdminToken_ReturnsCreated()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync();

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/showtimes")
            {
                Content = JsonContent.Create(
                    new
                    {
                        MovieId = movieId,
                        AuditoriumId = auditoriumId,
                        StartsAt =
                            DateTimeOffset.UtcNow.AddDays(30),
                        TicketPrice = 12.50m
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

        var showtime =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            movieId,
            showtime.GetProperty("movieId").GetInt32());

        Assert.Equal(
            auditoriumId,
            showtime.GetProperty("auditoriumId").GetInt32());
    }

    [Fact]
    public async Task UpdateShowtime_WithAdminToken_ReturnsOk()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync();

        var showtimeId =
            await CreateShowtimeDirectlyAsync(
                movieId,
                auditoriumId);

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/showtimes/{showtimeId}")
            {
                Content = JsonContent.Create(
                    new
                    {
                        MovieId = movieId,
                        AuditoriumId = auditoriumId,
                        StartsAt =
                            DateTimeOffset.UtcNow.AddDays(40),
                        TicketPrice = 15.00m
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

        var showtime =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            15.00m,
            showtime.GetProperty("ticketPrice").GetDecimal());
    }

    [Fact]
    public async Task CancelShowtime_WithAdminToken_ReturnsNoContent()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync();

        var showtimeId =
            await CreateShowtimeDirectlyAsync(
                movieId,
                auditoriumId);

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/showtimes/{showtimeId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);
    }

    [Fact]
    public async Task GetShowtimes_WithMovieFilter_ReturnsOnlyMatchingMovie()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync();

        var (otherMovieId, otherAuditoriumId) =
            await CreateSchedulingDependenciesAsync();

        var matchingShowtimeId =
            await CreateShowtimeDirectlyAsync(
                movieId,
                auditoriumId,
                DateTimeOffset.UtcNow.AddDays(50));

        await CreateShowtimeDirectlyAsync(
            otherMovieId,
            otherAuditoriumId,
            DateTimeOffset.UtcNow.AddDays(51));

        var response =
            await _client.GetAsync(
                $"/api/showtimes?movieId={movieId}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var showtimes =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        var items =
            showtimes.EnumerateArray().ToList();

        Assert.Contains(
            items,
            item =>
                item.GetProperty("id").GetInt32() ==
                matchingShowtimeId);

        Assert.All(
            items,
            item =>
                Assert.Equal(
                    movieId,
                    item.GetProperty("movieId").GetInt32()));
    }

    [Fact]
    public async Task GetShowtimes_WithAuditoriumFilter_ReturnsOnlyMatchingAuditorium()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync();

        var (otherMovieId, otherAuditoriumId) =
            await CreateSchedulingDependenciesAsync();

        await CreateShowtimeDirectlyAsync(
            movieId,
            auditoriumId,
            DateTimeOffset.UtcNow.AddDays(52));

        await CreateShowtimeDirectlyAsync(
            otherMovieId,
            otherAuditoriumId,
            DateTimeOffset.UtcNow.AddDays(53));

        var response =
            await _client.GetAsync(
                $"/api/showtimes?auditoriumId={auditoriumId}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var showtimes =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        var items =
            showtimes.EnumerateArray().ToList();

        Assert.NotEmpty(items);

        Assert.All(
            items,
            item =>
                Assert.Equal(
                    auditoriumId,
                    item.GetProperty("auditoriumId").GetInt32()));
    }

    [Fact]
    public async Task GetShowtimes_WithTimeWindow_ReturnsOnlyShowtimesInsideWindow()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync();

        var windowStart =
            DateTimeOffset.UtcNow.AddDays(60);

        var insideStart =
            windowStart.AddHours(2);

        var outsideStart =
            windowStart.AddDays(2);

        var insideShowtimeId =
            await CreateShowtimeDirectlyAsync(
                movieId,
                auditoriumId,
                insideStart);

        await CreateShowtimeDirectlyAsync(
            movieId,
            auditoriumId,
            outsideStart);

        var windowEnd =
            windowStart.AddDays(1);

        // Restrict the window to this test's unique movie so persisted data from
        // earlier integration runs cannot affect the boundary assertion.
        var response =
            await _client.GetAsync(
                $"/api/showtimes" +
                $"?movieId={movieId}" +
                $"&startsFrom={Uri.EscapeDataString(windowStart.ToString("O"))}" +
                $"&startsTo={Uri.EscapeDataString(windowEnd.ToString("O"))}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var showtimes =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        var items =
            showtimes.EnumerateArray().ToList();

        Assert.Single(items);

        Assert.Equal(
            insideShowtimeId,
            items[0].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task UpdateShowtime_WithUnknownId_ReturnsNotFound()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync();

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/showtimes/{int.MaxValue}")
            {
                Content = JsonContent.Create(
                    new
                    {
                        MovieId = movieId,
                        AuditoriumId = auditoriumId,
                        StartsAt =
                            DateTimeOffset.UtcNow.AddDays(70),
                        TicketPrice = 14.00m
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
    public async Task CancelShowtime_WithUnknownId_ReturnsNotFound()
    {
        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/showtimes/{int.MaxValue}");

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
    public async Task CreateShowtime_WithOverlap_ReturnsBadRequest()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync();

        var existingStart =
            DateTimeOffset.UtcNow.AddDays(80);

        await CreateShowtimeDirectlyAsync(
            movieId,
            auditoriumId,
            existingStart);

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/showtimes")
            {
                Content = JsonContent.Create(
                    new
                    {
                        MovieId = movieId,
                        AuditoriumId = auditoriumId,
                        StartsAt =
                            existingStart.AddMinutes(30),
                        TicketPrice = 12.00m
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
    [InlineData("POST", "/api/showtimes")]
    [InlineData("PUT", "/api/showtimes/999999")]
    [InlineData("DELETE", "/api/showtimes/999999")]
    public async Task ShowtimeWriteEndpoints_WithoutAuthentication_ReturnUnauthorized(
        string method,
        string url)
    {
        using var request =
            new HttpRequestMessage(
                new HttpMethod(method),
                url);

        if (method is "POST" or "PUT")
        {
            request.Content =
                JsonContent.Create(
                    new
                    {
                        MovieId = 1,
                        AuditoriumId = 1,
                        StartsAt =
                            DateTimeOffset.UtcNow.AddDays(10),
                        TicketPrice = 10.00m
                    });
        }

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/showtimes")]
    [InlineData("PUT", "/api/showtimes/999999")]
    [InlineData("DELETE", "/api/showtimes/999999")]
    public async Task ShowtimeWriteEndpoints_WithRegularUser_ReturnForbidden(
        string method,
        string url)
    {
        var userToken =
            await RegisterAndLoginRegularUserAsync(
                _client);

        using var request =
            new HttpRequestMessage(
                new HttpMethod(method),
                url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                userToken);

        if (method is "POST" or "PUT")
        {
            request.Content =
                JsonContent.Create(
                    new
                    {
                        MovieId = 1,
                        AuditoriumId = 1,
                        StartsAt =
                            DateTimeOffset.UtcNow.AddDays(10),
                        TicketPrice = 10.00m
                    });
        }

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    private async Task<(int MovieId, int AuditoriumId)>
        CreateSchedulingDependenciesAsync()
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
            Title = $"HTTP Showtime Movie {suffix}",
            Description = "Integration-test movie.",
            DurationMinutes = 120,
            IsActive = true
        };

        var auditorium = new Auditorium
        {
            Name = $"HTTP Showtime Screen {suffix}",
            IsActive = true
        };

        context.Movies.Add(movie);
        context.Auditoriums.Add(auditorium);

        await context.SaveChangesAsync();

        return (
            movie.Id,
            auditorium.Id);
    }

    private async Task<int> CreateShowtimeDirectlyAsync(
     int movieId,
     int auditoriumId,
     DateTimeOffset? startsAt = null)
    {
        await using var scope =
            _factory.Services.CreateAsyncScope();

        var context =
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var scheduledStart =
        startsAt ?? DateTimeOffset.UtcNow.AddDays(20);

        var showtime = new Showtime
        {
            MovieId = movieId,
            AuditoriumId = auditoriumId,
            StartsAt = scheduledStart,
            EndsAt = scheduledStart.AddMinutes(120),
            TicketPrice = 10.00m,
            Status = ShowtimeStatus.Scheduled
        };

        context.Showtimes.Add(showtime);

        await context.SaveChangesAsync();

        return showtime.Id;
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

        return await ReadAccessTokenAsync(
            response);
    }

    private static async Task<string>
        RegisterAndLoginRegularUserAsync(
            HttpClient client)
    {
        var email =
            $"showtime-auth-{Guid.NewGuid():N}@example.com";

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
                "Login response contained an empty access token.");
    }
}