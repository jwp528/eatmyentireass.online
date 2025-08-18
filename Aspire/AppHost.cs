var builder = DistributedApplication.CreateBuilder(args);

// Add the Azure Functions API as a project - let Aspire assign port
var api = builder.AddProject<Projects.Api>("api")
    .WithHttpEndpoint(name: "api-http");

// Add the Blazor WebAssembly Client - use port 5001
var client = builder.AddProject<Projects.Client>("client")
    .WithHttpEndpoint(port: 5001, name: "client-http")
    .WithEnvironment("API_Prefix", api.GetEndpoint("api-http"));

builder.Build().Run();
