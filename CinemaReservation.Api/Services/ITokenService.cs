using CinemaReservation.Api.Models;

namespace CinemaReservation.Api.Services;

public interface ITokenService
{
    Task<string> CreateAccessTokenAsync(ApplicationUser user);
}