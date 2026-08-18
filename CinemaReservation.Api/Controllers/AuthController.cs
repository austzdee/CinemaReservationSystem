using CinemaReservation.Api.Authorization;
using CinemaReservation.Api.DTOs.Auth;
using CinemaReservation.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CinemaReservation.Api.Services;

namespace CinemaReservation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var existingUser =
            await userManager.FindByEmailAsync(request.Email);

        if (existingUser is not null)
        {
            return Conflict(new
            {
                message = "An account with this email already exists."
            });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var createResult =
            await userManager.CreateAsync(user, request.Password);

        if (!createResult.Succeeded)
        {
            return BadRequest(new
            {
                errors = createResult.Errors
                    .Select(error => error.Description)
            });
        }

        // Public registration always receives the standard User role.
        // Role selection is intentionally not accepted from the request.
        var roleResult =
            await userManager.AddToRoleAsync(user, AppRoles.User);

        if (!roleResult.Succeeded)
        {
            // Avoid leaving a partially-created account if role assignment fails.
            await userManager.DeleteAsync(user);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "Unable to complete user registration."
                });
        }

        return StatusCode(
            StatusCodes.Status201Created,
            new
            {
                user.Id,
                user.Email
            });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        // Use the same response for an unknown account and an incorrect password
        // so the API does not reveal whether a specific email is registered.
        if (user is null ||
            !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var token = await tokenService.CreateAccessTokenAsync(user);

        return Ok(new
        {
            accessToken = token,
            tokenType = "Bearer",
            expiresIn = 3600
        });
    }
}