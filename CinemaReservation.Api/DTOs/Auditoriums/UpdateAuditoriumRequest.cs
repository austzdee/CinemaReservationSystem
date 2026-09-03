using System.ComponentModel.DataAnnotations;

namespace CinemaReservation.Api.DTOs.Auditoriums;

public class UpdateAuditoriumRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
}