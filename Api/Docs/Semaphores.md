# Understanding Semaphores in Azure Functions

## What is a Semaphore?
A semaphore is a synchronization primitive used in concurrent programming to control access to a shared resource by multiple threads or processes. Think of it as a digital "bouncer" that limits how many operations can access a resource simultaneously.

## SemaphoreSlim in .NET
`SemaphoreSlim` is a lightweight, async-friendly version of the traditional semaphore, designed specifically for use in modern .NET applications with async/await patterns.

### Basic Concepts
- **Initial Count:** Number of threads that can initially access the resource
- **Maximum Count:** Maximum number of threads that can ever access the resource
- **Current Count:** Number of available "slots" at any given time

## Why We Use Semaphores in the Leaderboard Function

### The Problem
In a multi-threaded web environment like Azure Functions, multiple requests can arrive simultaneously:

```
Request A: Save new score for Player 1
Request B: Save new score for Player 2  
Request C: Get top 10 scores
```

Without synchronization, these operations could interfere with each other:
1. **File Corruption:** Two writes happening simultaneously could corrupt the JSON file
2. **Data Loss:** One save operation might overwrite changes from another
3. **Inconsistent Reads:** A read might happen while a write is in progress, returning incomplete data
4. **Race Conditions:** The final state becomes unpredictable based on timing

### The Solution: File-Level Locking
```csharp
private static readonly SemaphoreSlim FileSemaphore = new(1, 1);
```

This creates a semaphore with:
- **Initial count: 1** - One thread can access the file initially
- **Maximum count: 1** - Only one thread can ever access the file at a time

## How It Works in Practice

### Acquiring the Semaphore
```csharp
await FileSemaphore.WaitAsync();
try
{
    // Critical section: file operations here
    // Only one thread can execute this code at a time
}
finally
{
    FileSemaphore.Release();
}
```

### Execution Flow
1. **Thread A** calls `WaitAsync()` ? Gets immediate access (count goes from 1 to 0)
2. **Thread B** calls `WaitAsync()` ? Blocks and waits (count is 0)
3. **Thread A** completes work and calls `Release()` ? Count goes from 0 to 1
4. **Thread B** unblocks and proceeds ? Gets access (count goes from 1 to 0)

## Real-World Scenario

### Without Semaphore (Dangerous)
```
Time 1: Player A submits score of 1000
Time 1: Player B submits score of 2000
Time 2: Both requests read current leaderboard: [500, 400, 300...]
Time 3: Player A adds their score: [1000, 500, 400, 300...]
Time 3: Player B adds their score: [2000, 500, 400, 300...]  // Lost Player A's score!
Time 4: Both save to file - Player B's version overwrites Player A's
Result: Player A's score is lost forever
```

### With Semaphore (Safe)
```
Time 1: Player A submits score of 1000 ? Acquires semaphore
Time 1: Player B submits score of 2000 ? Waits for semaphore
Time 2: Player A reads: [500, 400, 300...]
Time 3: Player A saves: [1000, 500, 400, 300...] ? Releases semaphore
Time 4: Player B acquires semaphore
Time 5: Player B reads: [1000, 500, 400, 300...]
Time 6: Player B saves: [2000, 1000, 500, 400...] ? Releases semaphore
Result: Both scores are preserved correctly
```

## Alternative Approaches and Why We Don't Use Them

### Database Transactions
- **Pro:** Built-in ACID guarantees
- **Con:** Requires database infrastructure, more complex setup
- **Why not chosen:** File storage is simpler for low-traffic scenarios

### File Locking (FileStream.Lock)
- **Pro:** OS-level file locking
- **Con:** Not async-friendly, harder to manage
- **Why not chosen:** Doesn't work well with async/await patterns

### In-Memory Locking
- **Pro:** Faster than file operations
- **Con:** Data lost on function restart/scale-out
- **Why not chosen:** Need persistent storage

## Performance Implications

### Benefits
- **Data Integrity:** Prevents corruption and data loss
- **Predictable Behavior:** Operations execute in a defined order
- **Thread Safety:** Safe for concurrent access

### Costs
- **Serialization:** All file operations become sequential
- **Potential Bottleneck:** High concurrent load could create queuing
- **Latency:** Later requests must wait for earlier ones to complete

### When to Consider Alternatives
- **High Traffic:** If you expect >100 concurrent requests
- **Geographic Distribution:** Multiple Azure regions need shared state  
- **Advanced Queries:** Need complex leaderboard filtering/searching

## Best Practices

### Always Use Try-Finally
```csharp
await semaphore.WaitAsync();
try
{
    // Critical section
}
finally
{
    semaphore.Release(); // Always release, even on exceptions
}
```

### Use Timeout for WaitAsync
```csharp
if (await semaphore.WaitAsync(TimeSpan.FromSeconds(30)))
{
    try { /* work */ }
    finally { semaphore.Release(); }
}
else
{
    throw new TimeoutException("Could not acquire file lock");
}
```

### Static Semaphore for Shared Resource
The semaphore should be static to ensure all instances of the function share the same lock:
```csharp
private static readonly SemaphoreSlim FileSemaphore = new(1, 1);
```

## Monitoring and Debugging

### Signs of Semaphore Issues
- Requests timing out unexpectedly
- High response times during concurrent access
- Logs showing long wait times before file operations

### Debugging Tips
- Add logging before and after semaphore acquisition
- Monitor current count: `FileSemaphore.CurrentCount`
- Set timeouts to prevent indefinite blocking
- Use Azure Application Insights to track performance metrics

## Conclusion
Semaphores are essential for maintaining data integrity in concurrent environments. While they introduce some performance overhead through serialization, they prevent much more serious issues like data corruption and race conditions. For the leaderboard function's file-based storage, a semaphore with count=1 ensures that all file operations are atomic and safe, providing reliable data persistence for game scores.