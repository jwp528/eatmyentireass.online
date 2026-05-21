var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("storage").RunAsEmulator(e =>
{
    e.WithLifetime(ContainerLifetime.Persistent);
});

builder.AddAzureFunctionsProject<Projects.Api>("api")
    .WithHttpEndpoint(port: 7071, name: "http")
    .WithHostStorage(storage);

builder.AddProject<Projects.Client>("client")
    .WithHttpEndpoint(port: 5001, name: "https");

builder.Build().Run();
