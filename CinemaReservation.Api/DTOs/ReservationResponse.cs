using CinemaReservation.Api.Models;

namespace CinemaReservation.Api.DTOs;

public class ReservationResponse
{
    public int Id { get; set; }
    public int ShowtimeId { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public decimal TotalPrice { get; set; }
    public List<ReservationSeatResponse> Seats { get; set; } = [];
}