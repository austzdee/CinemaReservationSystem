var builder = WebApplication.CreateBuilder(args);

// Register controller-based API services.
builder.Services.AddControllers();

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