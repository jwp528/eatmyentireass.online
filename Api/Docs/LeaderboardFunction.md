# Leaderboard Function

## Overview
The `LeaderboardFunction` class provides RESTful API endpoints for managing game leaderboard data in the "Eat My Entire Ass" game. It handles retrieving top scores and saving new score entries with thread-safe file operations.

## Purpose
- Maintain a persistent leaderboard of the top 10 game scores
- Provide secure, thread-safe access to leaderboard data
- Handle score submission and automatic ranking
- Ensure data integrity through file locking mechanisms

## Endpoints

### GET /api/leaderboard
**Function Name:** `GetLeaderboard`
- **Authorization:** Anonymous
- **Purpose:** Retrieve the current top 10 scores from the leaderboard
- **Returns:** JSON array of `LeaderboardEntry` objects ordered by score (descending) and game date (descending)
- **Error Handling:** Returns HTTP 500 with error message if file operations fail

### POST /api/leaderboard
**Function Name:** `SaveScore`
- **Authorization:** Anonymous
- **Purpose:** Submit a new score entry to the leaderboard
- **Accepts:** JSON `LeaderboardEntry` object in request body
- **Validation:** Ensures player name is not null or empty
- **Returns:** HTTP 200 with success message, HTTP 400 for invalid data, HTTP 500 for processing errors

## General Workflow

### Score Retrieval Process
1. Acquire file access semaphore
2. Check if leaderboard file exists in temp directory
3. Read and deserialize JSON data
4. Sort entries by score and date
5. Return top 10 entries
6. Release semaphore

### Score Submission Process
1. Deserialize incoming score entry from request body
2. Validate player name is provided
3. Acquire file access semaphore
4. Load current leaderboard data
5. Add new entry to collection
6. Sort all entries and keep only top 10
7. Serialize and save updated leaderboard to file
8. Log operation details
9. Release semaphore

## Special Objects and Concepts

### SemaphoreSlim (`FileSemaphore`)
The function uses a `SemaphoreSlim` with a capacity of 1 to ensure thread-safe file operations:

```csharp
private static readonly SemaphoreSlim FileSemaphore = new(1, 1);
```

**Why it's used:**
- **Prevents File Corruption:** Multiple simultaneous requests could corrupt the JSON file if they try to read/write concurrently
- **Data Consistency:** Ensures only one operation can modify the leaderboard at a time
- **Race Condition Prevention:** Eliminates scenarios where two users submit scores simultaneously and one gets lost
- **Thread Safety:** Makes the function safe for concurrent execution in a multi-threaded environment

The semaphore is acquired with `await FileSemaphore.WaitAsync()` and released in a `finally` block to guarantee cleanup even if exceptions occur.

## File Storage
- **Location:** System temp directory (`TEMP` environment variable or `/tmp` on Unix)
- **Filename:** `leaderboard.json`
- **Format:** JSON with indented formatting for readability
- **Capacity:** Automatically maintained at maximum 10 entries

## Data Models
- **LeaderboardEntry:** Contains player information, score, game statistics, and timestamps
- **Leaderboard:** Wrapper object containing a list of `LeaderboardEntry` objects

## Error Handling
- Comprehensive try-catch blocks around all operations
- Detailed logging for debugging and monitoring
- Graceful fallback to empty leaderboard if file doesn't exist
- HTTP status code compliance for API responses

## Performance Considerations
- File-based storage is suitable for low-traffic scenarios
- Semaphore ensures data integrity but may create bottlenecks under high load
- Consider database migration for production high-traffic scenarios
- JSON serialization overhead is minimal for 10-entry datasets