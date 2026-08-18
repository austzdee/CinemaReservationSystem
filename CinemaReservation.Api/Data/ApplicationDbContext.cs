using CinemaReservation.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;


namespace CinemaReservation.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Movie> Movies => Set<Movie>();

    public DbSet<Genre> Genres => Set<Genre>();

    public DbSet<MovieGenre> MovieGenres => Set<MovieGenre>();

    public DbSet<Auditorium> Auditoriums => Set<Auditorium>();

    public DbSet<Seat> Seats => Set<Seat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Keep entity configuration grouped by domain type so the model remains
        // readable as the application grows.
        ConfigureMovie(modelBuilder);
        ConfigureGenre(modelBuilder);
        ConfigureMovieGenre(modelBuilder);
        ConfigureAuditorium(modelBuilder);
        ConfigureSeat(modelBuilder);
    }

    private static void ConfigureMovie(ModelBuilder modelBuilder)
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

            // Movie duration is part of showtime scheduling, so invalid
            // non-positive values must be rejected at database level.
            // Movie duration is part of showtime scheduling, so invalid
            // non-positive values must be rejected at database level.
            entity.ToTable(table =>
                table.HasCheckConstraint(
                    "CK_Movies_DurationMinutes_Positive",
                    "\"DurationMinutes\" > 0"));
        });
    }

    private static void ConfigureGenre(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Genre>(entity =>
        {
            entity.HasKey(genre => genre.Id);

            entity.Property(genre => genre.Name)
                .HasMaxLength(100)
                .IsRequired();

            // Prevent duplicate genre records such as multiple "Drama" rows.
            entity.HasIndex(genre => genre.Name)
                .IsUnique();
        });
    }

    private static void ConfigureMovieGenre(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MovieGenre>(entity =>
        {
            // The composite key ensures the same movie cannot be assigned
            // to the same genre more than once.
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

            // Cascade deletion is acceptable here because MovieGenre is only
            // a relationship row and has no independent historical value.
        });
    }

    private static void ConfigureAuditorium(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Auditorium>(entity =>
        {
            entity.HasKey(auditorium => auditorium.Id);

            entity.Property(auditorium => auditorium.Name)
                .HasMaxLength(100)
                .IsRequired();

            // Auditorium names are unique within this cinema system.
            entity.HasIndex(auditorium => auditorium.Name)
                .IsUnique();
        });
    }

    private static void ConfigureSeat(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Seat>(entity =>
        {
            entity.HasKey(seat => seat.Id);

            entity.Property(seat => seat.Row)
                .HasMaxLength(10)
                .IsRequired();

            // A physical seat is uniquely identified by its auditorium,
            // row and seat number.
            entity.HasIndex(seat => new
            {
                seat.AuditoriumId,
                seat.Row,
                seat.Number
            })
            .IsUnique();

            // Seat numbering must remain valid even if data is inserted
            // outside the API layer.
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
}