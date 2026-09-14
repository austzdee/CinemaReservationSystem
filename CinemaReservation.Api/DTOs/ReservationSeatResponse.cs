 namespace CinemaReservation.Api.DTOs;

public class ReservationSeatResponse
{
    public int SeatId { get; set; }
    public string Row { get; set; } = string.Empty;
    public int Number { get; set; }
    public decimal UnitPrice { get; set; }
}