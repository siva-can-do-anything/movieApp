using System.ComponentModel.DataAnnotations;

namespace MovieTickets.Api.Models;

/// <summary>Fields shared by create and update requests.</summary>
public abstract class MovieTicketDetails : IValidatableObject
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string MovieTitle { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string TheaterName { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset? ShowTime { get; set; }

    [Range(1, 20)]
    public int NumberOfTickets { get; set; }

    public List<string> SeatNumbers { get; set; } = new();

    [Range(typeof(decimal), "0.01", "1000")]
    public decimal TicketPrice { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SeatNumbers.Count > 0 && SeatNumbers.Count != NumberOfTickets)
        {
            yield return new ValidationResult(
                "When seat numbers are given, there must be one per ticket.",
                new[] { nameof(SeatNumbers) });
        }

        if (SeatNumbers.Any(string.IsNullOrWhiteSpace))
        {
            yield return new ValidationResult(
                "Seat numbers cannot be blank.", new[] { nameof(SeatNumbers) });
        }

        if (SeatNumbers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != SeatNumbers.Count)
        {
            yield return new ValidationResult(
                "Seat numbers must be unique.", new[] { nameof(SeatNumbers) });
        }
    }
}

/// <summary>Body of POST /api/movietickets.</summary>
public sealed class CreateMovieTicketRequest : MovieTicketDetails
{
    [Required, EmailAddress, StringLength(254)]
    public string CustomerEmail { get; set; } = string.Empty;
}

/// <summary>Body of PUT /api/movietickets/{email}. Replaces the whole booking.</summary>
public sealed class UpdateMovieTicketRequest : MovieTicketDetails
{
}
