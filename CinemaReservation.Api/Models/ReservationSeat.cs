namespace CinemaReservation.Api.Models;

public class ReservationSeat
{
    public int Id { get; set; }

    public int ReservationId { get; set; }

    public int ShowtimeId { get; set; }

    public int SeatId { get; set; }

    public decimal UnitPrice { get; set; }

    public DateTimeOffset? ReleasedAt { get; set; }

    public Reservation Reservation { get; set; } = null!;

    public Showtime Showtime { get; set; } = null!;

    public Seat Seat { get; set; } = null!;
}