var builder = DistributedApplication.CreateBuilder(args);

// Configure the client project 
builder.AddProject<Projects.Client>("client")
    .WithHttpEndpoint(port: 5001, name: "https");

builder.AddProject<Projects.Api>("api")
    .WithHttpEndpoint(port: 7071, name: "http");

// Note: Azure Functions API should be started separately using 'func start' in the Api directory
// This is because Azure Functions don't integrate well with Aspire's orchestration model

builder.Build().Run();
