namespace CinemaReservation.Api.DTOs.Reports;

public class ReservationSummaryReportResponse
{
    public DateTimeOffset From { get; set; }

    public DateTimeOffset To { get; set; }

    public int TotalReservations { get; set; }

    public int ConfirmedReservations { get; set; }

    public int CancelledReservations { get; set; }

    public int ActiveSeatsReserved { get; set; }

    public int TotalCapacity { get; set; }

    public decimal OccupancyPercentage { get; set; }

    public decimal Revenue { get; set; }
}