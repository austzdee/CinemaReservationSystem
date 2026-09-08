using CinemaReservation.Api.DTOs.Movies;

namespace CinemaReservation.Api.Services;

public enum TmdbMovieImportStatus
{
    Imported,
    NotFound,
    AlreadyImported,
    InvalidMetadata
}

public sealed class TmdbMovieImportResult
{
    public required TmdbMovieImportStatus Status { get; init; }

    public MovieResponse? Movie { get; init; }

    public string? Message { get; init; }
}