using CinemaReservation.Api.Data;
using CinemaReservation.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CinemaReservation.Tests;

public class DatabaseModelTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=cinema_reservation_test;Username=test;Password=test")
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public void Seat_HasUniqueIndexForAuditoriumRowAndNumber()
    {
        using var context = CreateContext();

        var seatEntity = context.Model.FindEntityType(typeof(Seat));

        Assert.NotNull(seatEntity);

        // A physical seat must be unique within an auditorium.
        var uniqueSeatIndex = seatEntity
            .GetIndexes()
            .SingleOrDefault(index =>
                index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(
                    [
                        nameof(Seat.AuditoriumId),
                        nameof(Seat.Row),
                        nameof(Seat.Number)
                    ]));

        Assert.NotNull(uniqueSeatIndex);
    }

    [Fact]
public void Movie_HasPositiveDurationCheckConstraint()
{
    using var context = CreateContext();

    // Check constraints are stored in EF Core's full design-time model,
    // rather than the read-optimized runtime model.
    var designTimeModel = context
        .GetService<IDesignTimeModel>()
        .Model;

    var movieEntity = designTimeModel.FindEntityType(typeof(Movie));

    Assert.NotNull(movieEntity);

    // Scheduling depends on a valid runtime, so the database model
    // must reject movies with zero or negative duration.
    var constraint = movieEntity
        .GetCheckConstraints()
        .SingleOrDefault(checkConstraint =>
            checkConstraint.Name == "CK_Movies_DurationMinutes_Positive");

    Assert.NotNull(constraint);
}
}