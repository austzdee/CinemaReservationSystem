using CinemaReservation.Api.Models;

namespace CinemaReservation.Api.DTOs.Showtimes;

public class ShowtimeResponse
{
    public int Id { get; set; }

    public int MovieId { get; set; }

    public string MovieTitle { get; set; } = string.Empty;

    public int AuditoriumId { get; set; }

    public string AuditoriumName { get; set; } = string.Empty;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public decimal TicketPrice { get; set; }

    public ShowtimeStatus Status { get; set; }
}