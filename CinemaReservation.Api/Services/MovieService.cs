using CinemaReservation.Api.Data;
using CinemaReservation.Api.DTOs.Movies;
using CinemaReservation.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaReservation.Api.Services;

public class MovieService : IMovieService
{
    private readonly ApplicationDbContext _context;

    public MovieService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<IReadOnlyList<MovieSummaryResponse>> GetMoviesAsync(
        int? genreId,
        int page,
        int pageSize)
    {
        // Catalogue queries will enforce active-only visibility and optional
        // genre filtering before returning lightweight movie summaries.
        throw new NotImplementedException();
    }

    public Task<MovieResponse?> GetMovieByIdAsync(int id)
    {
        // Public movie retrieval will exclude archived records and return
        // genre data as part of the API response model.
        throw new NotImplementedException();
    }

    public async Task<MovieResponse> CreateMovieAsync(
        CreateMovieRequest request)
    {
        var genreIds = request.GenreIds
            .Distinct()
            .ToArray();

        var genres = await _context.Genres
            .Where(genre => genreIds.Contains(genre.Id))
            .OrderBy(genre => genre.Name)
            .ToListAsync();

        if (genres.Count != genreIds.Length)
        {
            throw new ArgumentException(
                "One or more supplied genre IDs are invalid.");
        }

        var movie = new Movie
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            PosterUrl = request.PosterUrl?.Trim(),
            DurationMinutes = request.DurationMinutes,
            IsActive = true
        };

        foreach (var genre in genres)
        {
            movie.MovieGenres.Add(
                new MovieGenre
                {
                    Movie = movie,
                    Genre = genre
                });
        }

        _context.Movies.Add(movie);

        await _context.SaveChangesAsync();

        // Return the persisted representation so generated IDs, timestamps,
        // and normalized genre relationships are reflected in the response.
        return new MovieResponse
        {
            Id = movie.Id,
            Title = movie.Title,
            Description = movie.Description,
            PosterUrl = movie.PosterUrl,
            DurationMinutes = movie.DurationMinutes,
            IsActive = movie.IsActive,
            CreatedAt = movie.CreatedAt,
            UpdatedAt = movie.UpdatedAt,
            Genres = genres
                .Select(genre => new GenreResponse
                {
                    Id = genre.Id,
                    Name = genre.Name
                })
                .ToList()
        };
    }

    public Task<MovieResponse?> UpdateMovieAsync(
        int id,
        UpdateMovieRequest request)
    {
        // Updates will replace the current genre assignments so the persisted
        // relationships reflect the request rather than accumulating entries.
        throw new NotImplementedException();
    }

    public Task<bool> ArchiveMovieAsync(int id)
    {
        // Archiving preserves the movie record for future historical
        // relationships while removing it from the active catalogue.
        throw new NotImplementedException();
    }
}