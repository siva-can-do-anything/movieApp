namespace MovieTickets.Api.Storage;

public sealed class TicketStorageOptions
{
    public const string SectionName = "TicketStorage";

    /// <summary>
    /// JSON file the API reads and writes. Relative paths are resolved from the app folder.
    /// On AWS Lambda this must be under /tmp, the only writable folder.
    /// </summary>
    public string FilePath { get; set; } = "Data/tickets.json";

    /// <summary>
    /// Optional starting data. Copied to <see cref="FilePath"/> the first time, if that file doesn't exist yet.
    /// </summary>
    public string? SeedFilePath { get; set; }
}
