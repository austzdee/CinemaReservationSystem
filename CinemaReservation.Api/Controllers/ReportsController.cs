using CinemaReservation.Api.DTOs.Reports;
using CinemaReservation.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaReservation.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin")]
public class ReportsController(
    IReportService reportService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ReservationSummaryReportResponse>> GetSummary(
        [FromQuery] ReportQueryParameters query,
        CancellationToken cancellationToken)
    {
        try
        {
            var report =
                await reportService.GetSummaryAsync(
                    query,
                    cancellationToken);

            return Ok(report);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new
            {
                message = exception.Message
            });
        }
    }

    [HttpGet("showtimes")]
    public async Task<ActionResult<IReadOnlyList<ShowtimeReportResponse>>>
        GetShowtimes(
            [FromQuery] ReportQueryParameters query,
            CancellationToken cancellationToken)
    {
        try
        {
            var report =
                await reportService.GetShowtimesAsync(
                    query,
                    cancellationToken);

            return Ok(report);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new
            {
                message = exception.Message
            });
        }
    }

    [HttpGet("movies")]
    public async Task<ActionResult<IReadOnlyList<MovieReportResponse>>>
        GetMovies(
            [FromQuery] ReportQueryParameters query,
            CancellationToken cancellationToken)
    {
        try
        {
            var report =
                await reportService.GetMoviesAsync(
                    query,
                    cancellationToken);

            return Ok(report);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new
            {
                message = exception.Message
            });
        }
    }
}