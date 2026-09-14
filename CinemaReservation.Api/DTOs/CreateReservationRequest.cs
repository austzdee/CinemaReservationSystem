namespace CinemaReservation.Api.DTOs;

public class CreateReservationRequest
{
    public int ShowtimeId { get; set; }
    public List<int> SeatIds { get; set; } = [];
}