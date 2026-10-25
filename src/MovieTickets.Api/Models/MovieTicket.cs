namespace MovieTickets.Api.Models;

/// <summary>
/// A movie ticket booking. The customer's email address is the primary key,
/// so each customer has at most one booking.
/// </summary>
public sealed class MovieTicket
{
    public required string CustomerEmail { get; init; }
    public required string CustomerName { get; set; }
    public required string MovieTitle { get; set; }
    public required string TheaterName { get; set; }
    public DateTimeOffset ShowTime { get; set; }
    public int NumberOfTickets { get; set; }
    public List<string> SeatNumbers { get; set; } = new();
    public decimal TicketPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
