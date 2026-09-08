using System.Net;
using System.Net.Http;
using System.Text;
using CinemaReservation.Api.Services;

namespace CinemaReservation.Tests;

public class TmdbServiceTests
{
    [Fact]
    public async Task SearchMoviesAsync_MapsProviderResponse()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            """
            {
              "results": [
                {
                  "id": 157336,
                  "title": "Interstellar",
                  "overview": "A science-fiction epic.",
                  "poster_path": "/poster.jpg",
                  "release_date": "2014-11-05"
                }
              ]
            }
            """);

        var service = CreateService(handler);

        var results =
            await service.SearchMoviesAsync(
                "interstellar");

        Assert.Single(results);

        var movie = results[0];

        Assert.Equal(157336, movie.TmdbId);
        Assert.Equal("Interstellar", movie.Title);
        Assert.Equal(
            "A science-fiction epic.",
            movie.Overview);
        Assert.Equal(
            "https://image.tmdb.org/t/p/w500/poster.jpg",
            movie.PosterUrl);
        Assert.Equal(
            new DateOnly(2014, 11, 5),
            movie.ReleaseDate);

        Assert.NotNull(handler.LastRequest);
        Assert.Contains(
            "search/movie",
            handler.LastRequest.RequestUri?.ToString());
        Assert.Contains(
            "query=interstellar",
            handler.LastRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task GetMovieDetailsAsync_MapsProviderResponse()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            """
            {
              "id": 157336,
              "title": "Interstellar",
              "overview": "A science-fiction epic.",
              "runtime": 169,
              "poster_path": "/poster.jpg",
              "release_date": "2014-11-05",
              "genres": [
                {
                  "id": 18,
                  "name": "Drama"
                },
                {
                  "id": 878,
                  "name": "Science Fiction"
                }
              ]
            }
            """);

        var service = CreateService(handler);

        var movie =
            await service.GetMovieDetailsAsync(
                157336);

        Assert.NotNull(movie);
        Assert.Equal(157336, movie.TmdbId);
        Assert.Equal("Interstellar", movie.Title);
        Assert.Equal(169, movie.RuntimeMinutes);
        Assert.Equal(
            "https://image.tmdb.org/t/p/w500/poster.jpg",
            movie.PosterUrl);
        Assert.Equal(2, movie.Genres.Count);
        Assert.Equal("Drama", movie.Genres[0].Name);
        Assert.Equal(
            "Science Fiction",
            movie.Genres[1].Name);
    }

    [Fact]
    public async Task GetMovieDetailsAsync_WhenProviderReturnsNotFound_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.NotFound,
            string.Empty);

        var service = CreateService(handler);

        var movie =
            await service.GetMovieDetailsAsync(
                999999);

        Assert.Null(movie);
    }

    [Fact]
    public async Task SearchMoviesAsync_WithBlankPosterPath_MapsPosterUrlToNull()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            """
            {
              "results": [
                {
                  "id": 1,
                  "title": "No Poster",
                  "overview": "",
                  "poster_path": null,
                  "release_date": ""
                }
              ]
            }
            """);

        var service = CreateService(handler);

        var results =
            await service.SearchMoviesAsync(
                "no poster");

        Assert.Single(results);
        Assert.Null(results[0].PosterUrl);
        Assert.Null(results[0].ReleaseDate);
    }

    private static TmdbService CreateService(
        StubHttpMessageHandler handler)
    {
        var client =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri(
                        "https://api.themoviedb.org/3/")
            };

        var factory =
            new StubHttpClientFactory(client);

        return new TmdbService(factory);
    }

    private sealed class StubHttpClientFactory(
        HttpClient client)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(
            string name)
        {
            Assert.Equal("Tmdb", name);
            return client;
        }
    }

    private sealed class StubHttpMessageHandler(
        HttpStatusCode statusCode,
        string content)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            var response =
                new HttpResponseMessage(statusCode)
                {
                    Content =
                        new StringContent(
                            content,
                            Encoding.UTF8,
                            "application/json")
                };

            return Task.FromResult(response);
        }
    }
}