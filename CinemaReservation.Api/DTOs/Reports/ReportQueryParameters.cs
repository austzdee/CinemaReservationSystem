namespace CinemaReservation.Api.DTOs.Reports;

public class ReportQueryParameters
{
    public DateTimeOffset From { get; set; }

    public DateTimeOffset To { get; set; }

    public int? MovieId { get; set; }

    public int? ShowtimeId { get; set; }

    public int? AuditoriumId { get; set; }
}
