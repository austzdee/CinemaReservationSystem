using Microsoft.AspNetCore.Identity;

namespace CinemaReservation.Api.Models;

public class ApplicationUser : IdentityUser
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}