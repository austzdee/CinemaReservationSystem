using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task GetMovies_ReturnsOnlyActiveMovies()
    {
        var activeTitle =
            $"Active Listing Movie {Guid.NewGuid():N}";

        var archivedTitle =
            $"Archived Listing Movie {Guid.NewGuid():N}";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            context.Movies.AddRange(
                new Movie
                {
                    Title = activeTitle,
                    Description = "Active movie used for listing validation.",
                    DurationMinutes = 100,
                    IsActive = true
                },
                new Movie
                {
                    Title = archivedTitle,
                    Description = "Archived movie used for listing validation.",
                    DurationMinutes = 100,
                    IsActive = false
                });

            await context.SaveChangesAsync();
        }

        var response =
            await _client.GetAsync("/api/movies?pageSize=100");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var movies =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        var titles =
            movies.EnumerateArray()
                .Select(movie =>
                    movie.GetProperty("title").GetString())
                .ToArray();

        Assert.Contains(activeTitle, titles);
        Assert.DoesNotContain(archivedTitle, titles);
    }

    [Fact]
    public async Task GetMovies_WithGenreFilter_ReturnsMatchingMovies()
    {
        int genreId;

        var matchingTitle =
            $"Genre Match {Guid.NewGuid():N}";

        var otherTitle =
            $"Genre Other {Guid.NewGuid():N}";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var targetGenre = new Genre
            {
                Name = $"Target Genre {Guid.NewGuid():N}"
            };

            var otherGenre = new Genre
            {
                Name = $"Other Genre {Guid.NewGuid():N}"
            };

            var matchingMovie = new Movie
            {
                Title = matchingTitle,
                Description = "Matches the requested genre.",
                DurationMinutes = 100,
                IsActive = true
            };

            matchingMovie.MovieGenres.Add(
                new MovieGenre
                {
                    Movie = matchingMovie,
                    Genre = targetGenre
                });

            var otherMovie = new Movie
            {
                Title = otherTitle,
                Description = "Does not match the requested genre.",
                DurationMinutes = 100,
                IsActive = true
            };

            otherMovie.MovieGenres.Add(
                new MovieGenre
                {
                    Movie = otherMovie,
                    Genre = otherGenre
                });

            context.Movies.AddRange(
                matchingMovie,
                otherMovie);

            await context.SaveChangesAsync();

            genreId = targetGenre.Id;
        }

        var response =
            await _client.GetAsync(
                $"/api/movies?genreId={genreId}&pageSize=100");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var movies =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        var titles =
            movies.EnumerateArray()
                .Select(movie =>
                    movie.GetProperty("title").GetString())
                .ToArray();

        Assert.Contains(matchingTitle, titles);
        Assert.DoesNotContain(otherTitle, titles);
    }

    [Fact]
    public async Task GetMovies_WithPagination_RespectsPageSize()
    {
        var response =
            await _client.GetAsync(
                "/api/movies?page=1&pageSize=1");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var movies =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.True(
            movies.GetArrayLength() <= 1);
    }

    [Theory]
    [InlineData("/api/movies?page=0")]
    [InlineData("/api/movies?pageSize=0")]
    [InlineData("/api/movies?pageSize=101")]
    [InlineData("/api/movies?genreId=0")]
    public async Task GetMovies_WithInvalidQuery_ReturnsBadRequest(
        string url)
    {
        var response =
            await _client.GetAsync(url);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task GetMovies_WithUnknownGenre_ReturnsEmptyList()
    {
        var response =
            await _client.GetAsync(
                $"/api/movies?genreId={int.MaxValue}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var movies =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            0,
            movies.GetArrayLength());
    }

    [Fact]
    public async Task UpdateMovie_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response =
            await _client.PutAsJsonAsync(
                $"/api/movies/{int.MaxValue}",
                new
                {
                    Title = "Unauthorized Update",
                    Description = "Should not reach the movie service.",
                    PosterUrl = "https://example.com/poster.jpg",
                    DurationMinutes = 120,
                    GenreIds = new[]
                    {
                    int.MaxValue
                    }
                });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateMovie_WithUserToken_ReturnsForbidden()
    {
        var email =
            $"movie-update-user-{Guid.NewGuid():N}@example.com";

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
                HttpMethod.Put,
                $"/api/movies/{int.MaxValue}")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Title = "Forbidden Update",
                        Description = "Regular users cannot update movies.",
                        PosterUrl = "https://example.com/poster.jpg",
                        DurationMinutes = 120,
                        GenreIds = new[]
                        {
                        int.MaxValue
                        }
                    })
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
    public async Task UpdateMovie_WithAdminToken_UpdatesMovieAndReconcilesGenres()
    {
        int movieId;
        int retainedGenreId;
        int removedGenreId;
        int addedGenreId;
        DateTimeOffset createdAt;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var retainedGenre = new Genre
            {
                Name = $"Retained Genre {Guid.NewGuid():N}"
            };

            var removedGenre = new Genre
            {
                Name = $"Removed Genre {Guid.NewGuid():N}"
            };

            var addedGenre = new Genre
            {
                Name = $"Added Genre {Guid.NewGuid():N}"
            };

            var movie = new Movie
            {
                Title = $"Original Movie {Guid.NewGuid():N}",
                Description = "Original movie description.",
                PosterUrl = "https://example.com/original.jpg",
                DurationMinutes = 100,
                IsActive = true
            };

            movie.MovieGenres.Add(
                new MovieGenre
                {
                    Movie = movie,
                    Genre = retainedGenre
                });

            movie.MovieGenres.Add(
                new MovieGenre
                {
                    Movie = movie,
                    Genre = removedGenre
                });

            context.Movies.Add(movie);
            context.Genres.Add(addedGenre);

            await context.SaveChangesAsync();

            movieId = movie.Id;
            retainedGenreId = retainedGenre.Id;
            removedGenreId = removedGenre.Id;
            addedGenreId = addedGenre.Id;
            createdAt = movie.CreatedAt;
        }

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/movies/{movieId}")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Title = "  Updated Movie Title  ",
                        Description = "  Updated description.  ",
                        PosterUrl = "https://example.com/updated.jpg",
                        DurationMinutes = 135,
                        GenreIds = new[]
                        {
                        retainedGenreId,
                        addedGenreId,
                        addedGenreId
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
            HttpStatusCode.OK,
            response.StatusCode);

        var updatedMovie =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Updated Movie Title",
            updatedMovie.GetProperty("title").GetString());

        Assert.Equal(
            "Updated description.",
            updatedMovie.GetProperty("description").GetString());

        Assert.Equal(
            "https://example.com/updated.jpg",
            updatedMovie.GetProperty("posterUrl").GetString());

        Assert.Equal(
            135,
            updatedMovie.GetProperty("durationMinutes").GetInt32());

        Assert.True(
            updatedMovie.GetProperty("isActive").GetBoolean());

        Assert.Equal(
            createdAt,
            updatedMovie.GetProperty("createdAt").GetDateTimeOffset());

        var genres =
            updatedMovie.GetProperty("genres");

        Assert.Equal(
            2,
            genres.GetArrayLength());

        var returnedGenreIds =
            genres.EnumerateArray()
                .Select(genre =>
                    genre.GetProperty("id").GetInt32())
                .ToArray();

        Assert.Contains(retainedGenreId, returnedGenreIds);
        Assert.Contains(addedGenreId, returnedGenreIds);
        Assert.DoesNotContain(removedGenreId, returnedGenreIds);

        await using var verificationScope =
            _factory.Services.CreateAsyncScope();

        var verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var persistedMovie =
            await verificationContext.Movies
                .AsNoTracking()
                .Include(movie => movie.MovieGenres)
                .SingleAsync(movie => movie.Id == movieId);

        Assert.Equal(
            2,
            persistedMovie.MovieGenres.Count);

        Assert.Contains(
            persistedMovie.MovieGenres,
            movieGenre =>
                movieGenre.GenreId == retainedGenreId);

        Assert.Contains(
            persistedMovie.MovieGenres,
            movieGenre =>
                movieGenre.GenreId == addedGenreId);

        Assert.DoesNotContain(
            persistedMovie.MovieGenres,
            movieGenre =>
                movieGenre.GenreId == removedGenreId);

        Assert.Equal(
            createdAt,
            persistedMovie.CreatedAt);

        Assert.True(
            persistedMovie.UpdatedAt > createdAt);
    }

    [Fact]
    public async Task UpdateMovie_WithUnknownMovie_ReturnsNotFound()
    {
        var genreId =
            await CreateGenreAsync();

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/movies/{int.MaxValue}")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Title = "Unknown Movie",
                        Description = "Movie does not exist.",
                        PosterUrl = "https://example.com/poster.jpg",
                        DurationMinutes = 120,
                        GenreIds = new[]
                        {
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
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateMovie_WithArchivedMovie_ReturnsNotFound()
    {
        int movieId;
        int genreId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var genre = new Genre
            {
                Name = $"Archived Update Genre {Guid.NewGuid():N}"
            };

            var movie = new Movie
            {
                Title = $"Archived Update Movie {Guid.NewGuid():N}",
                Description = "Archived movies cannot be updated.",
                DurationMinutes = 100,
                IsActive = false
            };

            context.Genres.Add(genre);
            context.Movies.Add(movie);

            await context.SaveChangesAsync();

            movieId = movie.Id;
            genreId = genre.Id;
        }

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/movies/{movieId}")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Title = "Attempted Update",
                        Description = "This update must be rejected.",
                        PosterUrl = "https://example.com/poster.jpg",
                        DurationMinutes = 120,
                        GenreIds = new[]
                        {
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
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateMovie_WithUnknownGenre_ReturnsBadRequest()
    {
        int movieId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var movie = new Movie
            {
                Title = $"Invalid Genre Update {Guid.NewGuid():N}",
                Description = "Original description.",
                DurationMinutes = 100,
                IsActive = true
            };

            context.Movies.Add(movie);

            await context.SaveChangesAsync();

            movieId = movie.Id;
        }

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/movies/{movieId}")
            {
                Content = JsonContent.Create(
                    new
                    {
                        Title = "Invalid Genre Update",
                        Description = "Should not be persisted.",
                        PosterUrl = "https://example.com/poster.jpg",
                        DurationMinutes = 120,
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
    public async Task ArchiveMovie_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response =
            await _client.DeleteAsync(
                $"/api/movies/{int.MaxValue}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task ArchiveMovie_WithUserToken_ReturnsForbidden()
    {
        var email =
            $"movie-archive-user-{Guid.NewGuid():N}@example.com";

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
                HttpMethod.Delete,
                $"/api/movies/{int.MaxValue}");

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
    public async Task ArchiveMovie_WithAdminToken_ArchivesMovie()
    {
        int movieId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var movie = new Movie
            {
                Title = $"Archive Movie {Guid.NewGuid():N}",
                Description = "Movie used to verify archiving.",
                DurationMinutes = 100,
                IsActive = true
            };

            context.Movies.Add(movie);

            await context.SaveChangesAsync();

            movieId = movie.Id;
        }

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/movies/{movieId}");

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

        var persistedMovie =
            await verificationContext.Movies
                .AsNoTracking()
                .SingleAsync(movie => movie.Id == movieId);

        Assert.False(persistedMovie.IsActive);
    }

    [Fact]
    public async Task ArchiveMovie_WhenAlreadyArchived_ReturnsNoContent()
    {
        int movieId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var movie = new Movie
            {
                Title = $"Already Archived Movie {Guid.NewGuid():N}",
                Description = "Movie is already archived.",
                DurationMinutes = 100,
                IsActive = false
            };

            context.Movies.Add(movie);

            await context.SaveChangesAsync();

            movieId = movie.Id;
        }

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/movies/{movieId}");

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
    public async Task ArchiveMovie_WithUnknownMovie_ReturnsNotFound()
    {
        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/movies/{int.MaxValue}");

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
    public async Task ArchiveMovie_RemovesMovieFromPublicCatalogueButPreservesRecord()
    {
        int movieId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var movie = new Movie
            {
                Title = $"Catalogue Archive Movie {Guid.NewGuid():N}",
                Description = "Movie used to verify public catalogue behavior.",
                DurationMinutes = 100,
                IsActive = true
            };

            context.Movies.Add(movie);

            await context.SaveChangesAsync();

            movieId = movie.Id;
        }

        var adminToken =
            await GetAdminAccessTokenAsync();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/movies/{movieId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var archiveResponse =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NoContent,
            archiveResponse.StatusCode);

        var publicResponse =
            await _client.GetAsync(
                $"/api/movies/{movieId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            publicResponse.StatusCode);

        await using var verificationScope =
            _factory.Services.CreateAsyncScope();

        var verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var persistedMovie =
            await verificationContext.Movies
                .AsNoTracking()
                .SingleAsync(movie => movie.Id == movieId);

        Assert.False(persistedMovie.IsActive);
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