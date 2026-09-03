using CinemaReservation.Api.Authorization;
using CinemaReservation.Api.DTOs.Auditoriums;
using CinemaReservation.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaReservation.Api.Controllers;

[ApiController]
[Route("api/auditoriums/{auditoriumId:int}/seats")]
public class SeatsController : ControllerBase
{
    private readonly ISeatService _seatService;

    public SeatsController(ISeatService seatService)
    {
        _seatService = seatService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<SeatResponse>>>
        GetSeats(int auditoriumId)
    {
        var seats =
            await _seatService.GetSeatsAsync(
                auditoriumId);

        if (seats is null)
        {
            return NotFound();
        }

        return Ok(seats);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<SeatResponse>>
        CreateSeat(
            int auditoriumId,
            CreateSeatRequest request)
    {
        try
        {
            var seat =
                await _seatService.CreateSeatAsync(
                    auditoriumId,
                    request);

            if (seat is null)
            {
                return NotFound();
            }

            return CreatedAtAction(
                nameof(GetSeats),
                new { auditoriumId },
                seat);
        }
        // Business conflicts are translated locally for now; centralized
        // ProblemDetails handling remains a later API-hardening concern.
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    [HttpPut("{seatId:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<SeatResponse>>
        UpdateSeat(
            int auditoriumId,
            int seatId,
            UpdateSeatRequest request)
    {
        try
        {
            var seat =
                await _seatService.UpdateSeatAsync(
                    auditoriumId,
                    seatId,
                    request);

            if (seat is null)
            {
                return NotFound();
            }

            return Ok(seat);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    [HttpDelete("{seatId:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult>
        DeleteSeat(
            int auditoriumId,
            int seatId)
    {
        var deleted =
            await _seatService.DeleteSeatAsync(
                auditoriumId,
                seatId);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}