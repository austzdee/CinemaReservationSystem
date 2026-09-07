namespace CinemaReservation.Api.DTOs.SeatAvailability;

public class ShowtimeSeatAvailabilityResponse
{
    public int ShowtimeId { get; set; }

    public int AuditoriumId { get; set; }

    public string AuditoriumName { get; set; } = string.Empty;

    public int Capacity { get; set; }

    public int AvailableSeatCount { get; set; }

    public IReadOnlyList<ShowtimeSeatResponse> Seats { get; set; } =
        [];
}