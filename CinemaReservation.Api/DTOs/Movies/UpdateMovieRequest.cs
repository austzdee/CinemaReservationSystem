using System.ComponentModel.DataAnnotations;

namespace CinemaReservation.Api.DTOs.Movies;

public class UpdateMovieRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(500)]
    [Url]
    public string? PosterUrl { get; set; }

    [Range(1, int.MaxValue)]
    public int DurationMinutes { get; set; }

    [Required]
    [MinLength(1)]
    public List<int> GenreIds { get; set; } = [];
}