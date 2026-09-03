using System.ComponentModel.DataAnnotations;

namespace CinemaReservation.Api.DTOs.Auditoriums;

public class CreateAuditoriumRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
}