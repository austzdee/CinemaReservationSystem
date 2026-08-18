using CinemaReservation.Api.Authorization;
using CinemaReservation.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CinemaReservation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
 // Restrict all user-management operations in this controller to administrators.
[Authorize(Roles = AppRoles.Admin)]
public class UsersController(
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpPut("{userId}/promote")]
    public async Task<IActionResult> PromoteToAdmin(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        // Keep promotion idempotent so repeating the request does not
        // create duplicate role relationships or unnecessary errors.
        if (await userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            return Ok(new
            {
                message = "User is already an administrator."
            });
        }

        // Role assignment is performed through Identity rather than direct
        // database writes so normalization and Identity rules remain intact.
        var result = await userManager.AddToRoleAsync(
            user,
            AppRoles.Admin);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                errors = result.Errors
                    .Select(error => error.Description)
            });
        }

        return Ok(new
        {
            message = "User promoted to administrator successfully."
        });
    }
}