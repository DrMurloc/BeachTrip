using BeachTrip.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddBeachTripInfrastructure();

var host = builder.Build();
host.Run();
