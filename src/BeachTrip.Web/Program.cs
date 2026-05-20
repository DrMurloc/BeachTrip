using BeachTrip.Infrastructure;
using BeachTrip.Web.Components;
using BeachTrip.Web.Live;
using BeachTrip.Web.Services;
using MassTransit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddBeachTripPublisher(bus =>
{
    bus.AddConsumer<ViewUpdatedConsumer>();
    bus.AddConsumer<SoloDriverBumpedConsumer>();
});

builder.Services.AddSingleton<LiveUpdates>();
builder.Services.AddSingleton<IViewStore, CosmosViewStore>();
builder.Services.AddScoped<IdentityService>();
builder.Services.AddScoped<ProtectedSessionStorage>();

builder.Services.AddMudServices();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
