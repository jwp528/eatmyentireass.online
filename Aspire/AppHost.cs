var builder = DistributedApplication.CreateBuilder(args);

// Configure the client project
builder.AddProject<Projects.Client>("client")
    .WithHttpEndpoint(port: 5001, name: "https");

// Azure Functions API must be started separately — do NOT add it here.
// When Aspire manages it via AddProject, it sets ASPNETCORE_URLS=http://localhost:7071
// which conflicts with the inner Kestrel that ConfigureFunctionsWebApplication() starts,
// causing the func host to accept TCP connections but never return HTTP responses.
//
// To start the API: open a terminal in the Api/ directory and run: func start

builder.Build().Run();
