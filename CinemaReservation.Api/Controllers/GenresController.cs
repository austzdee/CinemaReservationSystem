using CinemaReservation.Api.DTOs.Movies;
using CinemaReservation.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaReservation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GenresController : ControllerBase
{
    private readonly IGenreService _genreService;

    public GenresController(IGenreService genreService)
    {
        _genreService = genreService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<GenreResponse>>> GetGenres()
    {
        var genres =
            await _genreService.GetGenresAsync();

        return Ok(genres);
    }
}
