using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MovieTickets.Api.Tests;

/// <summary>Starts the API in memory with its own temporary JSON file.</summary>
public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    public string FilePath { get; } =
        Path.Combine(Path.GetTempPath(), "movietickets-tests", $"{Guid.NewGuid():N}.json");

    public string? SeedFilePath { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TicketStorage:FilePath"] = FilePath,
                ["TicketStorage:SeedFilePath"] = SeedFilePath ?? string.Empty,
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
