var builder = DistributedApplication.CreateBuilder(args);

var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator();

var cosmos = builder.AddAzureCosmosDB("cosmos")
    .RunAsEmulator();

var beachTripDb = cosmos.AddCosmosDatabase("beachtrip");

builder.AddProject<Projects.BeachTrip_Worker>("worker")
    .WithReference(serviceBus)
    .WithReference(beachTripDb)
    .WaitFor(serviceBus)
    .WaitFor(cosmos);

builder.AddProject<Projects.BeachTrip_Web>("web")
    .WithReference(serviceBus)
    .WithReference(beachTripDb)
    .WaitFor(serviceBus)
    .WaitFor(cosmos)
    .WithExternalHttpEndpoints();

builder.Build().Run();
