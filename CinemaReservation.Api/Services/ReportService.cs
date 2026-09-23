using CinemaReservation.Api.Data;
using CinemaReservation.Api.DTOs.Reports;
using CinemaReservation.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaReservation.Api.Services;

public class ReportService(
    ApplicationDbContext context) : IReportService
{
    public async Task<ReservationSummaryReportResponse> GetSummaryAsync(
        ReportQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        if (query.From >= query.To)
        {
            throw new InvalidOperationException(
                "'From' must be earlier than 'To'.");
        }

        // Reports are scoped by showtime start time so all aggregates use the
        // same operational date range rather than reservation creation time.
        var showtimesQuery =
            context.Showtimes
                .AsNoTracking()
                .Where(showtime =>
                    showtime.StartsAt >= query.From &&
                    showtime.StartsAt < query.To);

        if (query.MovieId.HasValue)
        {
            showtimesQuery =
                showtimesQuery.Where(showtime =>
                    showtime.MovieId == query.MovieId.Value);
        }

        if (query.ShowtimeId.HasValue)
        {
            showtimesQuery =
                showtimesQuery.Where(showtime =>
                    showtime.Id == query.ShowtimeId.Value);
        }

        if (query.AuditoriumId.HasValue)
        {
            showtimesQuery =
                showtimesQuery.Where(showtime =>
                    showtime.AuditoriumId ==
                    query.AuditoriumId.Value);
        }

        var showtimeIds =
            showtimesQuery.Select(showtime => showtime.Id);

        var reservationsQuery =
            context.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    showtimeIds.Contains(
                        reservation.ShowtimeId));

        var totalReservations =
            await reservationsQuery.CountAsync(
                cancellationToken);

        var confirmedReservations =
            await reservationsQuery.CountAsync(
                reservation =>
                    reservation.Status ==
                    ReservationStatus.Confirmed,
                cancellationToken);

        var cancelledReservations =
            await reservationsQuery.CountAsync(
                reservation =>
                    reservation.Status ==
                    ReservationStatus.Cancelled,
                cancellationToken);

        var activeSeatsReserved =
            await context.ReservationSeats
                .AsNoTracking()
                .Where(reservationSeat =>
                    showtimeIds.Contains(
                        reservationSeat.ShowtimeId) &&
                    reservationSeat.ReleasedAt == null)
                .CountAsync(cancellationToken);

        // Revenue uses the immutable historical reservation-seat price rather
        // than the current showtime price, and excludes released allocations.
        var revenue =
            await context.ReservationSeats
                .AsNoTracking()
                .Where(reservationSeat =>
                    showtimeIds.Contains(
                        reservationSeat.ShowtimeId) &&
                    reservationSeat.ReleasedAt == null)
                .SumAsync(
                    reservationSeat =>
                        (decimal?)reservationSeat.UnitPrice,
                    cancellationToken)
                ?? 0m;

        // Capacity is measured per performance. If an auditorium with 100
        // active seats has three showtimes, it contributes 300 seat opportunities.
        var totalCapacity =
            await showtimesQuery
                .Select(showtime =>
                    context.Seats.Count(seat =>
                        seat.AuditoriumId ==
                            showtime.AuditoriumId &&
                        seat.IsActive))
                .SumAsync(cancellationToken);

        var occupancyPercentage =
            totalCapacity == 0
                ? 0m
                : Math.Round(
                    activeSeatsReserved * 100m /
                    totalCapacity,
                    2);

        return new ReservationSummaryReportResponse
        {
            From = query.From,
            To = query.To,
            TotalReservations = totalReservations,
            ConfirmedReservations = confirmedReservations,
            CancelledReservations = cancelledReservations,
            ActiveSeatsReserved = activeSeatsReserved,
            TotalCapacity = totalCapacity,
            OccupancyPercentage = occupancyPercentage,
            Revenue = revenue
        };
    }

    public async Task<IReadOnlyList<ShowtimeReportResponse>> GetShowtimesAsync(
         ReportQueryParameters query,
         CancellationToken cancellationToken = default)
    {
        if (query.From >= query.To)
        {
            throw new InvalidOperationException(
                "'From' must be earlier than 'To'.");
        }

        var showtimesQuery =
            context.Showtimes
                .AsNoTracking()
                .Where(showtime =>
                    showtime.StartsAt >= query.From &&
                    showtime.StartsAt < query.To);

        if (query.MovieId.HasValue)
        {
            showtimesQuery =
                showtimesQuery.Where(showtime =>
                    showtime.MovieId == query.MovieId.Value);
        }

        if (query.ShowtimeId.HasValue)
        {
            showtimesQuery =
                showtimesQuery.Where(showtime =>
                    showtime.Id == query.ShowtimeId.Value);
        }

        if (query.AuditoriumId.HasValue)
        {
            showtimesQuery =
                showtimesQuery.Where(showtime =>
                    showtime.AuditoriumId ==
                    query.AuditoriumId.Value);
        }

        var rows =
            await showtimesQuery
                .OrderBy(showtime => showtime.StartsAt)
                .Select(showtime => new
                {
                    ShowtimeId = showtime.Id,
                    showtime.MovieId,
                    MovieTitle = showtime.Movie.Title,
                    showtime.AuditoriumId,
                    AuditoriumName = showtime.Auditorium.Name,
                    showtime.StartsAt,

                    Capacity =
                        context.Seats.Count(seat =>
                            seat.AuditoriumId ==
                                showtime.AuditoriumId &&
                            seat.IsActive),

                    ReservedSeats =
                        context.ReservationSeats.Count(
                            reservationSeat =>
                                reservationSeat.ShowtimeId ==
                                    showtime.Id &&
                                reservationSeat.ReleasedAt == null),

                    Revenue =
                        context.ReservationSeats
                            .Where(reservationSeat =>
                                reservationSeat.ShowtimeId ==
                                    showtime.Id &&
                                reservationSeat.ReleasedAt == null)
                            .Sum(reservationSeat =>
                                (decimal?)reservationSeat.UnitPrice)
                        ?? 0m
                })
                .ToListAsync(cancellationToken);

        return rows
            .Select(row =>
            {
                var availableSeats =
                    Math.Max(
                        row.Capacity - row.ReservedSeats,
                        0);

                // Occupancy is based on currently active seat allocations for the
                // performance, using active auditorium seats as its capacity.
                var occupancyPercentage =
                    row.Capacity == 0
                        ? 0m
                        : Math.Round(
                            row.ReservedSeats * 100m /
                            row.Capacity,
                            2);

                return new ShowtimeReportResponse
                {
                    ShowtimeId = row.ShowtimeId,
                    MovieId = row.MovieId,
                    MovieTitle = row.MovieTitle,
                    AuditoriumId = row.AuditoriumId,
                    AuditoriumName = row.AuditoriumName,
                    StartsAt = row.StartsAt,
                    Capacity = row.Capacity,
                    ReservedSeats = row.ReservedSeats,
                    AvailableSeats = availableSeats,
                    OccupancyPercentage = occupancyPercentage,
                    Revenue = row.Revenue
                };
            })
            .ToList();
    }

    public async Task<IReadOnlyList<MovieReportResponse>> GetMoviesAsync(
      ReportQueryParameters query,
      CancellationToken cancellationToken = default)
    {
        if (query.From >= query.To)
        {
            throw new InvalidOperationException(
                "'From' must be earlier than 'To'.");
        }

        var showtimesQuery =
            context.Showtimes
                .AsNoTracking()
                .Where(showtime =>
                    showtime.StartsAt >= query.From &&
                    showtime.StartsAt < query.To);

        if (query.MovieId.HasValue)
        {
            showtimesQuery =
                showtimesQuery.Where(showtime =>
                    showtime.MovieId == query.MovieId.Value);
        }

        if (query.ShowtimeId.HasValue)
        {
            showtimesQuery =
                showtimesQuery.Where(showtime =>
                    showtime.Id == query.ShowtimeId.Value);
        }

        if (query.AuditoriumId.HasValue)
        {
            showtimesQuery =
                showtimesQuery.Where(showtime =>
                    showtime.AuditoriumId ==
                    query.AuditoriumId.Value);
        }

        var showtimeIds =
            showtimesQuery.Select(showtime => showtime.Id);

        var rows =
            await showtimesQuery
                .GroupBy(showtime => new
                {
                    showtime.MovieId,
                    showtime.Movie.Title
                })
                .Select(group => new
                {
                    MovieId = group.Key.MovieId,
                    Title = group.Key.Title,

                    ShowtimeCount =
                        group.Count(),

                    ConfirmedReservations =
                        context.Reservations.Count(
                            reservation =>
                                showtimeIds.Contains(
                                    reservation.ShowtimeId) &&
                                reservation.Showtime.MovieId ==
                                    group.Key.MovieId &&
                                reservation.Status ==
                                    ReservationStatus.Confirmed),

                    ReservedSeats =
                        context.ReservationSeats.Count(
                            reservationSeat =>
                                showtimeIds.Contains(
                                    reservationSeat.ShowtimeId) &&
                                reservationSeat.Showtime.MovieId ==
                                    group.Key.MovieId &&
                                reservationSeat.ReleasedAt == null),

                    Revenue =
                        context.ReservationSeats
                            .Where(reservationSeat =>
                                showtimeIds.Contains(
                                    reservationSeat.ShowtimeId) &&
                                reservationSeat.Showtime.MovieId ==
                                    group.Key.MovieId &&
                                reservationSeat.ReleasedAt == null)
                            .Sum(reservationSeat =>
                                (decimal?)reservationSeat.UnitPrice)
                        ?? 0m
                })
                .OrderBy(row => row.Title)
                .ToListAsync(cancellationToken);

        // Movie reporting aggregates only showtimes inside the requested range;
        // reservations outside that performance window do not contribute.
        return rows
            .Select(row => new MovieReportResponse
            {
                MovieId = row.MovieId,
                Title = row.Title,
                ShowtimeCount = row.ShowtimeCount,
                ConfirmedReservations = row.ConfirmedReservations,
                ReservedSeats = row.ReservedSeats,
                Revenue = row.Revenue
            })
            .ToList();
    }
}