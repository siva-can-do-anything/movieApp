using System.Text.Json;
using Microsoft.Extensions.Options;
using MovieTickets.Api.Models;

namespace MovieTickets.Api.Storage;

/// <summary>
/// Stores tickets in a single JSON file. Every operation reads the file, and every change
/// rewrites it, under one lock so concurrent requests in this process don't overwrite each other.
/// Fine for initial setup; swap for DynamoDB (same interface) when data must outlive a Lambda instance.
/// </summary>
public sealed class JsonFileMovieTicketRepository : IMovieTicketRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _filePath;
    private readonly string? _seedFilePath;
    private bool _initialized;

    public JsonFileMovieTicketRepository(IOptions<TicketStorageOptions> options)
    {
        _filePath = ResolvePath(options.Value.FilePath);
        _seedFilePath = string.IsNullOrWhiteSpace(options.Value.SeedFilePath)
            ? null
            : ResolvePath(options.Value.SeedFilePath);
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task<IReadOnlyList<MovieTicket>> GetAllAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return await ReadAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<MovieTicket?> GetAsync(string customerEmail, CancellationToken ct = default)
    {
        var key = NormalizeEmail(customerEmail);
        await _lock.WaitAsync(ct);
        try
        {
            var tickets = await ReadAsync(ct);
            return tickets.FirstOrDefault(t => t.CustomerEmail == key);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> AddAsync(MovieTicket ticket, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var tickets = await ReadAsync(ct);
            if (tickets.Any(t => t.CustomerEmail == ticket.CustomerEmail))
            {
                return false;
            }

            tickets.Add(ticket);
            await WriteAsync(tickets, ct);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> UpdateAsync(MovieTicket ticket, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var tickets = await ReadAsync(ct);
            var index = tickets.FindIndex(t => t.CustomerEmail == ticket.CustomerEmail);
            if (index < 0)
            {
                return false;
            }

            tickets[index] = ticket;
            await WriteAsync(tickets, ct);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string customerEmail, CancellationToken ct = default)
    {
        var key = NormalizeEmail(customerEmail);
        await _lock.WaitAsync(ct);
        try
        {
            var tickets = await ReadAsync(ct);
            if (tickets.RemoveAll(t => t.CustomerEmail == key) == 0)
            {
                return false;
            }

            await WriteAsync(tickets, ct);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    // Caller must hold _lock.
    private async Task<List<MovieTicket>> ReadAsync(CancellationToken ct)
    {
        EnsureInitialized();
        if (!File.Exists(_filePath))
        {
            return new List<MovieTicket>();
        }

        await using var stream = File.OpenRead(_filePath);
        if (stream.Length == 0)
        {
            return new List<MovieTicket>();
        }

        return await JsonSerializer.DeserializeAsync<List<MovieTicket>>(stream, JsonOptions, ct)
               ?? new List<MovieTicket>();
    }

    // Caller must hold _lock. Writes to a temp file then swaps it in, so a crash can't leave half a file.
    private async Task WriteAsync(List<MovieTicket> tickets, CancellationToken ct)
    {
        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(
                stream, tickets.OrderBy(t => t.CustomerEmail).ToList(), JsonOptions, ct);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(_filePath) && _seedFilePath is not null && File.Exists(_seedFilePath)
            && !string.Equals(_seedFilePath, _filePath, StringComparison.Ordinal))
        {
            File.Copy(_seedFilePath, _filePath);
        }

        _initialized = true;
    }

    private static string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
}
