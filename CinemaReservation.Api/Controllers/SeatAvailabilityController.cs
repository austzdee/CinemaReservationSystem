using CinemaReservation.Api.DTOs.SeatAvailability;
using CinemaReservation.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaReservation.Api.Controllers;

[ApiController]
[Route("api/showtimes/{showtimeId:int}/seats")]
public class SeatAvailabilityController(
    ISeatAvailabilityService seatAvailabilityService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ShowtimeSeatAvailabilityResponse>>
        GetSeatAvailability(
            int showtimeId,
            CancellationToken cancellationToken)
    {
        var availability =
            await seatAvailabilityService.GetAvailabilityAsync(
                showtimeId,
                cancellationToken);

        if (availability is null)
        {
            return NotFound();
        }

        return Ok(availability);
    }
}