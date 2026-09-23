namespace CinemaReservation.Api.DTOs.Reports;

public class MovieReportResponse
{
    public int MovieId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int ShowtimeCount { get; set; }

    public int ConfirmedReservations { get; set; }

    public int ReservedSeats { get; set; }

    public decimal Revenue { get; set; }
}