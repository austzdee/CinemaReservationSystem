using CinemaReservation.Api.DTOs.SeatAvailability;

namespace CinemaReservation.Api.Services;

public interface ISeatAvailabilityService
{
    Task<ShowtimeSeatAvailabilityResponse?> GetAvailabilityAsync(
        int showtimeId,
        CancellationToken cancellationToken = default);
}