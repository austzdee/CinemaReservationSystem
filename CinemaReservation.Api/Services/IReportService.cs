using CinemaReservation.Api.DTOs.Reports;

namespace CinemaReservation.Api.Services;

public interface IReportService
{
    Task<ReservationSummaryReportResponse> GetSummaryAsync(
        ReportQueryParameters query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShowtimeReportResponse>> GetShowtimesAsync(
        ReportQueryParameters query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MovieReportResponse>> GetMoviesAsync(
        ReportQueryParameters query,
        CancellationToken cancellationToken = default);
}