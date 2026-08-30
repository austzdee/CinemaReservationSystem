using CinemaReservation.Api.DTOs.Movies;

namespace CinemaReservation.Api.Services;

public interface IGenreService
{
    Task<IReadOnlyList<GenreResponse>> GetGenresAsync();
}
