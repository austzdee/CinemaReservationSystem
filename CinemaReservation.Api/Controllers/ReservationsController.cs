using System.Security.Claims;
using CinemaReservation.Api.Authorization;
using CinemaReservation.Api.DTOs;
using CinemaReservation.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaReservation.Api.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
public class ReservationsController(
    IReservationService reservationService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ReservationResponse>> CreateReservation(
        CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        // Reservation ownership comes from the authenticated JWT identity,
        // never from client-supplied request data.
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            var reservation = await reservationService.CreateAsync(
                userId,
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetReservationById),
                new
                {
                    id = reservation.Id
                },
                reservation);
        }
        catch (KeyNotFoundException exception)
        {
            // Missing showtimes or seats are surfaced as resource-not-found errors.
            return NotFound(
                new
                {
                    message = exception.Message
                });
        }
        catch (ReservationConflictException exception)
        {
            // A seat-allocation race or existing active booking is a state
            // conflict rather than a malformed reservation request.
            return Conflict(
                new
                {
                    message = exception.Message
                });
        }
        catch (InvalidOperationException exception)
        {
            // Domain-rule violations such as inactive seats or past showtimes
            // are client-correctable reservation errors.
            return BadRequest(
                new
                {
                    message = exception.Message
                });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReservationResponse>> GetReservationById(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        // Administrators may inspect any reservation, while regular users
        // remain restricted to reservations they own.
        var isAdmin = User.IsInRole(AppRoles.Admin);

        var reservation = await reservationService.GetByIdAsync(
            id,
            userId,
            isAdmin,
            cancellationToken);

        if (reservation is null)
        {
            // Returning 404 avoids disclosing whether a reservation exists
            // when the authenticated user is not authorized to view it.
            return NotFound();
        }

        return Ok(reservation);
    }
}