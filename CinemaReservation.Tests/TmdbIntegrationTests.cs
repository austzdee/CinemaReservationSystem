using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaReservation.Api.DTOs.Movies;
using CinemaReservation.Api.DTOs.Tmdb;
using CinemaReservation.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CinemaReservation.Tests;


[Collection("Integration")]
public class TmdbIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private static int _tmdbIdSeed =
    Random.Shared.Next(1_000_000, 2_000_000_000);
    private readonly CustomWebApplicationFactory _factory;

    public TmdbIntegrationTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SearchMovies_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(
            "/api/admin/tmdb/movies/search?query=interstellar");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SearchMovies_AsRegularUser_ReturnsForbidden()
    {
        using var client = CreateClient();

        var token = await RegisterAndLoginRegularUserAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(
            "/api/admin/tmdb/movies/search?query=interstellar");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SearchMovies_AsAdmin_ReturnsResults()
    {
        using var client = CreateClient();

        var token = await LoginAdminAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(
            "/api/admin/tmdb/movies/search?query=interstellar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results =
            await response.Content
                .ReadFromJsonAsync<List<TmdbMovieSearchResult>>();

        Assert.NotNull(results);
        Assert.Single(results);
        Assert.Equal(157336, results[0].TmdbId);
        Assert.Equal("Interstellar", results[0].Title);
    }

    [Fact]
    public async Task SearchMovies_WithoutQuery_ReturnsBadRequest()
    {
        using var client = CreateClient();

        var token = await LoginAdminAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(
            "/api/admin/tmdb/movies/search");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportMovie_AsAdmin_CreatesLocalMovie()
    {
        var tmdbId = NextTmdbId();

        var tmdbService =
            new StubTmdbService(
                new TmdbMovieDetails
                {
                    TmdbId = tmdbId,
                    Title = "Imported Test Movie",
                    Overview = "Imported from the TMDB test provider.",
                    RuntimeMinutes = 142,
                    PosterUrl =
                        "https://image.tmdb.org/t/p/w500/test.jpg",
                    ReleaseDate = new DateOnly(2026, 1, 15),
                    Genres =
                    [
                        new()
                    {
                        TmdbId = 878,
                        Name = $"Science Fiction {tmdbId}"
                    },
                    new()
                    {
                        TmdbId = 18,
                        Name = $"Drama {tmdbId}"
                    }
                    ]
                });

        using var client = CreateClient(tmdbService);

        var token = await LoginAdminAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response = await client.PostAsync(
            $"/api/admin/tmdb/movies/{tmdbId}/import",
            null);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var movie =
            await response.Content
                .ReadFromJsonAsync<MovieResponse>();

        Assert.NotNull(movie);
        Assert.Equal("Imported Test Movie", movie.Title);
        Assert.Equal(142, movie.DurationMinutes);
        Assert.Equal(2, movie.Genres.Count);

        Assert.Equal(
            $"/api/movies/{movie.Id}",
            response.Headers.Location?.OriginalString);

        var localMovieResponse =
            await client.GetAsync(
                $"/api/movies/{movie.Id}");

        Assert.Equal(
            HttpStatusCode.OK,
            localMovieResponse.StatusCode);
    }

    [Fact]
    public async Task ImportMovie_WhenAlreadyImported_ReturnsConflict()
    {
        var tmdbId = NextTmdbId();

        var tmdbService =
            new StubTmdbService(
                new TmdbMovieDetails
                {
                    TmdbId = tmdbId,
                    Title = "Duplicate Import Test",
                    Overview = "Duplicate import test.",
                    RuntimeMinutes = 120,
                    Genres = []
                });

        using var client = CreateClient(tmdbService);

        var token = await LoginAdminAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var firstResponse = await client.PostAsync(
            $"/api/admin/tmdb/movies/{tmdbId}/import",
            null);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        var secondResponse = await client.PostAsync(
            $"/api/admin/tmdb/movies/{tmdbId}/import",
            null);

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondResponse.StatusCode);
    }

    [Fact]
    public async Task ImportMovie_WhenTmdbMovieDoesNotExist_ReturnsNotFound()
    {
        var tmdbId = NextTmdbId();

        using var client =
            CreateClient(new StubTmdbService());

        var token = await LoginAdminAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response = await client.PostAsync(
            $"/api/admin/tmdb/movies/{tmdbId}/import",
            null);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task ImportMovie_WithMissingRuntime_ReturnsUnprocessableEntity()
    {
        var tmdbId = NextTmdbId();

        var tmdbService =
            new StubTmdbService(
                new TmdbMovieDetails
                {
                    TmdbId = tmdbId,
                    Title = "Invalid Metadata Test",
                    Overview = "Runtime deliberately omitted.",
                    RuntimeMinutes = null,
                    Genres = []
                });

        using var client = CreateClient(tmdbService);

        var token = await LoginAdminAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response = await client.PostAsync(
            $"/api/admin/tmdb/movies/{tmdbId}/import",
            null);

        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            response.StatusCode);
    }

    [Fact]
    public async Task ImportMovie_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        var response = await client.PostAsync(
            "/api/admin/tmdb/movies/123/import",
            null);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task ImportMovie_AsRegularUser_ReturnsForbidden()
    {
        using var client = CreateClient();

        var token =
            await RegisterAndLoginRegularUserAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response = await client.PostAsync(
            "/api/admin/tmdb/movies/123/import",
            null);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task ImportMovie_WithInvalidTmdbId_ReturnsBadRequest()
    {
        using var client = CreateClient();

        var token = await LoginAdminAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response = await client.PostAsync(
            "/api/admin/tmdb/movies/0/import",
            null);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }
    private static int NextTmdbId()
    {
        return Interlocked.Increment(
            ref _tmdbIdSeed);
    }
    private HttpClient CreateClient(
       ITmdbService? tmdbService = null)
    {
        return _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration(
                    (_, configurationBuilder) =>
                    {
                        // The external TMDB service is replaced during integration
                        // testing, so a non-secret placeholder satisfies startup validation.
                        configurationBuilder.AddInMemoryCollection(
                            new Dictionary<string, string?>
                            {
                                ["Tmdb:ReadAccessToken"] =
                                    "integration-test-token"
                            });
                    });

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<ITmdbService>();

                    services.AddScoped<ITmdbService>(
                        _ => tmdbService ?? new StubTmdbService());
                });
            })
            .CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost")
                });
    }

    private static async Task<string> RegisterAndLoginRegularUserAsync(
        HttpClient client)
    {
        var email =
            $"tmdb-auth-{Guid.NewGuid():N}@example.com";

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
    
    private sealed class StubTmdbService : ITmdbService
    {
        private readonly TmdbMovieDetails? _movieDetails;

        public StubTmdbService(
            TmdbMovieDetails? movieDetails = null)
        {
            _movieDetails = movieDetails;
        }

        public Task<IReadOnlyList<TmdbMovieSearchResult>>
            SearchMoviesAsync(
                string query,
                CancellationToken cancellationToken = default)
        {
            IReadOnlyList<TmdbMovieSearchResult> results =
            [
                new()
            {
                TmdbId = 157336,
                Title = "Interstellar",
                Overview = "Test overview",
                ReleaseDate = new DateOnly(2014, 11, 5)
            }
            ];

            return Task.FromResult(results);
        }

        public Task<TmdbMovieDetails?> GetMovieDetailsAsync(
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            if (_movieDetails is null
                || _movieDetails.TmdbId != tmdbId)
            {
                return Task.FromResult<TmdbMovieDetails?>(null);
            }

            return Task.FromResult<TmdbMovieDetails?>(
                _movieDetails);
        }


    }
}