using Amazon.Lambda.AspNetCoreServer.Hosting;
using MovieTickets.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = Path.Combine(AppContext.BaseDirectory, "MovieTickets.Api.xml");
    if (File.Exists(xmlFile)) options.IncludeXmlComments(xmlFile);
});
builder.Services.AddProblemDetails();

builder.Services.Configure<TicketStorageOptions>(
    builder.Configuration.GetSection(TicketStorageOptions.SectionName));
builder.Services.AddSingleton<IMovieTicketRepository, JsonFileMovieTicketRepository>();
builder.Services.AddSingleton(TimeProvider.System);

// When running in AWS Lambda behind API Gateway (HTTP API), this swaps Kestrel for the Lambda runtime.
// Locally it does nothing, so `dotnet run` still starts a normal web server.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).ExcludeFromDescription();

app.Run();

// Lets the test project start the app in memory.
public partial class Program;
