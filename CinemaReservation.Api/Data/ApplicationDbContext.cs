using CinemaReservation.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CinemaReservation.Api.Data;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Movie> Movies => Set<Movie>();

    public DbSet<Genre> Genres => Set<Genre>();

    public DbSet<MovieGenre> MovieGenres => Set<MovieGenre>();

    public DbSet<Auditorium> Auditoriums => Set<Auditorium>();

    public DbSet<Seat> Seats => Set<Seat>();

    public DbSet<Showtime> Showtimes => Set<Showtime>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<ReservationSeat> ReservationSeats =>
        Set<ReservationSeat>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Keep entity configuration grouped by domain type so the model
        // remains readable as the application grows.
        ConfigureMovie(modelBuilder);
        ConfigureGenre(modelBuilder);
        ConfigureMovieGenre(modelBuilder);
        ConfigureAuditorium(modelBuilder);
        ConfigureSeat(modelBuilder);
        ConfigureShowtime(modelBuilder);
        ConfigureReservation(modelBuilder);
        ConfigureReservationSeat(modelBuilder);
    }

    private static void ConfigureMovie(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movie>(entity =>
        {
            entity.HasKey(movie => movie.Id);

            entity.Property(movie => movie.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(movie => movie.Description)
                .HasMaxLength(2000)
                .IsRequired();

            entity.Property(movie => movie.PosterUrl)
                .HasMaxLength(500);

            // Prevent the same TMDB movie from being imported more than once
            // while allowing manually created movies to remain independent.
            entity.HasIndex(movie => movie.TmdbId)
                .IsUnique()
                .HasFilter("\"TmdbId\" IS NOT NULL");

            // Duration is part of scheduling, so invalid values must also be
            // rejected when data bypasses the API layer.
            entity.ToTable(table =>
                table.HasCheckConstraint(
                    "CK_Movies_DurationMinutes_Positive",
                    "\"DurationMinutes\" > 0"));
        });
    }

    private static void ConfigureGenre(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Genre>(entity =>
        {
            entity.HasKey(genre => genre.Id);

            entity.Property(genre => genre.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(genre => genre.Name)
                .IsUnique();
        });
    }

    private static void ConfigureMovieGenre(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MovieGenre>(entity =>
        {
            // A movie cannot be linked to the same genre more than once.
            entity.HasKey(movieGenre => new
            {
                movieGenre.MovieId,
                movieGenre.GenreId
            });

            entity.HasOne(movieGenre => movieGenre.Movie)
                .WithMany(movie => movie.MovieGenres)
                .HasForeignKey(movieGenre => movieGenre.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(movieGenre => movieGenre.Genre)
                .WithMany(genre => genre.MovieGenres)
                .HasForeignKey(movieGenre => movieGenre.GenreId)
                .OnDelete(DeleteBehavior.Cascade);

            // MovieGenre is only a relationship row and has no independent
            // historical value, so cascade deletion is acceptable here.
        });
    }

    private static void ConfigureAuditorium(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Auditorium>(entity =>
        {
            entity.HasKey(auditorium => auditorium.Id);

            entity.Property(auditorium => auditorium.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(auditorium => auditorium.Name)
                .IsUnique();
        });
    }

    private static void ConfigureSeat(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Seat>(entity =>
        {
            entity.HasKey(seat => seat.Id);

            entity.Property(seat => seat.Row)
                .HasMaxLength(10)
                .IsRequired();

            // A physical seat is uniquely identified within an auditorium
            // by its row and number.
            entity.HasIndex(seat => new
            {
                seat.AuditoriumId,
                seat.Row,
                seat.Number
            })
            .IsUnique();

            entity.ToTable(table =>
                table.HasCheckConstraint(
                    "CK_Seats_Number_Positive",
                    "\"Number\" > 0"));

            entity.HasOne(seat => seat.Auditorium)
                .WithMany(auditorium => auditorium.Seats)
                .HasForeignKey(seat => seat.AuditoriumId)
                .OnDelete(DeleteBehavior.Restrict);

            // Restrict deletion so removing an auditorium cannot silently
            // destroy its operational seat structure.
        });
    }

    private static void ConfigureShowtime(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Showtime>(entity =>
        {
            entity.HasKey(showtime => showtime.Id);

            entity.Property(showtime => showtime.TicketPrice)
                .HasPrecision(10, 2);

            entity.Property(showtime => showtime.Status)
                .HasConversion<int>()
                .IsRequired();

            // Database constraints remain the final integrity boundary when
            // data is written outside the scheduling workflow.
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Showtimes_TicketPrice_Positive",
                    "\"TicketPrice\" > 0");

                table.HasCheckConstraint(
                    "CK_Showtimes_EndsAt_After_StartsAt",
                    "\"EndsAt\" > \"StartsAt\"");
            });

            entity.HasOne(showtime => showtime.Movie)
                .WithMany(movie => movie.Showtimes)
                .HasForeignKey(showtime => showtime.MovieId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(showtime => showtime.Auditorium)
                .WithMany(auditorium => auditorium.Showtimes)
                .HasForeignKey(showtime => showtime.AuditoriumId)
                .OnDelete(DeleteBehavior.Restrict);

            // Used when checking scheduling conflicts in an auditorium.
            entity.HasIndex(showtime => new
            {
                showtime.AuditoriumId,
                showtime.StartsAt
            });

            // Supports movie/date screening queries.
            entity.HasIndex(showtime => new
            {
                showtime.MovieId,
                showtime.StartsAt
            });
        });
    }

    private static void ConfigureReservation(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(reservation => reservation.Id);

            // ReservationSeat carries ShowtimeId so seat uniqueness can be
            // enforced directly. This alternate key also lets the database
            // guarantee that an allocation belongs to the same showtime as
            // its parent reservation.
            entity.HasAlternateKey(reservation => new
            {
                reservation.Id,
                reservation.ShowtimeId
            });

            entity.Property(reservation => reservation.UserId)
                .IsRequired();

            entity.Property(reservation => reservation.Status)
                .HasConversion<int>()
                .IsRequired();

            entity.HasOne(reservation => reservation.User)
                .WithMany(user => user.Reservations)
                .HasForeignKey(reservation => reservation.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(reservation => reservation.Showtime)
                .WithMany(showtime => showtime.Reservations)
                .HasForeignKey(reservation => reservation.ShowtimeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Supports customer reservation-history queries.
            entity.HasIndex(reservation => new
            {
                reservation.UserId,
                reservation.CreatedAt
            });

            // Supports showtime-level reservation and reporting queries.
            entity.HasIndex(reservation => new
            {
                reservation.ShowtimeId,
                reservation.Status
            });
        });
    }

    private static void ConfigureReservationSeat(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReservationSeat>(entity =>
        {
            entity.HasKey(reservationSeat => reservationSeat.Id);

            entity.Property(reservationSeat => reservationSeat.UnitPrice)
                .HasPrecision(10, 2);

            // The composite foreign key guarantees that ReservationSeat's
            // ShowtimeId matches the showtime on its parent Reservation.
            entity.HasOne(reservationSeat => reservationSeat.Reservation)
                .WithMany(reservation => reservation.ReservationSeats)
                .HasForeignKey(reservationSeat => new
                {
                    reservationSeat.ReservationId,
                    reservationSeat.ShowtimeId
                })
                .HasPrincipalKey(reservation => new
                {
                    reservation.Id,
                    reservation.ShowtimeId
                })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(reservationSeat => reservationSeat.Showtime)
                .WithMany(showtime => showtime.ReservationSeats)
                .HasForeignKey(reservationSeat => reservationSeat.ShowtimeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(reservationSeat => reservationSeat.Seat)
                .WithMany(seat => seat.ReservationSeats)
                .HasForeignKey(reservationSeat => reservationSeat.SeatId)
                .OnDelete(DeleteBehavior.Restrict);

            // Only one active allocation may exist for a physical seat at a
            // particular showtime. Released allocations remain as history.
            entity.HasIndex(reservationSeat => new
            {
                reservationSeat.ShowtimeId,
                reservationSeat.SeatId
            })
            .IsUnique()
            .HasFilter("\"ReleasedAt\" IS NULL");

            entity.ToTable(table =>
                table.HasCheckConstraint(
                    "CK_ReservationSeats_UnitPrice_Positive",
                    "\"UnitPrice\" > 0"));
        });
    }
}