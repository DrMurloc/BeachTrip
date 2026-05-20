var builder = DistributedApplication.CreateBuilder(args);

var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator();

var cosmos = builder.AddAzureCosmosDB("cosmos")
    .RunAsEmulator();

var beachTripDb = cosmos.AddCosmosDatabase("beachtrip");
beachTripDb.AddContainer("events", "/aggregateId");
beachTripDb.AddContainer("sagas", "/id");
beachTripDb.AddContainer("projection-leases", "/id");
beachTripDb.AddContainer("view-attendees", "/id");
beachTripDb.AddContainer("view-carpools", "/id");
beachTripDb.AddContainer("view-rooms", "/id");
beachTripDb.AddContainer("view-parking-spots", "/id");
beachTripDb.AddContainer("view-parking-allocation", "/id");

// Reference the cosmos *account* so the injected connection string is named "cosmos"
// (matches builder.AddAzureCosmosClient("cosmos") on the consumer side). The database
// name is hard-coded in BeachTripCosmosOptions.
//
// WaitForStart instead of WaitFor — the Cosmos emulator never publishes a Healthy state
// to Aspire (no separate cosmos-emulatorhealth resource like ServiceBus has), so a full
// WaitFor hangs forever. CosmosClient + CatalogSeeder retry on their own.
builder.AddProject<Projects.BeachTrip_Worker>("worker")
    .WithReference(serviceBus)
    .WithReference(cosmos)
    .WaitForStart(serviceBus)
    .WaitForStart(cosmos);

builder.AddProject<Projects.BeachTrip_Web>("web")
    .WithReference(serviceBus)
    .WithReference(cosmos)
    .WaitForStart(serviceBus)
    .WaitForStart(cosmos)
    .WithExternalHttpEndpoints();

builder.Build().Run();
