namespace CinemaReservation.Api.DTOs.Movies;

public class MovieResponse
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public required string Description { get; set; }

    public string? PosterUrl { get; set; }

    public int DurationMinutes { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public List<GenreResponse> Genres { get; set; } = [];
}