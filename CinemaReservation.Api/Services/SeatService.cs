using CinemaReservation.Api.Data;
using CinemaReservation.Api.DTOs.Auditoriums;
using CinemaReservation.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaReservation.Api.Services;

public class SeatService : ISeatService
{
    private readonly ApplicationDbContext _context;

    public SeatService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SeatResponse>?>
        GetSeatsAsync(int auditoriumId)
    {
        var auditoriumExists =
            await _context.Auditoriums
                .AsNoTracking()
                .AnyAsync(auditorium =>
                    auditorium.Id == auditoriumId);

        if (!auditoriumExists)
        {
            return null;
        }

        return await _context.Seats
            .AsNoTracking()
            .Where(seat =>
                seat.AuditoriumId == auditoriumId)
            .OrderBy(seat => seat.Row)
            .ThenBy(seat => seat.Number)
            .Select(seat => new SeatResponse
            {
                Id = seat.Id,
                AuditoriumId = seat.AuditoriumId,
                Row = seat.Row,
                Number = seat.Number
            })
            .ToListAsync();
    }

    public async Task<SeatResponse?>
        CreateSeatAsync(
            int auditoriumId,
            CreateSeatRequest request)
    {
        var auditoriumExists =
            await _context.Auditoriums
                .AnyAsync(auditorium =>
                    auditorium.Id == auditoriumId);

        if (!auditoriumExists)
        {
            return null;
        }

        // Seat rows are normalized at the application boundary so the
        // physical location "a-1" and "A-1" cannot represent two seats.
        var normalizedRow =
            request.Row.Trim().ToUpperInvariant();

        // The database unique constraint remains the final integrity guard;
        // this check lets the API return a meaningful 409 response first.
        var duplicateExists =
            await _context.Seats.AnyAsync(
                seat =>
                    seat.AuditoriumId == auditoriumId &&
                    seat.Row == normalizedRow &&
                    seat.Number == request.Number);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                "A seat with this row and number already exists in the auditorium.");
        }

        var seat = new Seat
        {
            AuditoriumId = auditoriumId,
            Row = normalizedRow,
            Number = request.Number
        };

        _context.Seats.Add(seat);

        await _context.SaveChangesAsync();

        return new SeatResponse
        {
            Id = seat.Id,
            AuditoriumId = seat.AuditoriumId,
            Row = seat.Row,
            Number = seat.Number
        };
    }

    public async Task<SeatResponse?>
        UpdateSeatAsync(
            int auditoriumId,
            int seatId,
            UpdateSeatRequest request)
    {
        var auditoriumExists =
            await _context.Auditoriums
                .AnyAsync(auditorium =>
                    auditorium.Id == auditoriumId);

        if (!auditoriumExists)
        {
            return null;
        }

        // Query through the parent resource boundary so a seat from another
        // auditorium cannot be modified through this auditorium's route.
        var seat =
            await _context.Seats
                .SingleOrDefaultAsync(
                    seat =>
                        seat.Id == seatId &&
                        seat.AuditoriumId == auditoriumId);

        if (seat is null)
        {
            return null;
        }

        var normalizedRow =
            request.Row.Trim().ToUpperInvariant();

        // Exclude the current seat so retaining its existing physical
        // position does not produce a false duplicate conflict.
        var duplicateExists =
            await _context.Seats.AnyAsync(
                other =>
                    other.Id != seatId &&
                    other.AuditoriumId == auditoriumId &&
                    other.Row == normalizedRow &&
                    other.Number == request.Number);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                "A seat with this row and number already exists in the auditorium.");
        }

        seat.Row = normalizedRow;
        seat.Number = request.Number;

        await _context.SaveChangesAsync();

        return new SeatResponse
        {
            Id = seat.Id,
            AuditoriumId = seat.AuditoriumId,
            Row = seat.Row,
            Number = seat.Number
        };
    }

    public async Task<bool>
        DeleteSeatAsync(
            int auditoriumId,
            int seatId)
    {
        // Including AuditoriumId in the lookup preserves the nested resource
        // boundary and prevents deleting a seat through the wrong auditorium.
        var seat =
            await _context.Seats
                .SingleOrDefaultAsync(
                    seat =>
                        seat.Id == seatId &&
                        seat.AuditoriumId == auditoriumId);

        if (seat is null)
        {
            return false;
        }

        _context.Seats.Remove(seat);

        await _context.SaveChangesAsync();

        return true;
    }
}