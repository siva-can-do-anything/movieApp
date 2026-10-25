using System.Net.Http.Json;
using MovieTickets.Api.Models;

namespace MovieTickets.Api.Tests;

public sealed class SeedDataTests
{
    [Fact]
    public async Task Seed_file_is_loaded_on_first_use()
    {
        // Data/tickets.json is copied next to the API assembly at build time.
        using var factory = new TestApiFactory { SeedFilePath = "Data/tickets.json" };
        using var client = factory.CreateClient();

        var tickets = await client.GetFromJsonAsync<List<MovieTicket>>("/api/movietickets");

        Assert.Contains(tickets!, t => t.CustomerEmail == "jane.doe@example.com");
        Assert.Contains(tickets!, t => t.CustomerEmail == "john.smith@example.com");
    }

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }
}
