# Movie Tickets Web API

An ASP.NET Core (.NET 8) Web API, hosted on **AWS Lambda** behind **API Gateway (HTTP API)**, for adding, reading, updating and deleting movie tickets.
The **customer email address is the primary key**: each email has at most one booking. Email matching ignores case.

Data is stored in a **JSON file**. This is the initial setup, and the storage sits behind an interface (`IMovieTicketRepository`) so it can be swapped for DynamoDB later.

## Endpoints

| Method | Route | Description | Responses |
|---|---|---|---|
| `GET` | `/api/movietickets` | List all tickets | 200 |
| `GET` | `/api/movietickets/{email}` | Get one customer's ticket | 200, 400, 404 |
| `POST` | `/api/movietickets` | Add a ticket | 201, 400, 409 (email already has one) |
| `PUT` | `/api/movietickets/{email}` | Replace a customer's ticket | 200, 400, 404 |
| `DELETE` | `/api/movietickets/{email}` | Delete a customer's ticket | 204, 400, 404 |
| `GET` | `/health` | Health check | 200 |

Errors are returned as standard RFC 7807 `ProblemDetails` JSON.

### Request body (POST)

```json
{
  "customerEmail": "alex.lee@example.com",
  "customerName": "Alex Lee",
  "movieTitle": "Space Odyssey Returns",
  "theaterName": "Downtown Cinema 3",
  "showTime": "2026-11-05T18:00:00Z",
  "numberOfTickets": 2,
  "seatNumbers": ["H1", "H2"],
  "ticketPrice": 11.75
}
```

`PUT` takes the same body without `customerEmail`, because the email comes from the URL. The server calculates `totalAmount` (`ticketPrice × numberOfTickets`) and sets `createdAtUtc` and `updatedAtUtc`.

**Validation:** email must be valid. `numberOfTickets` must be 1–20 and `ticketPrice` 0.01–1000. Seat numbers are optional, but if you give them there must be one per ticket, each unique.

## Project layout

```
src/MovieTickets.Api/
  Controllers/MovieTicketsController.cs   API endpoints
  Models/                                  Ticket and request models
  Storage/JsonFileMovieTicketRepository.cs JSON file storage
  Data/tickets.json                        Starting (seed) data
  serverless.template                      AWS SAM / CloudFormation template
  aws-lambda-tools-defaults.json           Settings for `dotnet lambda deploy-serverless`
  MovieTickets.Api.http                    Sample requests
tests/MovieTickets.Api.Tests/              xUnit integration tests
```

## Run locally

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet run --project src/MovieTickets.Api --launch-profile http
```

Open http://localhost:5050/swagger. Locally, data is saved to `App_Data/tickets.json` in the build output folder. The first run copies it from `Data/tickets.json`.

## Test

```bash
dotnet test
```

## Deploy to AWS Lambda

```bash
dotnet tool install -g Amazon.Lambda.Tools
cd src/MovieTickets.Api
dotnet lambda deploy-serverless --s3-bucket <your-deployment-bucket>
```

The stack outputs `ApiURL`. Region and stack name are set in `aws-lambda-tools-defaults.json`.
To turn on Swagger in AWS, pass `--template-parameters EnableSwagger=true`.

### Storage configuration

| Setting | Local default | Lambda (set in `serverless.template`) |
|---|---|---|
| `TicketStorage:FilePath` | `App_Data/tickets.json` | `/tmp/tickets.json` |
| `TicketStorage:SeedFilePath` | `Data/tickets.json` | `Data/tickets.json` |

> **Limitation:** in Lambda, `/tmp` is the only writable folder, and it is **per instance and temporary**. Changes are lost when an instance is recycled, and separate instances don't share data. That is fine for the initial setup and demos. For production, replace `JsonFileMovieTicketRepository` with a DynamoDB repository (partition key `customerEmail`) and register it in `Program.cs`.
