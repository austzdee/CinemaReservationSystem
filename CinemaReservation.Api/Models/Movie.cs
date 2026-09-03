namespace CinemaReservation.Api.Models;

public class Movie
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public required string Description { get; set; }

    public string? PosterUrl { get; set; }

    public int DurationMinutes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<MovieGenre> MovieGenres { get; set; } = [];

    public ICollection<Showtime> Showtimes { get; set; }
    = new List<Showtime>();
}