using System.ComponentModel.DataAnnotations;

namespace CinemaReservation.Api.DTOs.Showtimes;

public class UpdateShowtimeRequest
{
    [Range(1, int.MaxValue)]
    public int MovieId { get; set; }

    [Range(1, int.MaxValue)]
    public int AuditoriumId { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    [Range(typeof(decimal), "0.01", "99999999.99")]
    public decimal TicketPrice { get; set; }
}