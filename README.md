# Belgium VAT Checker API

A .NET 9 Web API for validating Belgian and EU VAT numbers using the VIES (VAT Information Exchange System) service.

## Features

- ✅ Validate Belgian VAT numbers with checksum verification
- ✅ Validate any EU country VAT numbers via VIES
- ✅ Real-time validation against official EU databases
- ✅ Resilient HTTP client with retry policies and circuit breaker (Polly)
- ✅ RESTful API with OpenAPI/Swagger documentation
- ✅ Comprehensive unit tests with FakeItEasy and Shouldly
- ✅ CORS support for web applications

## Prerequisites

- .NET 9 SDK
- Visual Studio 2022 or VS Code (optional)

## Getting Started

1. Clone the repository:
```bash
git clone https://github.com/phmatray/BelgiumVatChecker.git
cd BelgiumVatChecker
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Build the solution:
```bash
dotnet build
```

4. Run tests:
```bash
dotnet test
```

5. Run the API:
```bash
cd BelgiumVatChecker.Api
dotnet run
```

The API will start on `https://localhost:5001` (or `http://localhost:5000`).

## API Endpoints

### Validate Any EU VAT Number
```http
POST /api/vat/validate
Content-Type: application/json

{
  "countryCode": "BE",
  "vatNumber": "0477472701"
}
```

Response:
```json
{
  "isValid": true,
  "countryCode": "BE",
  "vatNumber": "0477472701",
  "name": "COMPANY NAME",
  "address": "COMPANY ADDRESS",
  "requestDate": "2024-01-15T10:30:00Z",
  "errorMessage": null
}
```

### Validate Belgian VAT Number
```http
GET /api/vat/validate/belgium/0477472701
```

### Check VIES Service Status
```http
GET /api/vat/status
```

Response:
```json
{
  "isAvailable": true,
  "countryAvailability": {
    "BE": true,
    "FR": true,
    "DE": true,
    ...
  },
  "checkedAt": "2024-01-15T10:30:00Z"
}
```

## Belgian VAT Number Format

Belgian VAT numbers follow these rules:
- Format: BE0123456789 (10 digits)
- The first digit is always 0 or 1
- The last two digits are a checksum calculated using modulo 97

The API automatically:
- Removes country prefix if present
- Validates the format
- Verifies the checksum
- Checks against the VIES database

## Architecture

### Solution Structure
```
BelgiumVatChecker/
├── BelgiumVatChecker.Core/       # Core business logic
│   ├── Models/                   # Data models
│   ├── Services/                 # Service implementations
│   ├── Interfaces/               # Service contracts
│   └── Exceptions/               # Custom exceptions
├── BelgiumVatChecker.Api/        # Web API project
│   └── Controllers/              # API controllers
└── BelgiumVatChecker.Tests/      # Unit tests
```

### Key Components

- **ViesClient**: Handles SOAP communication with the EU VIES service
- **VatValidationService**: Implements VAT validation logic with Belgian-specific rules
- **VatController**: Exposes RESTful endpoints
- **Polly Integration**: Provides resilience with retry and circuit breaker patterns

## Error Handling

The API returns appropriate HTTP status codes:
- `200 OK`: Successful validation
- `400 Bad Request`: Invalid input parameters
- `500 Internal Server Error`: VIES service errors or unexpected issues

Error responses include descriptive messages:
```json
{
  "error": "Invalid Belgian VAT number format. Expected format: BE0123456789 (10 digits)"
}
```

## Configuration

### Polly Policies

The HTTP client is configured with:
- **Retry Policy**: 3 retries with exponential backoff (2, 4, 8 seconds)
- **Circuit Breaker**: Opens after 5 failures, stays open for 1 minute
- **Timeout**: 30 seconds per request

### CORS

CORS is enabled by default allowing any origin. For production, configure specific origins in `Program.cs`:

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://yourdomain.com")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```

## Testing

The project includes comprehensive unit tests for:
- VAT number format validation
- Belgian VAT checksum calculation
- VIES client HTTP communication
- Error handling scenarios

Run tests with coverage:
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Limitations

- The VIES service may be temporarily unavailable during maintenance (returns 503 errors)
- Rate limiting may apply to the VIES service
- VAT validation only confirms the number exists in the database, not the company's current status
- When VIES is unavailable, the API will return an error message indicating the service is down

## Handling VIES Service Unavailability

The VIES service occasionally returns 503 Service Unavailable errors during maintenance periods. The API handles this gracefully:

1. The Polly retry policy will automatically retry failed requests
2. If all retries fail, a clear error message is returned
3. The `/api/vat/status` endpoint can be used to check service availability before making validation requests

Example error response when VIES is down:
```json
{
  "isValid": false,
  "countryCode": "BE",
  "vatNumber": "0477472701",
  "errorMessage": "VIES service is temporarily unavailable (503). Please try again later."
}

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- EU VIES Service: https://ec.europa.eu/taxation_customs/vies/
- Belgian VAT Information: https://finances.belgium.be/