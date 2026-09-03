namespace CinemaReservation.Api.Models;

public class Auditorium
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Seat> Seats { get; set; } = [];

    public ICollection<Showtime> Showtimes { get; set; }
    = new List<Showtime>();
}
