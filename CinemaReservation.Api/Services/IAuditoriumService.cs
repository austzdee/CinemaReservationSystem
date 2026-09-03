using CinemaReservation.Api.DTOs.Auditoriums;

namespace CinemaReservation.Api.Services;

public interface IAuditoriumService
{
    Task<IReadOnlyList<AuditoriumSummaryResponse>> GetAuditoriumsAsync();

    Task<AuditoriumResponse?> GetAuditoriumByIdAsync(int id);

    Task<AuditoriumResponse> CreateAuditoriumAsync(
        CreateAuditoriumRequest request);

    Task<AuditoriumResponse?> UpdateAuditoriumAsync(
        int id,
        UpdateAuditoriumRequest request);

    Task<bool> DeleteAuditoriumAsync(int id);
}