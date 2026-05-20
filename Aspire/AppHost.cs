var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureFunctionsProject<Projects.Api>("api")
    .WithHttpEndpoint(port: 7071, name: "http");

builder.AddProject<Projects.Client>("client")
    .WithHttpEndpoint(port: 5001, name: "https");

builder.Build().Run();
