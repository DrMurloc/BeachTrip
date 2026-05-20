var builder = DistributedApplication.CreateBuilder(args);

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

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

// WaitForStart instead of WaitFor — the Cosmos emulator never publishes a Healthy state
// to Aspire, so a full WaitFor hangs forever. CosmosClient + CatalogSeeder retry on their
// own. RabbitMQ has a proper health check, so WaitFor is fine on it.
builder.AddProject<Projects.BeachTrip_Worker>("worker")
    .WithReference(rabbitmq)
    .WithReference(cosmos)
    .WaitFor(rabbitmq)
    .WaitForStart(cosmos);

builder.AddProject<Projects.BeachTrip_Web>("web")
    .WithReference(rabbitmq)
    .WithReference(cosmos)
    .WaitFor(rabbitmq)
    .WaitForStart(cosmos)
    .WithExternalHttpEndpoints();

builder.Build().Run();
