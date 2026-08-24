using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using CinemaReservation.Api.Data;
using CinemaReservation.Api.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaReservation.Tests;

// Validates movie-management authorization, creation, and public retrieval
// through the real API pipeline against the isolated PostgreSQL test database.
[Collection("Integration")]
public class MovieManagementIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public MovieManagementIntegrationTests(
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
    public async Task CreateMovie_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/movies",
            CreateAuthorizationTestRequest());

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateMovie_WithUserToken_ReturnsForbidden()
    {
        var email =
            $"movie-user-{Guid.NewGuid():N}@example.com";

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

        var token =
            await LoginAndReadAccessTokenAsync(
                email,
                password);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/movies")
            {
                Content = JsonContent.Create(
                   CreateAuthorizationTestRequest())
            };

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
    public async Task CreateMovie_WithAdminToken_ReturnsCreated()
    {
        var genreId = await CreateGenreAsync();

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/movies")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Title =
                            $"Integration Movie {Guid.NewGuid():N}",
                        Description =
                            "Created through the movie-management integration test.",
                        PosterUrl =
                            "https://example.com/poster.jpg",
                        DurationMinutes = 120,
                        GenreIds = new[]
                        {
                            genreId,
                            genreId
                        }
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

        var movie =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        var movieId =
     movie.GetProperty("id").GetInt32();

        Assert.True(movieId > 0);

        Assert.NotNull(response.Headers.Location);

        Assert.EndsWith(
            $"/api/movies/{movieId}",
            response.Headers.Location.ToString(),
            StringComparison.OrdinalIgnoreCase);

        Assert.True(
            movie.GetProperty("isActive").GetBoolean());

        // Duplicate genre IDs supplied by the client must be normalized
        // rather than creating duplicate MovieGenre relationships.
        Assert.Equal(
            1,
            movie.GetProperty("genres")
                .GetArrayLength());
    }

    [Fact]
    public async Task CreateMovie_WithUnknownGenre_ReturnsBadRequest()
    {
        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/movies")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Title =
                            $"Invalid Genre Movie {Guid.NewGuid():N}",
                        Description =
                            "Uses a genre that does not exist.",
                        PosterUrl =
                            "https://example.com/poster.jpg",
                        DurationMinutes = 110,
                        GenreIds = new[]
                        {
                            int.MaxValue
                        }
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
    public async Task GetMovieById_WithActiveMovie_ReturnsOk()
    {
        int movieId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var genre = new Genre
            {
                Name = $"Drama-{Guid.NewGuid():N}"
            };

            var movie = new Movie
            {
                Title = "Integration Test Movie",
                Description = "Movie used to verify public retrieval.",
                DurationMinutes = 120,
                IsActive = true
            };

            movie.MovieGenres.Add(new MovieGenre
            {
                Movie = movie,
                Genre = genre
            });

            context.Movies.Add(movie);
            await context.SaveChangesAsync();

            movieId = movie.Id;
        }

        var response = await _client.GetAsync($"/api/movies/{movieId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = document.RootElement;

        Assert.Equal(movieId, root.GetProperty("id").GetInt32());
        Assert.Equal(
            "Integration Test Movie",
            root.GetProperty("title").GetString());
        Assert.True(root.GetProperty("isActive").GetBoolean());

        var genres = root.GetProperty("genres");

        Assert.Equal(1, genres.GetArrayLength());
    }

    [Fact]
    public async Task GetMovieById_WithUnknownMovie_ReturnsNotFound()
    {
        var response =
            await _client.GetAsync(
                $"/api/movies/{int.MaxValue}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetMovieById_WithArchivedMovie_ReturnsNotFound()
    {
        int movieId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var movie = new Movie
            {
                Title =
                    $"Archived Movie {Guid.NewGuid():N}",
                Description =
                    "Archived movies must not be publicly accessible.",
                DurationMinutes = 105,
                IsActive = false
            };

            context.Movies.Add(movie);
            await context.SaveChangesAsync();

            movieId = movie.Id;
        }

        var response =
            await _client.GetAsync(
                $"/api/movies/{movieId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    private async Task<int> CreateGenreAsync()
    {
        await using var scope =
            _factory.Services.CreateAsyncScope();

        var context =
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var genre = new Genre
        {
            Name = $"Integration Genre {Guid.NewGuid():N}"
        };

        context.Genres.Add(genre);

        await context.SaveChangesAsync();

        // Test data is created through the application's configured DbContext
        // so it remains isolated inside the integration-test database.
        return genre.Id;
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

        return await LoginAndReadAccessTokenAsync(
            "admin@example.com",
            adminPassword);
    }

    private async Task<string> LoginAndReadAccessTokenAsync(
        string email,
        string password)
    {
        var response =
            await _client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    Email = email,
                    Password = password
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

    private static object CreateAuthorizationTestRequest()
    {
        return new
        {
            Title =
                $"Movie {Guid.NewGuid():N}",
            Description =
                "Movie-management integration test.",
            PosterUrl =
                "https://example.com/poster.jpg",
            DurationMinutes = 100,
            GenreIds = new[]
            {
                int.MaxValue
            }
        };
    }
}