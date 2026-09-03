using CinemaReservation.Api.Data;
using CinemaReservation.Api.DTOs.Showtimes;
using CinemaReservation.Api.Models;
using CinemaReservation.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CinemaReservation.Tests;

// Validates showtime scheduling rules through the application service layer
// against the isolated PostgreSQL integration-test database.
[Collection("Integration")]
public class ShowtimeServiceIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly WebApplicationFactory<Program> _factory;

    public ShowtimeServiceIntegrationTests(
        CustomWebApplicationFactory factory)
    {
        // Replace the system clock so future/past scheduling assertions remain
        // deterministic regardless of when the test suite is executed.
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();

                services.AddSingleton<TimeProvider>(
                    new FixedTimeProvider(FixedUtcNow));
            });
        });
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_CalculatesEndTimeAndNormalisesToUtc()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync(
                movieDurationMinutes: 120);

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var service =
            scope.ServiceProvider
                .GetRequiredService<IShowtimeService>();

        // 15:00 at +01:00 represents the same instant as 14:00 UTC.
        var request = new CreateShowtimeRequest
        {
            MovieId = movieId,
            AuditoriumId = auditoriumId,
            StartsAt = new DateTimeOffset(
                2026,
                9,
                4,
                15,
                0,
                0,
                TimeSpan.FromHours(1)),
            TicketPrice = 12.50m
        };

        var result =
            await service.CreateAsync(request);

        Assert.Equal(
            new DateTimeOffset(
                2026,
                9,
                4,
                14,
                0,
                0,
                TimeSpan.Zero),
            result.StartsAt);

        Assert.Equal(
            new DateTimeOffset(
                2026,
                9,
                4,
                16,
                0,
                0,
                TimeSpan.Zero),
            result.EndsAt);

        Assert.Equal(
            ShowtimeStatus.Scheduled,
            result.Status);
    }

    [Fact]
    public async Task CreateAsync_WithOverlappingShowtime_ThrowsInvalidOperationException()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync(
                movieDurationMinutes: 120);

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var service =
            scope.ServiceProvider
                .GetRequiredService<IShowtimeService>();

        await service.CreateAsync(
            new CreateShowtimeRequest
            {
                MovieId = movieId,
                AuditoriumId = auditoriumId,
                StartsAt = FixedUtcNow.AddDays(1),
                TicketPrice = 10.00m
            });

        // The proposed screening starts before the existing screening ends.
        var overlappingRequest =
            new CreateShowtimeRequest
            {
                MovieId = movieId,
                AuditoriumId = auditoriumId,
                StartsAt = FixedUtcNow
                    .AddDays(1)
                    .AddHours(1),
                TicketPrice = 10.00m
            };

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateAsync(
                    overlappingRequest));

        Assert.Contains(
            "already has a showtime",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_WithBackToBackShowtime_AllowsScheduling()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync(
                movieDurationMinutes: 120);

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var service =
            scope.ServiceProvider
                .GetRequiredService<IShowtimeService>();

        var first =
            await service.CreateAsync(
                new CreateShowtimeRequest
                {
                    MovieId = movieId,
                    AuditoriumId = auditoriumId,
                    StartsAt = FixedUtcNow.AddDays(2),
                    TicketPrice = 11.00m
                });

        var second =
            await service.CreateAsync(
                new CreateShowtimeRequest
                {
                    MovieId = movieId,
                    AuditoriumId = auditoriumId,

                    // Exact boundary contact is valid because the first
                    // screening has already ended at this instant.
                    StartsAt = first.EndsAt,

                    TicketPrice = 11.00m
                });

        Assert.Equal(
            first.EndsAt,
            second.StartsAt);
    }

    [Fact]
    public async Task CreateAsync_AfterCancellingExistingShowtime_AllowsSameInterval()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync(
                movieDurationMinutes: 90);

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var service =
            scope.ServiceProvider
                .GetRequiredService<IShowtimeService>();

        var startsAt =
            FixedUtcNow.AddDays(3);

        var existing =
            await service.CreateAsync(
                new CreateShowtimeRequest
                {
                    MovieId = movieId,
                    AuditoriumId = auditoriumId,
                    StartsAt = startsAt,
                    TicketPrice = 9.50m
                });

        var cancelled =
            await service.CancelAsync(existing.Id);

        Assert.True(cancelled);

        // Cancelled screenings intentionally stop participating in overlap
        // detection while their historical record remains persisted.
        var replacement =
            await service.CreateAsync(
                new CreateShowtimeRequest
                {
                    MovieId = movieId,
                    AuditoriumId = auditoriumId,
                    StartsAt = startsAt,
                    TicketPrice = 9.50m
                });

        Assert.NotEqual(
            existing.Id,
            replacement.Id);
    }

    [Fact]
    public async Task CreateAsync_WithInactiveMovie_ThrowsInvalidOperationException()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync(
                movieDurationMinutes: 100,
                movieIsActive: false);

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var service =
            scope.ServiceProvider
                .GetRequiredService<IShowtimeService>();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateAsync(
                    new CreateShowtimeRequest
                    {
                        MovieId = movieId,
                        AuditoriumId = auditoriumId,
                        StartsAt = FixedUtcNow.AddDays(4),
                        TicketPrice = 8.00m
                    }));

        Assert.Contains(
            "movie does not exist or is inactive",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_WithInactiveAuditorium_ThrowsInvalidOperationException()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync(
                movieDurationMinutes: 100,
                auditoriumIsActive: false);

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var service =
            scope.ServiceProvider
                .GetRequiredService<IShowtimeService>();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateAsync(
                    new CreateShowtimeRequest
                    {
                        MovieId = movieId,
                        AuditoriumId = auditoriumId,
                        StartsAt = FixedUtcNow.AddDays(5),
                        TicketPrice = 8.00m
                    }));

        Assert.Contains(
            "auditorium does not exist or is inactive",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelAsync_WhenAlreadyCancelled_RemainsSuccessful()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync(
                movieDurationMinutes: 90);

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var service =
            scope.ServiceProvider
                .GetRequiredService<IShowtimeService>();

        var showtime =
            await service.CreateAsync(
                new CreateShowtimeRequest
                {
                    MovieId = movieId,
                    AuditoriumId = auditoriumId,
                    StartsAt = FixedUtcNow.AddDays(6),
                    TicketPrice = 10.00m
                });

        var firstCancellation =
            await service.CancelAsync(showtime.Id);

        var secondCancellation =
            await service.CancelAsync(showtime.Id);

        Assert.True(firstCancellation);
        Assert.True(secondCancellation);
    }

    [Fact]
    public async Task CreateAsync_WithPastStartTime_ThrowsInvalidOperationException()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync(
                movieDurationMinutes: 90);

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var service =
            scope.ServiceProvider
                .GetRequiredService<IShowtimeService>();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateAsync(
                    new CreateShowtimeRequest
                    {
                        MovieId = movieId,
                        AuditoriumId = auditoriumId,
                        StartsAt = FixedUtcNow.AddMinutes(-1),
                        TicketPrice = 10.00m
                    }));

        Assert.Contains(
            "future",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAsync_WithUnchangedInterval_DoesNotConflictWithItself()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync(
                movieDurationMinutes: 120);

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var service =
            scope.ServiceProvider
                .GetRequiredService<IShowtimeService>();

        var existing =
            await service.CreateAsync(
                new CreateShowtimeRequest
                {
                    MovieId = movieId,
                    AuditoriumId = auditoriumId,
                    StartsAt = FixedUtcNow.AddDays(7),
                    TicketPrice = 10.00m
                });

        // The overlap query must exclude the record being updated; otherwise an
        // unchanged screening would incorrectly conflict with itself.
        var updated =
            await service.UpdateAsync(
                existing.Id,
                new UpdateShowtimeRequest
                {
                    MovieId = movieId,
                    AuditoriumId = auditoriumId,
                    StartsAt = existing.StartsAt,
                    TicketPrice = 12.00m
                });

        Assert.NotNull(updated);
        Assert.Equal(
            existing.StartsAt,
            updated.StartsAt);
        Assert.Equal(
            12.00m,
            updated.TicketPrice);
    }

    [Fact]
    public async Task UpdateAsync_WithOverlappingShowtime_ThrowsInvalidOperationException()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync(
                movieDurationMinutes: 120);

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var service =
            scope.ServiceProvider
                .GetRequiredService<IShowtimeService>();

        var first =
            await service.CreateAsync(
                new CreateShowtimeRequest
                {
                    MovieId = movieId,
                    AuditoriumId = auditoriumId,
                    StartsAt = FixedUtcNow.AddDays(8),
                    TicketPrice = 10.00m
                });

        var second =
            await service.CreateAsync(
                new CreateShowtimeRequest
                {
                    MovieId = movieId,
                    AuditoriumId = auditoriumId,
                    StartsAt = first.EndsAt,
                    TicketPrice = 10.00m
                });

        // Moving the second screening one hour earlier creates a real overlap
        // with the first screening and must be rejected.
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateAsync(
                    second.Id,
                    new UpdateShowtimeRequest
                    {
                        MovieId = movieId,
                        AuditoriumId = auditoriumId,
                        StartsAt = first.StartsAt.AddHours(1),
                        TicketPrice = 10.00m
                    }));

        Assert.Contains(
            "already has a showtime",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAsync_WithCancelledShowtime_ThrowsInvalidOperationException()
    {
        var (movieId, auditoriumId) =
            await CreateSchedulingDependenciesAsync(
                movieDurationMinutes: 90);

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var service =
            scope.ServiceProvider
                .GetRequiredService<IShowtimeService>();

        var showtime =
            await service.CreateAsync(
                new CreateShowtimeRequest
                {
                    MovieId = movieId,
                    AuditoriumId = auditoriumId,
                    StartsAt = FixedUtcNow.AddDays(9),
                    TicketPrice = 10.00m
                });

        await service.CancelAsync(showtime.Id);

        // Cancellation is terminal in the current domain model; an update must
        // never act as an implicit reactivation mechanism.
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateAsync(
                    showtime.Id,
                    new UpdateShowtimeRequest
                    {
                        MovieId = movieId,
                        AuditoriumId = auditoriumId,
                        StartsAt = FixedUtcNow.AddDays(10),
                        TicketPrice = 10.00m
                    }));

        Assert.Contains(
            "cancelled showtimes cannot be updated",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(int MovieId, int AuditoriumId)>
        CreateSchedulingDependenciesAsync(
            int movieDurationMinutes,
            bool movieIsActive = true,
            bool auditoriumIsActive = true)
    {
        await using var scope =
            _factory.Services.CreateAsyncScope();

        var context =
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var suffix =
            Guid.NewGuid().ToString("N");

        var movie = new Movie
        {
            Title = $"Showtime Test Movie {suffix}",
            Description = "Integration-test movie.",
            DurationMinutes = movieDurationMinutes,
            IsActive = movieIsActive
        };

        var auditorium = new Auditorium
        {
            Name = $"Showtime Screen {suffix}",
            IsActive = auditoriumIsActive
        };

        context.Movies.Add(movie);
        context.Auditoriums.Add(auditorium);

        await context.SaveChangesAsync();

        return (
            movie.Id,
            auditorium.Id);
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}