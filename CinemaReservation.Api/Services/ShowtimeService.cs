using CinemaReservation.Api.Data;
using CinemaReservation.Api.DTOs.Showtimes;
using CinemaReservation.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaReservation.Api.Services;

public class ShowtimeService(
    ApplicationDbContext dbContext,
    TimeProvider timeProvider) : IShowtimeService
{
    public async Task<IReadOnlyList<ShowtimeResponse>> GetScheduledAsync(
        DateTimeOffset? startsFrom = null,
        DateTimeOffset? startsTo = null,
        int? movieId = null,
        int? auditoriumId = null,
        CancellationToken cancellationToken = default)
    {
        // Public scheduling reads expose only operational showtimes whose
        // parent movie and auditorium are still active.
        var query = dbContext.Showtimes
            .AsNoTracking()
            .Where(showtime =>
                showtime.Status == ShowtimeStatus.Scheduled &&
                showtime.Movie.IsActive &&
                showtime.Auditorium.IsActive);

        if (startsFrom.HasValue)
        {
            // Normalise externally supplied offsets before comparing them with
            // PostgreSQL timestamp-with-time-zone values.
            var startsFromUtc = startsFrom.Value.ToUniversalTime();

            query = query.Where(showtime =>
                showtime.StartsAt >= startsFromUtc);
        }

        if (startsTo.HasValue)
        {
            var startsToUtc = startsTo.Value.ToUniversalTime();

            // Use an exclusive upper bound so adjacent windows do not return
            // the same screening twice.
            query = query.Where(showtime =>
                showtime.StartsAt < startsToUtc);
        }

        if (movieId.HasValue)
        {
            query = query.Where(showtime =>
                showtime.MovieId == movieId.Value);
        }

        if (auditoriumId.HasValue)
        {
            query = query.Where(showtime =>
                showtime.AuditoriumId == auditoriumId.Value);
        }

        return await query
            .OrderBy(showtime => showtime.StartsAt)
            .Select(showtime => new ShowtimeResponse
            {
                Id = showtime.Id,
                MovieId = showtime.MovieId,
                MovieTitle = showtime.Movie.Title,
                AuditoriumId = showtime.AuditoriumId,
                AuditoriumName = showtime.Auditorium.Name,
                StartsAt = showtime.StartsAt,
                EndsAt = showtime.EndsAt,
                TicketPrice = showtime.TicketPrice,
                Status = showtime.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ShowtimeResponse?> GetScheduledByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        // Cancelled showtimes and showtimes tied to archived operational
        // records are intentionally hidden from the public read contract.
        return await dbContext.Showtimes
            .AsNoTracking()
            .Where(showtime =>
                showtime.Id == id &&
                showtime.Status == ShowtimeStatus.Scheduled &&
                showtime.Movie.IsActive &&
                showtime.Auditorium.IsActive)
            .Select(showtime => new ShowtimeResponse
            {
                Id = showtime.Id,
                MovieId = showtime.MovieId,
                MovieTitle = showtime.Movie.Title,
                AuditoriumId = showtime.AuditoriumId,
                AuditoriumName = showtime.Auditorium.Name,
                StartsAt = showtime.StartsAt,
                EndsAt = showtime.EndsAt,
                TicketPrice = showtime.TicketPrice,
                Status = showtime.Status
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ShowtimeResponse> CreateAsync(
        CreateShowtimeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTicketPrice(request.TicketPrice);

        // Persist scheduling instants consistently in UTC even when clients
        // submit equivalent timestamps using another offset.
        var startsAtUtc = request.StartsAt.ToUniversalTime();

        if (startsAtUtc <= timeProvider.GetUtcNow())
        {
            throw new InvalidOperationException(
                "Showtime must start in the future.");
        }

        // New showtimes may only reference movies that are currently active.
        var movie = await dbContext.Movies
            .SingleOrDefaultAsync(
                movie =>
                    movie.Id == request.MovieId &&
                    movie.IsActive,
                cancellationToken);

        if (movie is null)
        {
            throw new InvalidOperationException(
                "The selected movie does not exist or is inactive.");
        }

        // Historical showtimes may reference an inactive auditorium, but new
        // screenings cannot be scheduled into one.
        var auditorium = await dbContext.Auditoriums
            .SingleOrDefaultAsync(
                auditorium =>
                    auditorium.Id == request.AuditoriumId &&
                    auditorium.IsActive,
                cancellationToken);

        if (auditorium is null)
        {
            throw new InvalidOperationException(
                "The selected auditorium does not exist or is inactive.");
        }

        // Persist EndsAt using the duration at scheduling time so later movie
        // edits do not rewrite the screening's historical interval.
        var endsAtUtc = startsAtUtc.AddMinutes(movie.DurationMinutes);

        await EnsureNoOverlapAsync(
            auditorium.Id,
            startsAtUtc,
            endsAtUtc,
            excludedShowtimeId: null,
            cancellationToken);

        var showtime = new Showtime
        {
            MovieId = movie.Id,
            AuditoriumId = auditorium.Id,
            StartsAt = startsAtUtc,
            EndsAt = endsAtUtc,
            TicketPrice = request.TicketPrice,
            Status = ShowtimeStatus.Scheduled,
            Movie = movie,
            Auditorium = auditorium
        };

        dbContext.Showtimes.Add(showtime);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(showtime);
    }

    public async Task<ShowtimeResponse?> UpdateAsync(
        int id,
        UpdateShowtimeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTicketPrice(request.TicketPrice);

        var showtime = await dbContext.Showtimes
            .Include(showtime => showtime.Movie)
            .Include(showtime => showtime.Auditorium)
            .SingleOrDefaultAsync(
                showtime => showtime.Id == id,
                cancellationToken);

        if (showtime is null)
        {
            return null;
        }

        // Cancellation is terminal in the current scheduling lifecycle; an
        // update must never silently reactivate a cancelled screening.
        if (showtime.Status == ShowtimeStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cancelled showtimes cannot be updated.");
        }

        var now = timeProvider.GetUtcNow();

        // Once a screening has started, its scheduling data becomes operational
        // history and should no longer be rewritten.
        if (showtime.StartsAt <= now)
        {
            throw new InvalidOperationException(
                "Past showtimes cannot be updated.");
        }

        var startsAtUtc = request.StartsAt.ToUniversalTime();

        if (startsAtUtc <= now)
        {
            throw new InvalidOperationException(
                "Updated showtime must start in the future.");
        }

        var movie = await dbContext.Movies
            .SingleOrDefaultAsync(
                movie =>
                    movie.Id == request.MovieId &&
                    movie.IsActive,
                cancellationToken);

        if (movie is null)
        {
            throw new InvalidOperationException(
                "The selected movie does not exist or is inactive.");
        }

        var auditorium = await dbContext.Auditoriums
            .SingleOrDefaultAsync(
                auditorium =>
                    auditorium.Id == request.AuditoriumId &&
                    auditorium.IsActive,
                cancellationToken);

        if (auditorium is null)
        {
            throw new InvalidOperationException(
                "The selected auditorium does not exist or is inactive.");
        }

        // Recalculate the interval because changing either the movie or start
        // time can change the persisted end time.
        var endsAtUtc = startsAtUtc.AddMinutes(movie.DurationMinutes);

        await EnsureNoOverlapAsync(
            auditorium.Id,
            startsAtUtc,
            endsAtUtc,
            showtime.Id,
            cancellationToken);

        showtime.MovieId = movie.Id;
        showtime.AuditoriumId = auditorium.Id;
        showtime.StartsAt = startsAtUtc;
        showtime.EndsAt = endsAtUtc;
        showtime.TicketPrice = request.TicketPrice;

        // Keep navigation properties aligned with the new foreign keys so the
        // response can be returned without another database round-trip.
        showtime.Movie = movie;
        showtime.Auditorium = auditorium;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(showtime);
    }

    public async Task<bool> CancelAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var showtime = await dbContext.Showtimes
            .SingleOrDefaultAsync(
                showtime => showtime.Id == id,
                cancellationToken);

        if (showtime is null)
        {
            return false;
        }

        // Cancellation is idempotent and preserves historical scheduling data.
        if (showtime.Status == ShowtimeStatus.Cancelled)
        {
            return true;
        }

        showtime.Status = ShowtimeStatus.Cancelled;

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task EnsureNoOverlapAsync(
        int auditoriumId,
        DateTimeOffset proposedStartsAt,
        DateTimeOffset proposedEndsAt,
        int? excludedShowtimeId,
        CancellationToken cancellationToken)
    {
        // Two intervals overlap when each begins before the other one ends.
        // Exact boundary contact is allowed, enabling back-to-back screenings.
        var hasOverlap = await dbContext.Showtimes
            .AnyAsync(
                existing =>
                    existing.AuditoriumId == auditoriumId &&
                    existing.Status == ShowtimeStatus.Scheduled &&
                    (!excludedShowtimeId.HasValue ||
                     existing.Id != excludedShowtimeId.Value) &&
                    existing.StartsAt < proposedEndsAt &&
                    existing.EndsAt > proposedStartsAt,
                cancellationToken);

        if (hasOverlap)
        {
            throw new InvalidOperationException(
                "The auditorium already has a showtime during the requested period.");
        }
    }

    private static void ValidateTicketPrice(decimal ticketPrice)
    {
        // Preserve the invariant even when the service is called outside the
        // HTTP model-validation pipeline.
        if (ticketPrice <= 0)
        {
            throw new InvalidOperationException(
                "Ticket price must be greater than zero.");
        }
    }

    private static ShowtimeResponse ToResponse(Showtime showtime)
    {
        return new ShowtimeResponse
        {
            Id = showtime.Id,
            MovieId = showtime.MovieId,
            MovieTitle = showtime.Movie.Title,
            AuditoriumId = showtime.AuditoriumId,
            AuditoriumName = showtime.Auditorium.Name,
            StartsAt = showtime.StartsAt,
            EndsAt = showtime.EndsAt,
            TicketPrice = showtime.TicketPrice,
            Status = showtime.Status
        };
    }
}