using CinemaReservation.Api.Authorization;
using CinemaReservation.Api.DTOs.Movies;
using CinemaReservation.Api.DTOs.Tmdb;
using CinemaReservation.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace CinemaReservation.Api.Controllers;

[ApiController]
[Route("api/admin/tmdb/movies")]
[Authorize(Roles = AppRoles.Admin)]
public class TmdbController(
    ITmdbService tmdbService,
    ITmdbMovieImportService tmdbMovieImportService)
    : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<TmdbMovieSearchResult>>> SearchMovies(
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Search query is required.");
        }

        var results = await tmdbService.SearchMoviesAsync(
            query,
            cancellationToken);

        return Ok(results);
    }

    [HttpPost("{tmdbId:int}/import")]
    public async Task<ActionResult<MovieResponse>> ImportMovie(
        int tmdbId,
        CancellationToken cancellationToken)
    {
        if (tmdbId <= 0)
        {
            return BadRequest("TMDB movie ID must be greater than zero.");
        }

        var result =
            await tmdbMovieImportService.ImportMovieAsync(
                tmdbId,
                cancellationToken);

        return result.Status switch
        {
            TmdbMovieImportStatus.Imported =>
                Created(
                    $"/api/movies/{result.Movie!.Id}",
                    result.Movie),

            TmdbMovieImportStatus.NotFound =>
                NotFound(result.Message),

            TmdbMovieImportStatus.AlreadyImported =>
                Conflict(result.Message),

            TmdbMovieImportStatus.InvalidMetadata =>
                UnprocessableEntity(result.Message),

            _ => throw new InvalidOperationException(
                "Unexpected TMDB import result.")
        };
    }
}