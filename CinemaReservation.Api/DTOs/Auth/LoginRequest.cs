using System.ComponentModel.DataAnnotations;

namespace CinemaReservation.Api.DTOs.Auth;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    public required string Password { get; set; }
}