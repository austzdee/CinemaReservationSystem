using CinemaReservation.Api.Data;
using CinemaReservation.Api.DTOs.Auditoriums;
using CinemaReservation.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaReservation.Api.Services;

public class AuditoriumService : IAuditoriumService
{
    private readonly ApplicationDbContext _context;

    public AuditoriumService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AuditoriumSummaryResponse>>
        GetAuditoriumsAsync()
    {
        return await _context.Auditoriums
            .AsNoTracking()
            .OrderBy(auditorium => auditorium.Name)
            .Select(auditorium => new AuditoriumSummaryResponse
            {
                Id = auditorium.Id,
                Name = auditorium.Name
            })
            .ToListAsync();
    }

    public async Task<AuditoriumResponse?>
        GetAuditoriumByIdAsync(int id)
    {
        return await _context.Auditoriums
            .AsNoTracking()
            .Where(auditorium => auditorium.Id == id)
            .Select(auditorium => new AuditoriumResponse
            {
                Id = auditorium.Id,
                Name = auditorium.Name
            })
            .SingleOrDefaultAsync();
    }

    public async Task<AuditoriumResponse>
        CreateAuditoriumAsync(CreateAuditoriumRequest request)
    {
        var normalizedName = request.Name.Trim();

        // Prevent duplicate physical auditorium names before relying on
        // the database uniqueness constraint as the final integrity guard.
        var duplicateExists =
            await _context.Auditoriums.AnyAsync(
                auditorium =>
                    auditorium.Name == normalizedName);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                "An auditorium with this name already exists.");
        }

        var auditorium = new Auditorium
        {
            Name = normalizedName
        };

        _context.Auditoriums.Add(auditorium);

        await _context.SaveChangesAsync();

        return new AuditoriumResponse
        {
            Id = auditorium.Id,
            Name = auditorium.Name
        };
    }

    public async Task<AuditoriumResponse?>
        UpdateAuditoriumAsync(
            int id,
            UpdateAuditoriumRequest request)
    {
        var auditorium =
            await _context.Auditoriums
                .SingleOrDefaultAsync(
                    auditorium => auditorium.Id == id);

        if (auditorium is null)
        {
            return null;
        }

        var normalizedName = request.Name.Trim();

        // Exclude the current auditorium so renaming to its existing name
        // does not produce a false duplicate conflict.
        var duplicateExists =
            await _context.Auditoriums.AnyAsync(
                other =>
                    other.Id != id &&
                    other.Name == normalizedName);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                "An auditorium with this name already exists.");
        }

        auditorium.Name = normalizedName;

        await _context.SaveChangesAsync();

        return new AuditoriumResponse
        {
            Id = auditorium.Id,
            Name = auditorium.Name
        };
    }

    public async Task<bool> DeleteAuditoriumAsync(int id)
    {
        var auditorium =
            await _context.Auditoriums
                .Include(auditorium => auditorium.Seats)
                .SingleOrDefaultAsync(
                    auditorium => auditorium.Id == id);

        if (auditorium is null)
        {
            return false;
        }

        // Seats represent physical layout data. Rejecting deletion avoids
        // silently destroying that layout through a parent delete operation.
        if (auditorium.Seats.Count > 0)
        {
            throw new InvalidOperationException(
                "An auditorium with seats cannot be deleted.");
        }

        _context.Auditoriums.Remove(auditorium);

        await _context.SaveChangesAsync();

        return true;
    }
}