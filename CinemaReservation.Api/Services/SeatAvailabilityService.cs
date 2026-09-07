using CinemaReservation.Api.Data;
using CinemaReservation.Api.DTOs.SeatAvailability;
using CinemaReservation.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaReservation.Api.Services;

public class SeatAvailabilityService(
    ApplicationDbContext context) : ISeatAvailabilityService
{
    public async Task<ShowtimeSeatAvailabilityResponse?> GetAvailabilityAsync(
        int showtimeId,
        CancellationToken cancellationToken = default)
    {
        var showtime =
            await context.Showtimes
                .AsNoTracking()
                .Where(showtime =>
                    showtime.Id == showtimeId &&
                    showtime.Status == ShowtimeStatus.Scheduled &&
                    showtime.Movie.IsActive &&
                    showtime.Auditorium.IsActive)
                .Select(showtime => new
                {
                    showtime.Id,
                    showtime.AuditoriumId,
                    AuditoriumName = showtime.Auditorium.Name
                })
                .SingleOrDefaultAsync(cancellationToken);

        if (showtime is null)
        {
            return null;
        }

        var seats =
            await context.Seats
                .AsNoTracking()
                .Where(seat =>
                    seat.AuditoriumId == showtime.AuditoriumId &&
                    seat.IsActive)
                .OrderBy(seat => seat.Row)
                .ThenBy(seat => seat.Number)
                .Select(seat => new ShowtimeSeatResponse
                {
                    Id = seat.Id,
                    Row = seat.Row,
                    Number = seat.Number,

                    // Reservation occupancy is introduced in Phase 8.
                    // Until then, every active seat is available.
                    IsAvailable = true
                })
                .ToListAsync(cancellationToken);

        return new ShowtimeSeatAvailabilityResponse
        {
            ShowtimeId = showtime.Id,
            AuditoriumId = showtime.AuditoriumId,
            AuditoriumName = showtime.AuditoriumName,
            Capacity = seats.Count,
            AvailableSeatCount = seats.Count,
            Seats = seats
        };
    }
}