# Shared Leaderboard System

## Overview
The "Eat My Entire Ass" game now features a **true shared leaderboard system** where all players see the same high scores. The system uses Azure Functions API to write directly to a single JSON file that all players read from.

## How It Works

### Architecture
- **File Location**: `Client/wwwroot/data/leaderboard.json`
- **Read Operations**: Client reads directly from the JSON file (fast, immediate)
- **Write Operations**: Client sends scores to Azure Functions API
- **API Updates**: Azure Functions writes directly to the shared file
- **Format**: JSON with an `entries` array containing `LeaderboardEntry` objects
- **Capacity**: Maintains up to 100 entries, displays top 10

### User Experience
1. **Viewing Scores**: Players see the global leaderboard when clicking the trophy button
2. **Saving Scores**: When a player saves a score:
   - Score is sent to the Azure Functions API
   - API writes directly to `Client/wwwroot/data/leaderboard.json`  
   - All players immediately see the updated scores
3. **Real-Time Updates**: No manual file replacement needed - everything is automatic!

### File Structure
```json
{
  "entries": [
    {
      "playerName": "PlayerName",
      "score": 85.5,
      "totalClicks": 342,
      "gameDate": "2024-01-15T10:30:00.000Z",
      "assTypeBreakdown": {
        "Golden": 2,
        "GYAT": 3,
        "Cartoon": 5,
        "Flat": 8,
        "Hairy": 4,
        "Boney": 12
      },
      "gameDurationSeconds": 60
    }
  ]
}
```

## Running the System

### Prerequisites
- .NET 9.0 SDK
- Azure Functions Core Tools v4.1.2+

### Setup Instructions
1. **Start Azure Functions API**:
   ```sh
   cd Api
   func start
   ```
   - Runs on `http://localhost:7071`
   - Handles score writes to shared file

2. **Start Blazor Client**:
   ```sh
   cd Client  
   dotnet run
   ```
   - Runs on `http://localhost:5000`
   - Reads from shared file, sends writes to API

3. **Play and Test**:
   - Navigate to `http://localhost:5000`
   - Play the game and save scores
   - Check that scores appear for all players immediately

## Technical Implementation

### API Endpoints
- **GET** `/api/leaderboard` - Returns top 10 scores
- **POST** `/api/leaderboard` - Saves a new score to shared file
- **DELETE** `/api/leaderboard` - Clears the leaderboard

### Services
- **`LeaderboardService`**: API-based service that communicates with Azure Functions
- **`LocalLeaderboardService`**: Fallback local storage service (backup only)

### File Operations
- **Thread-Safe**: Uses `SemaphoreSlim` to prevent concurrent file corruption
- **Automatic Directory Creation**: Creates `Client/wwwroot/data/` if it doesn't exist
- **Error Handling**: Graceful fallback to local storage if API is unavailable
- **CORS Support**: Proper CORS headers for cross-origin requests

## Benefits of This Approach

### ? **True Shared Experience**
- All players see the same leaderboard data
- No manual file updates required
- Real-time score sharing

### ? **High Performance**
- Reads are direct file access (very fast)
- Writes are handled server-side (reliable)
- No client-side file limitations

### ? **Reliability**  
- Thread-safe file operations
- Local storage fallback
- Comprehensive error handling

### ? **Scalability**
- Can easily be extended to use databases
- API-based architecture ready for cloud deployment
- Maintains up to 100 scores for better ranking

## Development Workflow

### Testing Score Submission
1. Play the game and achieve a score
2. Save the score with a player name
3. Check that the score appears immediately in the leaderboard
4. Verify that other players/browser tabs see the score

### File Location
The shared leaderboard file is located at:
`Client/wwwroot/data/leaderboard.json`

You can monitor this file to see scores being added in real-time.

### Troubleshooting

#### API Connection Issues
- Ensure Azure Functions API is running on `http://localhost:7071`
- Check API logs for file write operations
- Verify CORS headers are properly set

#### File Permission Issues  
- Ensure the API process has write permissions to `Client/wwwroot/data/`
- Check that the directory is created automatically

#### Scores Not Appearing
- Verify the API is writing to the correct file path
- Check browser network tab for failed API calls
- Look for fallback local storage entries

## Future Enhancements

### Potential Improvements
1. **Database Backend**: Replace file storage with SQL/NoSQL database
2. **Real-Time Notifications**: Add SignalR for live score updates
3. **Player Profiles**: Add user accounts and authentication  
4. **Score Validation**: Server-side validation to prevent cheating
5. **Leaderboard Categories**: Multiple leaderboards (daily, weekly, all-time)

### Cloud Deployment
This architecture is ready for Azure Static Web Apps deployment with:
- Client deployed as static web app
- API deployed as Azure Functions
- Shared storage via Azure Blob Storage or Azure SQL

## Security Considerations
- **Trust-Based System**: No validation of submitted scores
- **Public File**: Leaderboard file is publicly readable from wwwroot
- **No Authentication**: Anonymous score submission
- **File-Based**: Simple file locking prevents most concurrency issues

For production use, consider adding score validation, rate limiting, and user authentication.