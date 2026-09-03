using System.ComponentModel.DataAnnotations;

namespace CinemaReservation.Api.DTOs.Auditoriums;

public class CreateSeatRequest
{
    [Required]
    [StringLength(10)]
    public string Row { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Number { get; set; }
}