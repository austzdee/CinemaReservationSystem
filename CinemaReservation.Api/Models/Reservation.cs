namespace CinemaReservation.Api.Models;

public class Reservation
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int ShowtimeId { get; set; }

    public ReservationStatus Status { get; set; } =
        ReservationStatus.Confirmed;

    public DateTimeOffset CreatedAt { get; set; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset? CancelledAt { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Showtime Showtime { get; set; } = null!;

    public ICollection<ReservationSeat> ReservationSeats { get; set; } =
        [];
}