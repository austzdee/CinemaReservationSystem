namespace CinemaReservation.Api.Models;

public class Seat
{
    public int Id { get; set; }

    public int AuditoriumId { get; set; }

    public Auditorium Auditorium { get; set; } = null!;

    public required string Row { get; set; }

    public int Number { get; set; }

    public bool IsActive { get; set; } = true;
}