using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CinemaReservation.Api.DTOs.Tmdb;

namespace CinemaReservation.Api.Services;

public sealed class TmdbService(
    IHttpClientFactory httpClientFactory) : ITmdbService
{
    private const string ClientName = "Tmdb";
    private const string PosterBaseUrl =
        "https://image.tmdb.org/t/p/w500";

    public async Task<IReadOnlyList<TmdbMovieSearchResult>> SearchMoviesAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var client = httpClientFactory.CreateClient(ClientName);

        var encodedQuery = Uri.EscapeDataString(query.Trim());

        using var response = await client.GetAsync(
            $"search/movie?query={encodedQuery}&include_adult=false&language=en-US&page=1",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var searchResponse =
            await response.Content.ReadFromJsonAsync<TmdbSearchResponse>(
                cancellationToken);

        if (searchResponse is null)
        {
            return [];
        }

        return searchResponse.Results
            .Select(movie => new TmdbMovieSearchResult
            {
                TmdbId = movie.Id,
                Title = movie.Title ?? string.Empty,
                Overview = movie.Overview ?? string.Empty,
                PosterUrl = BuildPosterUrl(movie.PosterPath),
                ReleaseDate = ParseDate(movie.ReleaseDate)
            })
            .ToList();
    }

    public async Task<TmdbMovieDetails?> GetMovieDetailsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        if (tmdbId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tmdbId),
                "TMDB movie ID must be greater than zero.");
        }

        var client = httpClientFactory.CreateClient(ClientName);

        using var response = await client.GetAsync(
            $"movie/{tmdbId}?language=en-US",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var movie =
            await response.Content.ReadFromJsonAsync<TmdbMovieDetailsResponse>(
                cancellationToken);

        if (movie is null)
        {
            return null;
        }

        return new TmdbMovieDetails
        {
            TmdbId = movie.Id,
            Title = movie.Title ?? string.Empty,
            Overview = movie.Overview ?? string.Empty,
            RuntimeMinutes = movie.Runtime,
            PosterUrl = BuildPosterUrl(movie.PosterPath),
            ReleaseDate = ParseDate(movie.ReleaseDate),
            Genres = movie.Genres
                .Select(genre => new TmdbGenreResult
                {
                    TmdbId = genre.Id,
                    Name = genre.Name ?? string.Empty
                })
                .ToList()
        };
    }

    private static string? BuildPosterUrl(string? posterPath)
    {
        if (string.IsNullOrWhiteSpace(posterPath))
        {
            return null;
        }

        return $"{PosterBaseUrl}{posterPath}";
    }

    private static DateOnly? ParseDate(string? value)
    {
        return DateOnly.TryParse(value, out var date)
            ? date
            : null;
    }

    private sealed class TmdbSearchResponse
    {
        [JsonPropertyName("results")]
        public List<TmdbSearchMovie> Results { get; init; } = [];
    }

    private sealed class TmdbSearchMovie
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("overview")]
        public string? Overview { get; init; }

        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; init; }

        [JsonPropertyName("release_date")]
        public string? ReleaseDate { get; init; }
    }

    private sealed class TmdbMovieDetailsResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("overview")]
        public string? Overview { get; init; }

        [JsonPropertyName("runtime")]
        public int? Runtime { get; init; }

        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; init; }

        [JsonPropertyName("release_date")]
        public string? ReleaseDate { get; init; }

        [JsonPropertyName("genres")]
        public List<TmdbGenreResponse> Genres { get; init; } = [];
    }

    private sealed class TmdbGenreResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }
}