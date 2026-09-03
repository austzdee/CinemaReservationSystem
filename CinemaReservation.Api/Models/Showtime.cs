namespace CinemaReservation.Api.Models;

public class Showtime
{
    public int Id { get; set; }

    public int MovieId { get; set; }

    public int AuditoriumId { get; set; }

    // DateTimeOffset preserves an absolute point in time rather than relying
    // on the local timezone of whichever server executes the application.
    public DateTimeOffset StartsAt { get; set; }

    // Persisting the calculated end time makes auditorium-overlap queries
    // independent of subsequent changes to the movie's duration.
    public DateTimeOffset EndsAt { get; set; }

    public decimal TicketPrice { get; set; }

    public ShowtimeStatus Status { get; set; }
        = ShowtimeStatus.Scheduled;

    public Movie Movie { get; set; } = null!;

    public Auditorium Auditorium { get; set; } = null!;
}