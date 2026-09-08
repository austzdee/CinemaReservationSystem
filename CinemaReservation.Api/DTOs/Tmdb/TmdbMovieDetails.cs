namespace CinemaReservation.Api.DTOs.Tmdb;

public class TmdbMovieDetails
{
    public int TmdbId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }

    public string? PosterUrl { get; set; }

    public DateOnly? ReleaseDate { get; set; }

    public IReadOnlyList<TmdbGenreResult> Genres { get; set; } = [];
}