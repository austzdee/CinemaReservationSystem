namespace CinemaReservation.Api.DTOs.Reports;

public class ShowtimeReportResponse
{
    public int ShowtimeId { get; set; }

    public int MovieId { get; set; }

    public string MovieTitle { get; set; } = string.Empty;

    public int AuditoriumId { get; set; }

    public string AuditoriumName { get; set; } = string.Empty;

    public DateTimeOffset StartsAt { get; set; }

    public int Capacity { get; set; }

    public int ReservedSeats { get; set; }

    public int AvailableSeats { get; set; }

    public decimal OccupancyPercentage { get; set; }

    public decimal Revenue { get; set; }
}