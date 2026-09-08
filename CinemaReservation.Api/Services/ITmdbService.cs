using CinemaReservation.Api.DTOs.Tmdb;

namespace CinemaReservation.Api.Services;

public interface ITmdbService
{
    Task<IReadOnlyList<TmdbMovieSearchResult>> SearchMoviesAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<TmdbMovieDetails?> GetMovieDetailsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default);
}