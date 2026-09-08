using CinemaReservation.Api.Data;
using CinemaReservation.Api.DTOs.Movies;
using CinemaReservation.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CinemaReservation.Api.Services;

public sealed class TmdbMovieImportService(
    ApplicationDbContext context,
    ITmdbService tmdbService)
    : ITmdbMovieImportService
{
    public async Task<TmdbMovieImportResult> ImportMovieAsync(
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tmdbId);

        var alreadyImported =
            await context.Movies
                .AsNoTracking()
                .AnyAsync(
                    movie => movie.TmdbId == tmdbId,
                    cancellationToken);

        if (alreadyImported)
        {
            return new TmdbMovieImportResult
            {
                Status = TmdbMovieImportStatus.AlreadyImported,
                Message = "This TMDB movie has already been imported."
            };
        }

        var details =
            await tmdbService.GetMovieDetailsAsync(
                tmdbId,
                cancellationToken);

        if (details is null)
        {
            return new TmdbMovieImportResult
            {
                Status = TmdbMovieImportStatus.NotFound,
                Message = "The requested TMDB movie was not found."
            };
        }

        if (string.IsNullOrWhiteSpace(details.Title)
            || details.RuntimeMinutes is null
            || details.RuntimeMinutes <= 0)
        {
            return new TmdbMovieImportResult
            {
                Status = TmdbMovieImportStatus.InvalidMetadata,
                Message =
                    "TMDB did not provide the metadata required to import this movie."
            };
        }

        var genreNames = details.Genres
            .Select(genre => genre.Name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToArray();

        var existingGenres =
            await context.Genres
                .Where(genre => genreNames.Contains(genre.Name))
                .ToListAsync(cancellationToken);

        var existingGenreNames = existingGenres
            .Select(genre => genre.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var genres = existingGenres.ToList();

        foreach (var genreName in genreNames)
        {
            if (existingGenreNames.Contains(genreName))
            {
                continue;
            }

            var genre = new Genre
            {
                Name = genreName
            };

            context.Genres.Add(genre);
            genres.Add(genre);
        }

        genres = genres
            .OrderBy(genre => genre.Name)
            .ToList();

        var movie = new Movie
        {
            TmdbId = details.TmdbId,
            Title = details.Title.Trim(),
            Description = details.Overview.Trim(),
            PosterUrl = details.PosterUrl?.Trim(),
            DurationMinutes = details.RuntimeMinutes.Value,
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

        context.Movies.Add(movie);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_Movies_TmdbId"
            })
        {
            // The database index remains authoritative if two imports for the
            // same TMDB movie pass the application-level check concurrently.
            return new TmdbMovieImportResult
            {
                Status = TmdbMovieImportStatus.AlreadyImported,
                Message = "This TMDB movie has already been imported."
            };
        }

        return new TmdbMovieImportResult
        {
            Status = TmdbMovieImportStatus.Imported,
            Movie = new MovieResponse
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
            }
        };
    }
}