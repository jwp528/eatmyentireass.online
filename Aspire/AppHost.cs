var builder = DistributedApplication.CreateBuilder(args);

// Azure Functions must be launched via 'func start', not 'dotnet run'.
// Using AddExecutable prevents Aspire from injecting ASPNETCORE_URLS, which
// would conflict with the inner Kestrel that ConfigureFunctionsWebApplication() starts.
var apiPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "Api"));
builder.AddExecutable("api", "cmd", apiPath, "/c", "func", "start")
    .WithHttpEndpoint(port: 7071, name: "http");

// Configure the client project
builder.AddProject<Projects.Client>("client")
    .WithHttpEndpoint(port: 5001, name: "https");

builder.Build().Run();
