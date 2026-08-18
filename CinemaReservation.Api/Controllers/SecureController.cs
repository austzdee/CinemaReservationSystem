using CinemaReservation.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using  Microsoft.AspNetCore.Mvc;

namespace CinemaReservation.Api.Controllers;
[ApiController]
[Route ("api/[controller]")]
public class SecureController : ControllerBase
{
    [Authorize]
    [HttpGet ("authenticated")]
    public IActionResult Authenticated()
    {
        return Ok (new
        {
            message = "Authenticated."
        });
        
    }
    [Authorize (Roles = AppRoles.Admin)]
    [HttpGet("admin")]
    public IActionResult AdminOnly()
    {
        return Ok ( new
        {
            message = "You are authorized as an administrator."
        });

        
    }
}