using CinemaReservation.Api.DTOs.Showtimes;

namespace CinemaReservation.Api.Services;

public interface IShowtimeService
{
    Task<IReadOnlyList<ShowtimeResponse>> GetScheduledAsync(
        DateTimeOffset? startsFrom = null,
        DateTimeOffset? startsTo = null,
        int? movieId = null,
        int? auditoriumId = null,
        CancellationToken cancellationToken = default);

    Task<ShowtimeResponse?> GetScheduledByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<ShowtimeResponse> CreateAsync(
        CreateShowtimeRequest request,
        CancellationToken cancellationToken = default);

    Task<ShowtimeResponse?> UpdateAsync(
        int id,
        UpdateShowtimeRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(
        int id,
        CancellationToken cancellationToken = default);
}