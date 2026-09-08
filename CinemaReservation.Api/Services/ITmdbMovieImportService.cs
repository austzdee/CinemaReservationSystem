namespace CinemaReservation.Api.Services;

public interface ITmdbMovieImportService
{
    Task<TmdbMovieImportResult> ImportMovieAsync(
        int tmdbId,
        CancellationToken cancellationToken = default);
}