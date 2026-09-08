namespace CinemaReservation.Api.DTOs.Tmdb;

public class TmdbMovieSearchResult
{
    public int TmdbId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string? PosterUrl { get; set; }

    public DateOnly? ReleaseDate { get; set; }
}