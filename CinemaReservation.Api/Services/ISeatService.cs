using CinemaReservation.Api.DTOs.Auditoriums;

namespace CinemaReservation.Api.Services;

public interface ISeatService
{
    Task<IReadOnlyList<SeatResponse>?> GetSeatsAsync(
        int auditoriumId);

    Task<SeatResponse?> CreateSeatAsync(
        int auditoriumId,
        CreateSeatRequest request);

    Task<SeatResponse?> UpdateSeatAsync(
        int auditoriumId,
        int seatId,
        UpdateSeatRequest request);

    Task<bool> DeleteSeatAsync(
        int auditoriumId,
        int seatId);
}