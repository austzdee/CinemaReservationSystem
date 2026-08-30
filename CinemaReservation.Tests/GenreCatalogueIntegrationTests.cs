using System.Net;
using System.Net.Http.Json;
using CinemaReservation.Api.Data;
using CinemaReservation.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaReservation.Tests;

[Collection("Integration")]
public class GenreCatalogueIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GenreCatalogueIntegrationTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
    }

    [Fact]
    public async Task GetGenres_WithoutAuthentication_ReturnsOk()
    {
        var response =
            await _client.GetAsync("/api/genres");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task GetGenres_ReturnsGenresAlphabetically()
    {
        var suffix = Guid.NewGuid().ToString("N");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            context.Genres.AddRange(
                new Genre
                {
                    Name = $"Zulu-{suffix}"
                },
                new Genre
                {
                    Name = $"Alpha-{suffix}"
                },
                new Genre
                {
                    Name = $"Middle-{suffix}"
                });

            await context.SaveChangesAsync();
        }

        var genres =
            await _client.GetFromJsonAsync<List<GenreResponseDto>>(
                "/api/genres");

        Assert.NotNull(genres);

        var matchingGenres = genres!
            .Where(genre =>
                genre.Name.EndsWith(
                    suffix,
                    StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, matchingGenres.Count);

        Assert.Equal(
            new[]
            {
                $"Alpha-{suffix}",
                $"Middle-{suffix}",
                $"Zulu-{suffix}"
            },
            matchingGenres.Select(genre => genre.Name));
    }

    [Fact]
    public async Task GetGenres_ReturnsIdAndNameProjection()
    {
        int genreId;
        var genreName =
            $"Projection-{Guid.NewGuid():N}";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var genre = new Genre
            {
                Name = genreName
            };

            context.Genres.Add(genre);

            await context.SaveChangesAsync();

            genreId = genre.Id;
        }

        var genres =
            await _client.GetFromJsonAsync<List<GenreResponseDto>>(
                "/api/genres");

        Assert.NotNull(genres);

        var genreResponse =
            Assert.Single(
                genres!,
                genre => genre.Id == genreId);

        Assert.Equal(
            genreName,
            genreResponse.Name);
    }

    private sealed class GenreResponseDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
