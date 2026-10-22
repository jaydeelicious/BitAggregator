# BitAggregator

BitAggregator is a .NET app for aggregating, retrieving and caching cryptocurrency (currently BTC) prices from multiple exchanges.

The application retrieves hourly closing prices from Bitstamp and Bitfinex, calculates an aggregated price using the arithmetic mean, and persists the result in SQLite. Subsequent requests for the same instrument and timestamp return the stored result without calling external exchanges again.

A standalone Blazor WebAssembly frontend provides price lookup and persisted-price history views.

## Technology Stack

| Component | Technology |
|---|---|
| Backend | ASP.NET Core Web API, .NET 10, SQLite |
| Frontend | Blazor WebAssembly, .NET 10 |
| Persistence | SQLite with Entity Framework Core |
| Automated testing | xUnit, Moq, bUnit |


## Features

- Hourly BTC/USD closing-price retrieval from Bitstamp and Bitfinex
- Arithmetic-mean aggregation of exchange prices
- Persistent SQLite caching by instrument and UTC timestamp
- Individual exchange-price breakdown
- Datastore-only historical price queries
- Hour-accurate UTC timestamp selection
- Responsive Blazor WebAssembly interface
- Automated backend, integration, HTTP-client, and Blazor component tests

## Architecture

The backend follows a layered architecture:

```text
src/
├── BitAggregator.Api/
├── BitAggregator.Application/
├── BitAggregator.Domain/
├── BitAggregator.Infrastructure/
└── BitAggregator.Web/

tests/
└── BitAggregator.Tests/
```

**Domain:** Aggregated price and source-price entities.

**Application:** Contains the aggregation service, interfaces, result models, and aggregation logic. It orchestrates price-source retrieval and cache access.

**Infrastructure:** Implements SQLite persistence, repositories, and the HTTP adapters for Bitstamp and Bitfinex.

**API:** Exposes REST endpoints, implements request validation and error handling.

**Web:** A standalone Blazor WebAssembly application using a typed HTTP client to communicate with the backend.

The `IPriceSource` abstraction allows additional exchange adapters to be introduced. The `IPriceAggregator` abstraction allows alternative aggregation strategies without modifying the API controller.

## Prerequisites

- .NET 10 SDK
- EF Core command-line tools for database migrations

Verify the SDK:

```bash
dotnet --version
```

Install the EF Core CLI if needed:

```bash
dotnet tool install --global dotnet-ef --version "10.*"
```

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/jaydeelicious/BitAggregator.git
cd BitAggregator
```

### 2. Restore and build

```bash
dotnet restore
dotnet build
```

### 3. Initialize SQLite

Run from the repository root:

```bash
dotnet ef database update --project src/BitAggregator.Infrastructure --startup-project src/BitAggregator.Api
```

The default connection string is:

```json
"DefaultConnection": "Data Source=bitaggregator.db"
```

### 4. Start the backend

From the repository root:

```bash
dotnet run --project src/BitAggregator.Api --launch-profile https
```

Default local endpoints:

- HTTPS: `https://localhost:7017`
- HTTP: `http://localhost:5005`
- Swagger UI: `https://localhost:7017/swagger`

If the HTTPS development certificate is not trusted:

```bash
dotnet dev-certs https --trust
```

Swagger UI is enabled in the Development environment.

### 5. Start the frontend

Open a second terminal at the repository root:

```bash
dotnet run --project src/BitAggregator.Web --urls http://localhost:5180
```

Open:

`http://localhost:5180`

The frontend has two pages:

- **Price lookup (`/`):** Choose BTC/USD, a UTC date, and an hour. Retrieve the aggregated price, source prices, and result origin.
- **Price history (`/history`):** Select an inclusive UTC date/time range and view previously persisted prices.

## API

### Retrieve an aggregated price

```http
GET /api/prices/BTCUSD?timestamp=2023-01-01T01:00:00Z
```

The API first checks SQLite for the requested instrument and timestamp.

If the record exists, it returns the persisted result.

Otherwise, it fetches the corresponding hourly closing prices from both exchanges, calculates their arithmetic mean, persists the aggregated and source prices, and returns the result.

Example response structure (illustrative prices):

```json
{
  "instrument": "BTC/USD",
  "timestamp": "2023-01-01T01:00:00Z",
  "aggregatedPrice": 16530.25,
  "resultSource": "ExternalApis",
  "sourcePrices": [
    {
      "source": "Bitstamp",
      "price": 16531.0
    },
    {
      "source": "Bitfinex",
      "price": 16529.5
    }
  ]
}
```

On repeated requests for the same cached timestamp, `resultSource` becomes `"Datastore"`.

### Retrieve persisted price history

```http
GET /api/prices/BTCUSD/history?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z
```

This endpoint reads **only from SQLite**.

Returns persisted BTC/USD prices within an inclusive UTC time range, ordered by timestamp. An empty range returns an empty array with HTTP 200.

### Validation and error handling

The API currently supports `BTCUSD`.

Timestamps must be:

- Explicitly UTC (`Z` or `+00:00`)
- Aligned to a full hour
- Valid for the requested operation

Single-price lookup rejects future timestamps. History requests require `from` to be earlier than or equal to `to`.

Invalid requests return HTTP 400 using `ValidationProblemDetails`.

Exchange failures can produce HTTP 502.

Unexpected server failures produce HTTP 500 with standardized problem responses.

## Design Decisions and Assumptions

### Hourly timestamp convention

The requested timestamp represents the **closing boundary** of a one-hour OHLC candle.

For example, requesting `2023-01-01T01:00:00Z` corresponds to the candle that starts at `2023-01-01T00:00:00Z`.

The API uses the candle's closing price, not its opening, highest, or lowest price.

### Aggregation

The current aggregation strategy is the arithmetic mean of the available configured exchange closing prices.

Both configured sources are required for a successful new aggregation. A missing or failed source does not silently produce an incomplete average.

### Caching

Prices are cached persistently in SQLite.

The instrument and requested timestamp form a unique key, preventing duplicate persisted aggregates for the same hour.

The response identifies whether it came from the datastore or from a fresh external aggregation.

## Testing

Run all automated tests from the repository root:

```bash
dotnet test
```

The test suite covers application logic, exchange adapters, persistence, API behavior, typed HTTP communication, and Blazor component states.

The frontend component tests use bUnit and mocked HTTP responses. They do not require live exchange access.