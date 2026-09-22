using CinemaReservation.Api.DTOs;

namespace CinemaReservation.Api.Services;

public interface IReservationService
{
    Task<ReservationResponse> CreateAsync(
        string userId,
        CreateReservationRequest request,
        CancellationToken cancellationToken = default);

    Task<ReservationResponse?> GetByIdAsync(
        int reservationId,
        string userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<ReservationResponse> CancelAsync(
        int reservationId,
        string userId,
        CancellationToken cancellationToken = default);
}