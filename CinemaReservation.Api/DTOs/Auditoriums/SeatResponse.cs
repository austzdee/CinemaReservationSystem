namespace CinemaReservation.Api.DTOs.Auditoriums;

public class SeatResponse
{
    public int Id { get; set; }

    public int AuditoriumId { get; set; }

    public string Row { get; set; } = string.Empty;

    public int Number { get; set; }
}