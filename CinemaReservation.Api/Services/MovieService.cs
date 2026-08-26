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

    public async Task<IReadOnlyList<MovieSummaryResponse>> GetMoviesAsync(
            int? genreId,
            int page,
            int pageSize)
    {
        var query = _context.Movies
            .AsNoTracking()
            .Where(movie => movie.IsActive);

        if (genreId.HasValue)
        {
            query = query.Where(
                movie => movie.MovieGenres.Any(
                    movieGenre => movieGenre.GenreId == genreId.Value));
        }

        return await query
            .OrderBy(movie => movie.Title)
            .ThenBy(movie => movie.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(movie => new MovieSummaryResponse
            {
                Id = movie.Id,
                Title = movie.Title,
                PosterUrl = movie.PosterUrl,
                DurationMinutes = movie.DurationMinutes,
                Genres = movie.MovieGenres
                    .OrderBy(movieGenre => movieGenre.Genre.Name)
                    .Select(movieGenre => new GenreResponse
                    {
                        Id = movieGenre.GenreId,
                        Name = movieGenre.Genre.Name
                    })
                    .ToList()
            })
            .ToListAsync();
    }

    public async Task<MovieResponse?> GetMovieByIdAsync(int id)
    {
        return await _context.Movies
            .AsNoTracking()
            .Where(movie => movie.Id == id && movie.IsActive)
            .Select(movie => new MovieResponse
            {
                Id = movie.Id,
                Title = movie.Title,
                Description = movie.Description,
                PosterUrl = movie.PosterUrl,
                DurationMinutes = movie.DurationMinutes,
                IsActive = movie.IsActive,
                CreatedAt = movie.CreatedAt,
                UpdatedAt = movie.UpdatedAt,
                Genres = movie.MovieGenres
                    .OrderBy(movieGenre => movieGenre.Genre.Name)
                    .Select(movieGenre => new GenreResponse
                    {
                        Id = movieGenre.Genre.Id,
                        Name = movieGenre.Genre.Name
                    })
                    .ToList()
            })
            .SingleOrDefaultAsync();
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

    public async Task<MovieResponse?> UpdateMovieAsync(
    int id,
    UpdateMovieRequest request)
    {
        var movie = await _context.Movies
            .Include(movie => movie.MovieGenres)
            .SingleOrDefaultAsync(
                movie => movie.Id == id && movie.IsActive);

        if (movie is null)
        {
            return null;
        }

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

        movie.Title = request.Title.Trim();
        movie.Description = request.Description.Trim();
        movie.PosterUrl = request.PosterUrl?.Trim();
        movie.DurationMinutes = request.DurationMinutes;
        movie.UpdatedAt = DateTime.UtcNow;
        // Reconcile the relationship set so retained genres keep their tracked.
        // join entities while removed and newly assigned genres are changed explicitly.
        var requestedGenreIds = genreIds.ToHashSet();

        var removedMovieGenres = movie.MovieGenres
            .Where(movieGenre =>
                !requestedGenreIds.Contains(movieGenre.GenreId))
            .ToList();

        foreach (var movieGenre in removedMovieGenres)
        {
            movie.MovieGenres.Remove(movieGenre);
        }

        var existingGenreIds = movie.MovieGenres
            .Select(movieGenre => movieGenre.GenreId)
            .ToHashSet();

        foreach (var genre in genres)
        {
            if (existingGenreIds.Contains(genre.Id))
            {
                continue;
            }

            movie.MovieGenres.Add(
                new MovieGenre
                {
                    Movie = movie,
                    Genre = genre
                });
        }

        await _context.SaveChangesAsync();

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

  public async Task<bool> ArchiveMovieAsync(int id)
{
    var movie =
        await _context.Movies
            .SingleOrDefaultAsync(movie => movie.Id == id);

    if (movie is null)
    {
        return false;
    }

    if (!movie.IsActive)
    {
        return true;
    }

    // Archiving preserves the movie record for historical relationships
    // while removing it from the active catalogue.
    movie.IsActive = false;
    movie.UpdatedAt = DateTimeOffset.UtcNow;

    await _context.SaveChangesAsync();

    return true;
}
}