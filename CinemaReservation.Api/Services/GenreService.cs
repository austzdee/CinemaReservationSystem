using CinemaReservation.Api.Data;
using CinemaReservation.Api.DTOs.Movies;
using Microsoft.EntityFrameworkCore;

namespace CinemaReservation.Api.Services;

public class GenreService : IGenreService
{
    private readonly ApplicationDbContext _context;

    public GenreService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<GenreResponse>> GetGenresAsync()
    {
        return await _context.Genres
            .AsNoTracking()
            .OrderBy(genre => genre.Name)
            .Select(genre => new GenreResponse
            {
                Id = genre.Id,
                Name = genre.Name
            })
            .ToListAsync();
    }
}
