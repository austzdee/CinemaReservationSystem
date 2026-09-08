using System.ComponentModel.DataAnnotations;

namespace CinemaReservation.Api.Configuration;

public sealed class TmdbOptions
{
    public const string SectionName = "Tmdb";

    [Required]
    public string BaseUrl { get; init; } =
        "https://api.themoviedb.org/3/";

    [Required]
    public string ReadAccessToken { get; init; } =
        string.Empty;
}