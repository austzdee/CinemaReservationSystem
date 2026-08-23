namespace CinemaReservation.Api.DTOs.Movies;

public class MovieSummaryResponse
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public string? PosterUrl { get; set; }

    public int DurationMinutes { get; set; }

    public List<GenreResponse> Genres { get; set; } = [];
}