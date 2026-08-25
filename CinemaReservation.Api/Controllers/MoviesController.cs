using CinemaReservation.Api.Authorization;
using CinemaReservation.Api.DTOs.Movies;
using CinemaReservation.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaReservation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MoviesController : ControllerBase
{
    private readonly IMovieService _movieService;

    public MoviesController(IMovieService movieService)
    {
        _movieService = movieService;
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<MovieResponse>> CreateMovie(
        CreateMovieRequest request)
    {
        try
        {
            var movie =
                await _movieService.CreateMovieAsync(request);

            return CreatedAtAction(
                nameof(GetMovieById),
                new { id = movie.Id },
                movie);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(
                new
                {
                    message = exception.Message
                });
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<MovieSummaryResponse>>> GetMovies(
                [FromQuery] int? genreId = null,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 20)
    {
        if (genreId is <= 0)
        {
            return BadRequest(
                new
                {
                    message = "Genre ID must be greater than zero."
                });
        }

        if (page <= 0)
        {
            return BadRequest(
                new
                {
                    message = "Page must be greater than zero."
                });
        }

        if (pageSize is <= 0 or > 100)
        {
            return BadRequest(
                new
                {
                    message = "Page size must be between 1 and 100."
                });
        }

        var movies =
            await _movieService.GetMoviesAsync(
                genreId,
                page,
                pageSize);

        return Ok(movies);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<MovieResponse>> GetMovieById(int id)
    {
        var movie = await _movieService.GetMovieByIdAsync(id);

        if (movie is null)
        {
            return NotFound();
        }

        return Ok(movie);
    }
}