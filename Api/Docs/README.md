# API Documentation

This directory contains detailed documentation for all Azure Functions in the "Eat My Entire Ass" game API.

## Available Functions

### [LeaderboardFunction.md](./LeaderboardFunction.md)
Comprehensive documentation for the leaderboard management system, including:
- GET `/api/leaderboard` - Retrieve top 10 scores
- POST `/api/leaderboard` - Submit new scores
- Thread-safe file operations using semaphores
- Error handling and data validation

### [WeatherForecastFunction.md](./WeatherForecastFunction.md)
Documentation for the demonstration weather API endpoint:
- GET `/api/WeatherForecast` - Generate mock weather data
- Template implementation for other functions
- Basic Azure Functions structure example

## Technical Concepts

### [Semaphores.md](./Semaphores.md)
Detailed explanation of semaphore usage in the API:
- What are semaphores and why we need them
- Thread safety in concurrent environments
- Real-world examples of race conditions
- Performance implications and best practices

## API Architecture

### Technology Stack
- **.NET 9.0** - Runtime framework
- **Azure Functions Worker** - Serverless compute platform
- **JSON File Storage** - Simple persistence for low-traffic scenarios
- **Thread-Safe Synchronization** - SemaphoreSlim for data integrity

### Common Patterns
All functions follow these patterns:
- Dependency injection for logging
- Comprehensive error handling
- HTTP status code compliance
- JSON serialization for data exchange
- Anonymous authorization for public access

### Development Guidelines
When adding new functions to this API:

1. **Create corresponding documentation** in this folder
2. **Follow existing error handling patterns** with try-catch blocks
3. **Use appropriate HTTP status codes** (200, 400, 500, etc.)
4. **Add logging** for debugging and monitoring
5. **Consider thread safety** if accessing shared resources
6. **Validate input data** before processing
7. **Use dependency injection** for services like logging

### File Organization
```
Api/
??? Docs/
?   ??? README.md (this file)
?   ??? LeaderboardFunction.md
?   ??? WeatherForecastFunction.md
?   ??? Semaphores.md
??? LeaderboardFunction.cs
??? WeatherForecastFunction.cs
??? Program.cs
??? local.settings.json
```

## Getting Started

### Prerequisites
- .NET 9.0 SDK
- Azure Functions Core Tools v4
- Understanding of async/await patterns
- Basic knowledge of HTTP REST APIs

### Running the API
1. Navigate to the `Api` directory
2. Ensure `local.settings.json` exists (copy from `local.settings.example.json`)
3. Run `func start` to start the Functions runtime
4. API will be available at `http://localhost:7071`

### Testing Endpoints
- **Weather:** `curl http://localhost:7071/api/WeatherForecast`
- **Leaderboard GET:** `curl http://localhost:7071/api/leaderboard`
- **Leaderboard POST:** `curl -X POST http://localhost:7071/api/leaderboard -H "Content-Type: application/json" -d '{"PlayerName":"Test","Score":1000,"GameDate":"2024-01-01T00:00:00Z"}'`

## Deployment
The API is configured for deployment to Azure Functions via Azure Static Web Apps. See the main project README for deployment instructions.

## Contributing
When modifying or adding functions:
1. Update the corresponding documentation file
2. Add new concepts to the technical documentation if applicable
3. Follow the established patterns and coding standards
4. Test thoroughly with both success and error scenarios