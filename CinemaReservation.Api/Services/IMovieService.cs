using CinemaReservation.Api.DTOs.Movies;

namespace CinemaReservation.Api.Services;

public interface IMovieService
{
    Task<IReadOnlyList<MovieSummaryResponse>> GetMoviesAsync(
        int? genreId,
        int page,
        int pageSize);

    Task<MovieResponse?> GetMovieByIdAsync(int id);

    Task<MovieResponse> CreateMovieAsync(
        CreateMovieRequest request);

    Task<MovieResponse?> UpdateMovieAsync(
        int id,
        UpdateMovieRequest request);

    Task<bool> ArchiveMovieAsync(int id);
}