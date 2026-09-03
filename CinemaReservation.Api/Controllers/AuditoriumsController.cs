using CinemaReservation.Api.Authorization;
using CinemaReservation.Api.DTOs.Auditoriums;
using CinemaReservation.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaReservation.Api.Controllers;

[ApiController]
[Route("api/auditoriums")]
public class AuditoriumsController : ControllerBase
{
    private readonly IAuditoriumService _auditoriumService;

    public AuditoriumsController(
        IAuditoriumService auditoriumService)
    {
        _auditoriumService = auditoriumService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<AuditoriumSummaryResponse>>>
        GetAuditoriums()
    {
        var auditoriums =
            await _auditoriumService.GetAuditoriumsAsync();

        return Ok(auditoriums);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<AuditoriumResponse>>
        GetAuditoriumById(int id)
    {
        var auditorium =
            await _auditoriumService
                .GetAuditoriumByIdAsync(id);

        if (auditorium is null)
        {
            return NotFound();
        }

        return Ok(auditorium);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<AuditoriumResponse>>
        CreateAuditorium(
            CreateAuditoriumRequest request)
    {
        try
        {
            var auditorium =
                await _auditoriumService
                    .CreateAuditoriumAsync(request);

            return CreatedAtAction(
                nameof(GetAuditoriumById),
                new { id = auditorium.Id },
                auditorium);
        }
        // Business conflicts are translated here for now. A centralized
        // ProblemDetails strategy can replace this during API hardening.
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<AuditoriumResponse>>
        UpdateAuditorium(
            int id,
            UpdateAuditoriumRequest request)
    {
        try
        {
            var auditorium =
                await _auditoriumService
                    .UpdateAuditoriumAsync(id, request);

            if (auditorium is null)
            {
                return NotFound();
            }

            return Ok(auditorium);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult>
        DeleteAuditorium(int id)
    {
        try
        {
            var deleted =
                await _auditoriumService
                    .DeleteAuditoriumAsync(id);

            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }
}