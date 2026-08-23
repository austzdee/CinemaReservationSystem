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

    // The full public retrieval behaviour is implemented in Step 4.5.
    // This route exists now so POST can return a valid Location header.
    [HttpGet("{id:int}")]
    public ActionResult GetMovieById(int id)
    {
        return StatusCode(
            StatusCodes.Status501NotImplemented);
    }
}