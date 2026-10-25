using MovieTickets.Api.Models;

namespace MovieTickets.Api.Storage;

public interface IMovieTicketRepository
{
    Task<IReadOnlyList<MovieTicket>> GetAllAsync(CancellationToken ct = default);
    Task<MovieTicket?> GetAsync(string customerEmail, CancellationToken ct = default);

    /// <summary>Returns false if a ticket for this email already exists.</summary>
    Task<bool> AddAsync(MovieTicket ticket, CancellationToken ct = default);

    /// <summary>Returns false if no ticket exists for this email.</summary>
    Task<bool> UpdateAsync(MovieTicket ticket, CancellationToken ct = default);

    /// <summary>Returns false if no ticket exists for this email.</summary>
    Task<bool> DeleteAsync(string customerEmail, CancellationToken ct = default);
}
