namespace CinemaReservation.Api.DTOs.SeatAvailability;

public class ShowtimeSeatResponse
{
    public int Id { get; set; }

    public string Row { get; set; } = string.Empty;

    public int Number { get; set; }

    public bool IsAvailable { get; set; }
}