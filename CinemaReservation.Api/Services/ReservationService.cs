using CinemaReservation.Api.Data;
using CinemaReservation.Api.DTOs;
using CinemaReservation.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CinemaReservation.Api.Services;
public class ReservationService(
    ApplicationDbContext context,
    TimeProvider timeProvider) : IReservationService
{
    public async Task<ReservationResponse> CreateAsync(
        string userId,
        CreateReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "Authenticated user id is required.",
                nameof(userId));
        }

        if (request.SeatIds is null ||
            request.SeatIds.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one seat must be selected.");
        }

        if (request.SeatIds.Count != request.SeatIds.Distinct().Count())
        {
            throw new InvalidOperationException(
                "Duplicate seat selections are not allowed.");
        }

        var now = timeProvider.GetUtcNow();

        var showtime = await context.Showtimes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                showtime => showtime.Id == request.ShowtimeId,
                cancellationToken);


        if (showtime is null)
        {
            throw new KeyNotFoundException("Showtime was not found.");
        }

        if (showtime.Status != ShowtimeStatus.Scheduled)
        {
            throw new InvalidOperationException(
                "Reservations can only be created for scheduled showtimes.");
        }

        if (showtime.StartsAt <= now)
        {
            throw new InvalidOperationException(
                "Reservations can only be created for upcoming showtimes.");
        }

        var seats = await context.Seats
            .AsNoTracking()
            .Where(seat => request.SeatIds.Contains(seat.Id))
            .ToListAsync(cancellationToken);

        if (seats.Count != request.SeatIds.Count)
        {
            throw new KeyNotFoundException(
                "One or more selected seats were not found.");
        }

        if (seats.Any(seat => !seat.IsActive))
        {
            throw new InvalidOperationException(
                "Inactive seats cannot be reserved.");
        }

        if (seats.Any(seat =>
            seat.AuditoriumId != showtime.AuditoriumId))
        {
            throw new InvalidOperationException(
                "All selected seats must belong to the showtime auditorium.");
        }

        var reservation = new Reservation
        {
            UserId = userId,
            ShowtimeId = showtime.Id,
            Status = ReservationStatus.Confirmed,
            CreatedAt = now,
            ReservationSeats = seats
                .Select(seat => new ReservationSeat
                {
                    ShowtimeId = showtime.Id,
                    SeatId = seat.Id,
                    UnitPrice = showtime.TicketPrice
                })
                .ToList()
        };

        context.Reservations.Add(reservation);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgresException &&
                  postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
                  postgresException.ConstraintName ==
                      "IX_ReservationSeats_ShowtimeId_SeatId")
        {
            throw new ReservationConflictException(
             "One or more selected seats are no longer available.",
             exception);
        }

        var seatLookup = seats.ToDictionary(seat => seat.Id);

        return new ReservationResponse
        {
            Id = reservation.Id,
            ShowtimeId = reservation.ShowtimeId,
            Status = reservation.Status,
            CreatedAt = reservation.CreatedAt,
            CancelledAt = reservation.CancelledAt,
            TotalPrice = reservation.ReservationSeats
                .Sum(reservationSeat => reservationSeat.UnitPrice),
            Seats = reservation.ReservationSeats
                .Select(reservationSeat =>
                {
                    var seat = seatLookup[reservationSeat.SeatId];

                    return new ReservationSeatResponse
                    {
                        SeatId = seat.Id,
                        Row = seat.Row,
                        Number = seat.Number,
                        UnitPrice = reservationSeat.UnitPrice
                    };
                })
                .OrderBy(seat => seat.Row)
                .ThenBy(seat => seat.Number)
                .ToList()
        };
    }

    public async Task<ReservationResponse?> GetByIdAsync(
        int reservationId,
        string userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var reservation = await context.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.Id == reservationId &&
                (isAdmin || reservation.UserId == userId))
            .Select(reservation => new ReservationResponse
            {
                Id = reservation.Id,
                ShowtimeId = reservation.ShowtimeId,
                Status = reservation.Status,
                CreatedAt = reservation.CreatedAt,
                CancelledAt = reservation.CancelledAt,
                TotalPrice = reservation.ReservationSeats
                    .Sum(reservationSeat => reservationSeat.UnitPrice),
                Seats = reservation.ReservationSeats
                    .OrderBy(reservationSeat => reservationSeat.Seat.Row)
                    .ThenBy(reservationSeat => reservationSeat.Seat.Number)
                    .Select(reservationSeat => new ReservationSeatResponse
                    {
                        SeatId = reservationSeat.SeatId,
                        Row = reservationSeat.Seat.Row,
                        Number = reservationSeat.Seat.Number,
                        UnitPrice = reservationSeat.UnitPrice
                    })
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return reservation;
    }
}