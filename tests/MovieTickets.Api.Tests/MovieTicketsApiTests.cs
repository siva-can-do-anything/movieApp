using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MovieTickets.Api.Models;

namespace MovieTickets.Api.Tests;

public sealed class MovieTicketsApiTests : IDisposable
{
    private const string BaseUrl = "/api/movietickets";
    private readonly TestApiFactory _factory = new();
    private readonly HttpClient _client;

    public MovieTicketsApiTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private static object NewTicket(string email = "alex.lee@example.com", int count = 2) => new
    {
        customerEmail = email,
        customerName = "Alex Lee",
        movieTitle = "Space Odyssey Returns",
        theaterName = "Downtown Cinema 3",
        showTime = "2026-11-05T18:00:00Z",
        numberOfTickets = count,
        seatNumbers = Enumerable.Range(1, count).Select(i => $"H{i}").ToArray(),
        ticketPrice = 11.75m,
    };

    [Fact]
    public async Task GetAll_starts_empty()
    {
        var tickets = await _client.GetFromJsonAsync<List<MovieTicket>>(BaseUrl);
        Assert.NotNull(tickets);
        Assert.Empty(tickets);
    }

    [Fact]
    public async Task Create_then_get_returns_ticket_with_total()
    {
        var create = await _client.PostAsJsonAsync(BaseUrl, NewTicket());
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.EndsWith($"{BaseUrl}/alex.lee@example.com", create.Headers.Location!.ToString());

        var ticket = await _client.GetFromJsonAsync<MovieTicket>($"{BaseUrl}/alex.lee@example.com");
        Assert.NotNull(ticket);
        Assert.Equal("Alex Lee", ticket.CustomerName);
        Assert.Equal(23.50m, ticket.TotalAmount);
        Assert.Equal(new[] { "H1", "H2" }, ticket.SeatNumbers);
    }

    [Fact]
    public async Task Email_key_is_case_insensitive()
    {
        await _client.PostAsJsonAsync(BaseUrl, NewTicket("Alex.Lee@Example.COM"));

        var response = await _client.GetAsync($"{BaseUrl}/ALEX.LEE@example.com");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ticket = await response.Content.ReadFromJsonAsync<MovieTicket>();
        Assert.Equal("alex.lee@example.com", ticket!.CustomerEmail);
    }

    [Fact]
    public async Task Create_duplicate_email_returns_409()
    {
        await _client.PostAsJsonAsync(BaseUrl, NewTicket());
        var second = await _client.PostAsJsonAsync(BaseUrl, NewTicket());
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", 2)]
    [InlineData("alex.lee@example.com", 0)]
    [InlineData("alex.lee@example.com", 21)]
    public async Task Create_with_invalid_input_returns_400(string email, int count)
    {
        var response = await _client.PostAsJsonAsync(BaseUrl, NewTicket(email, count));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_seat_count_mismatch_returns_400()
    {
        var body = new
        {
            customerEmail = "a@example.com", customerName = "A", movieTitle = "M", theaterName = "T",
            showTime = "2026-11-05T18:00:00Z", numberOfTickets = 3, seatNumbers = new[] { "A1" },
            ticketPrice = 10m,
        };
        var response = await _client.PostAsJsonAsync(BaseUrl, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_without_show_time_returns_400()
    {
        var body = new
        {
            customerEmail = "a@example.com", customerName = "A", movieTitle = "M", theaterName = "T",
            numberOfTickets = 1, ticketPrice = 10m,
        };
        var response = await _client.PostAsJsonAsync(BaseUrl, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_changes_ticket_and_keeps_created_time()
    {
        await _client.PostAsJsonAsync(BaseUrl, NewTicket());
        var original = await _client.GetFromJsonAsync<MovieTicket>($"{BaseUrl}/alex.lee@example.com");

        var update = new
        {
            customerName = "Alex Lee", movieTitle = "Another Movie", theaterName = "Riverside IMAX",
            showTime = "2026-11-06T20:00:00Z", numberOfTickets = 4, seatNumbers = Array.Empty<string>(),
            ticketPrice = 10m,
        };
        var response = await _client.PutAsJsonAsync($"{BaseUrl}/alex.lee@example.com", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await _client.GetFromJsonAsync<MovieTicket>($"{BaseUrl}/alex.lee@example.com");
        Assert.Equal("Another Movie", updated!.MovieTitle);
        Assert.Equal(40m, updated.TotalAmount);
        Assert.Equal(original!.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.True(updated.UpdatedAtUtc >= original.UpdatedAtUtc);
    }

    [Fact]
    public async Task Update_missing_ticket_returns_404()
    {
        var update = new
        {
            customerName = "X", movieTitle = "M", theaterName = "T",
            showTime = "2026-11-06T20:00:00Z", numberOfTickets = 1, ticketPrice = 10m,
        };
        var response = await _client.PutAsJsonAsync($"{BaseUrl}/nobody@example.com", update);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_ticket_then_returns_404()
    {
        await _client.PostAsJsonAsync(BaseUrl, NewTicket());

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"{BaseUrl}/alex.lee@example.com")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{BaseUrl}/alex.lee@example.com")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"{BaseUrl}/alex.lee@example.com")).StatusCode);
    }

    [Fact]
    public async Task Get_with_invalid_email_returns_400()
    {
        var response = await _client.GetAsync($"{BaseUrl}/not-an-email");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Changes_are_saved_to_the_json_file()
    {
        await _client.PostAsJsonAsync(BaseUrl, NewTicket());

        var json = await File.ReadAllTextAsync(_factory.FilePath);
        var saved = JsonSerializer.Deserialize<List<MovieTicket>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Single(saved!);
        Assert.Equal("alex.lee@example.com", saved![0].CustomerEmail);
    }

    [Fact]
    public async Task Concurrent_creates_all_saved()
    {
        var tasks = Enumerable.Range(0, 20)
            .Select(i => _client.PostAsJsonAsync(BaseUrl, NewTicket($"user{i}@example.com", 1)));
        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var all = await _client.GetFromJsonAsync<List<MovieTicket>>(BaseUrl);
        Assert.Equal(20, all!.Count);
    }
}
