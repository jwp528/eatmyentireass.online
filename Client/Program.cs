using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorApp.Client;
using BlazorApp.Client.Services;
using Blazored.LocalStorage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

Console.WriteLine("[Program] === EMEA Client Configuration ===");
Console.WriteLine($"[Program] Environment: {builder.HostEnvironment.Environment}");
Console.WriteLine($"[Program] BaseAddress: {builder.HostEnvironment.BaseAddress}");

// In Azure SWA, /api/* is proxied to Azure Functions at the same origin.
// In local development, Azure Functions runs on port 7071.
var apiBaseAddress = builder.HostEnvironment.IsDevelopment()
    ? "http://localhost:7071/"
    : builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseAddress) });

// Configure services
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<ISettingsService, SettingsService>();

// Version check service
builder.Services.AddScoped<IVersionCheckService, VersionCheckService>();

// Stats service
builder.Services.AddScoped<IStatsService, StatsService>();

// Collection (Assdex) service
builder.Services.AddScoped<ICollectionService, CollectionService>();

// Progress / perks service
builder.Services.AddScoped<IProgressService, ProgressService>();

// Daily Challenge service
builder.Services.AddScoped<IDailyChallengeService, DailyChallengeService>();

// Use API-based shared leaderboard service (writes to shared file via API)
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();

// Player name claiming service
builder.Services.AddScoped<IPlayerService, PlayerService>();

// Also keep local leaderboard service as fallback
builder.Services.AddScoped<ILocalLeaderboardService, LocalLeaderboardService>();

Console.WriteLine("[Program] ? Services configured:");
Console.WriteLine("[Program]   - LocalStorage for settings");
Console.WriteLine("[Program]   - API-based shared leaderboard (writes to Client/wwwroot/data/leaderboard.json)");
Console.WriteLine("[Program]   - Local leaderboard (fallback to browser storage)");
Console.WriteLine($"[Program]   - HttpClient base address: {apiBaseAddress}");

var app = builder.Build();

Console.WriteLine("[Program] ? Application built successfully - using shared leaderboard via API");
Console.WriteLine("[Program] === Ready to Run ===");

await app.RunAsync();
