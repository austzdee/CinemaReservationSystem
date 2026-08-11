using CinemaReservation.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register controller-based API services.
builder.Services.AddControllers();

// Register PostgreSQL through Entity Framework Core.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Register OpenAPI document generation for development and API tooling.
builder.Services.AddOpenApi();

var app = builder.Build();

// Expose the OpenAPI document only in development.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();