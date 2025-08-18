var builder = DistributedApplication.CreateBuilder(args);

// Add the Azure Functions API as a project
var api = builder.AddProject<Projects.Api>("api")
    .WithHttpEndpoint(port: 7071, name: "api-http");

// Add the Blazor WebAssembly Client
var client = builder.AddProject<Projects.Client>("client")
    .WithHttpEndpoint(port: 5000, name: "client-http")
    .WithEnvironment("API_Prefix", api.GetEndpoint("api-http"));

builder.Build().Run();
