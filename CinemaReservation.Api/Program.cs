using System.Net.Http.Headers;
using System.Text;
using CinemaReservation.Api.Configuration;
using CinemaReservation.Api.Data;
using CinemaReservation.Api.Models;
using CinemaReservation.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;


var builder = WebApplication.CreateBuilder(args);

// Register controller-based API services.
builder.Services.AddControllers();

// Register PostgreSQL through Entity Framework Core.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Register ASP.NET Core Data Protection for Identity token generation.
builder.Services.AddDataProtection();

// Register OpenAPI document generation for development and API tooling.
builder.Services.AddOpenApi();

// Register the genre catalogue operations behind the application service boundary.
builder.Services.AddScoped<IGenreService, GenreService>();

// Register seat availability operations behind the application service boundary.
builder.Services.AddScoped<
    ISeatAvailabilityService,
    SeatAvailabilityService>();

// Register ASP.NET Core Identity for application users and role-based authorization.
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // Require unique email addresses so each account maps to one identity.
        options.User.RequireUniqueEmail = true;

        // Establish a reasonable baseline password policy for user accounts.
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Register movie catalogue operations behind the application service boundary.
builder.Services.AddScoped<IMovieService, MovieService>();

// Register auditorium operations behind the application service boundary.
builder.Services.AddScoped<IAuditoriumService, AuditoriumService>();

// Register seat operations behind the application service boundary.
builder.Services.AddScoped<ISeatService, SeatService>();

// Register the system clock behind TimeProvider so scheduling rules remain
// testable without coupling business logic directly to DateTimeOffset.UtcNow.
builder.Services.AddSingleton(TimeProvider.System);

// Register showtime scheduling operations behind the application service boundary.
builder.Services.AddScoped<IShowtimeService, ShowtimeService>();

// Configure server-side TMDB access without exposing provider credentials to clients.
builder.Services
    .AddOptions<TmdbOptions>()
    .Bind(
        builder.Configuration.GetSection(
            TmdbOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient(
    "Tmdb",
    (serviceProvider, client) =>
    {
        var options =
            serviceProvider
                .GetRequiredService<
                    Microsoft.Extensions.Options.IOptions<TmdbOptions>>()
                .Value;

        client.BaseAddress =
            new Uri(options.BaseUrl);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                options.ReadAccessToken);

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));
    });

// Isolate TMDB API communication behind the application service contract.
builder.Services.AddScoped<ITmdbService, TmdbService>();

// Coordinate TMDB metadata with local movie and genre persistence.
builder.Services.AddScoped<
    ITmdbMovieImportService,
    TmdbMovieImportService>();

// Generate JWT access tokens for authenticated users.
builder.Services.AddScoped<ITokenService, TokenService>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT signing key is not configured.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer is not configured.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience is not configured.");

// Configure bearer-token authentication and strict JWT validation.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)),

            ValidateLifetime = true,

            // Reject tokens as soon as they expire rather than allowing
            // the framework's default clock-skew grace period.
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Expose the OpenAPI document only in development.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure required roles and the initial administrator exist at startup.
await IdentitySeeder.SeedAsync(
    app.Services,
    app.Configuration);

app.Run();

// Expose the generated Program type to the integration test project.
public partial class Program;