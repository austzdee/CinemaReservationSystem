using CinemaReservation.Api.Authorization;
using CinemaReservation.Api.DTOs.Showtimes;
using CinemaReservation.Api.Models;
using CinemaReservation.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaReservation.Api.Controllers;

[ApiController]
[Route("api/showtimes")]
public class ShowtimesController(
    IShowtimeService showtimeService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ShowtimeResponse>>> GetShowtimes(
        [FromQuery] DateTimeOffset? startsFrom,
        [FromQuery] DateTimeOffset? startsTo,
        [FromQuery] int? movieId,
        [FromQuery] int? auditoriumId,
        CancellationToken cancellationToken)
    {
        // Query filters are optional and delegated to the service so public
        // read behavior remains consistent across callers.
        var showtimes =
            await showtimeService.GetScheduledAsync(
                startsFrom,
                startsTo,
                movieId,
                auditoriumId,
                cancellationToken);

        return Ok(showtimes);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ShowtimeResponse>> GetShowtimeById(
        int id,
        CancellationToken cancellationToken)
    {
        var showtime =
            await showtimeService.GetScheduledByIdAsync(
                id,
                cancellationToken);

        if (showtime is null)
        {
            return NotFound();
        }

        return Ok(showtime);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<ShowtimeResponse>> CreateShowtime(
        CreateShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var showtime =
                await showtimeService.CreateAsync(
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetShowtimeById),
                new
                {
                    id = showtime.Id
                },
                showtime);
        }
        catch (InvalidOperationException exception)
        {
            // Scheduling-rule violations are client-correctable conflicts or
            // invalid requests rather than infrastructure failures.
            return BadRequest(
                new
                {
                    message = exception.Message
                });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<ShowtimeResponse>> UpdateShowtime(
        int id,
        UpdateShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var showtime =
                await showtimeService.UpdateAsync(
                    id,
                    request,
                    cancellationToken);

            if (showtime is null)
            {
                return NotFound();
            }

            return Ok(showtime);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(
                new
                {
                    message = exception.Message
                });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> CancelShowtime(
        int id,
        CancellationToken cancellationToken)
    {
        var cancelled =
            await showtimeService.CancelAsync(
                id,
                cancellationToken);

        if (!cancelled)
        {
            return NotFound();
        }

        // Cancellation is logical rather than destructive, and repeated
        // requests remain successful because cancellation is idempotent.
        return NoContent();
    }
}