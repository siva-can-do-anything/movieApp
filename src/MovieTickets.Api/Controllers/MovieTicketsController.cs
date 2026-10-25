using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MovieTickets.Api.Models;
using MovieTickets.Api.Storage;

namespace MovieTickets.Api.Controllers;

/// <summary>Add, read, update and delete movie tickets. The customer email is the key.</summary>
[ApiController]
[Route("api/movietickets")]
[Produces("application/json")]
public sealed class MovieTicketsController : ControllerBase
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    private readonly IMovieTicketRepository _repository;
    private readonly TimeProvider _clock;

    public MovieTicketsController(IMovieTicketRepository repository, TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <summary>List all movie tickets.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MovieTicket>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MovieTicket>>> GetAll(CancellationToken ct)
    {
        return Ok(await _repository.GetAllAsync(ct));
    }

    /// <summary>Get the movie ticket for one customer email.</summary>
    [HttpGet("{email}", Name = nameof(GetByEmail))]
    [ProducesResponseType<MovieTicket>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovieTicket>> GetByEmail(string email, CancellationToken ct)
    {
        if (InvalidEmail(email) is { } bad) return bad;

        var ticket = await _repository.GetAsync(email, ct);
        return ticket is null ? NotFoundFor(email) : Ok(ticket);
    }

    /// <summary>Add a movie ticket. Returns 409 if this email already has one.</summary>
    [HttpPost]
    [ProducesResponseType<MovieTicket>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovieTicket>> Create(CreateMovieTicketRequest request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var ticket = new MovieTicket
        {
            CustomerEmail = JsonFileMovieTicketRepository.NormalizeEmail(request.CustomerEmail),
            CustomerName = request.CustomerName.Trim(),
            MovieTitle = request.MovieTitle.Trim(),
            TheaterName = request.TheaterName.Trim(),
            ShowTime = request.ShowTime!.Value,
            NumberOfTickets = request.NumberOfTickets,
            SeatNumbers = request.SeatNumbers.Select(s => s.Trim()).ToList(),
            TicketPrice = request.TicketPrice,
            TotalAmount = request.TicketPrice * request.NumberOfTickets,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        if (!await _repository.AddAsync(ticket, ct))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Ticket already exists",
                detail: $"A movie ticket for '{ticket.CustomerEmail}' already exists. Use PUT to change it.");
        }

        return CreatedAtRoute(nameof(GetByEmail), new { email = ticket.CustomerEmail }, ticket);
    }

    /// <summary>Replace the movie ticket for one customer email.</summary>
    [HttpPut("{email}")]
    [ProducesResponseType<MovieTicket>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovieTicket>> Update(string email, UpdateMovieTicketRequest request, CancellationToken ct)
    {
        if (InvalidEmail(email) is { } bad) return bad;

        var existing = await _repository.GetAsync(email, ct);
        if (existing is null) return NotFoundFor(email);

        var ticket = new MovieTicket
        {
            CustomerEmail = existing.CustomerEmail,
            CustomerName = request.CustomerName.Trim(),
            MovieTitle = request.MovieTitle.Trim(),
            TheaterName = request.TheaterName.Trim(),
            ShowTime = request.ShowTime!.Value,
            NumberOfTickets = request.NumberOfTickets,
            SeatNumbers = request.SeatNumbers.Select(s => s.Trim()).ToList(),
            TicketPrice = request.TicketPrice,
            TotalAmount = request.TicketPrice * request.NumberOfTickets,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = _clock.GetUtcNow(),
        };

        // It could have been deleted between the read and the write.
        return await _repository.UpdateAsync(ticket, ct) ? Ok(ticket) : NotFoundFor(email);
    }

    /// <summary>Delete the movie ticket for one customer email.</summary>
    [HttpDelete("{email}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string email, CancellationToken ct)
    {
        if (InvalidEmail(email) is { } bad) return bad;

        return await _repository.DeleteAsync(email, ct) ? NoContent() : NotFoundFor(email);
    }

    private ActionResult? InvalidEmail(string email)
    {
        if (!string.IsNullOrWhiteSpace(email) && email.Length <= 254 && EmailValidator.IsValid(email))
        {
            return null;
        }

        ModelState.AddModelError(nameof(email), "A valid customer email address is required.");
        return ValidationProblem(ModelState);
    }

    private ObjectResult NotFoundFor(string email) => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Ticket not found",
        detail: $"No movie ticket found for '{JsonFileMovieTicketRepository.NormalizeEmail(email)}'.");
}
