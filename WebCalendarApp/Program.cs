using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using WebCalendarApp.Services.Interfaces.Calendar;
using WebCalendarApp.Services.Services.Calendar;

var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ICalendarService, GraphCalendarService>();
builder.Services.AddSingleton(sp =>
    {
        var clientId = builder.Configuration["testPortal:Azure:ClientId"];
        var clientSecret = builder.Configuration["testPortal:Azure:ClientSecret"];
        var tenantId = builder.Configuration["testPortal:Azure:TenantId"];
        var scopes = new[] { builder.Configuration["testPortal:Azure:GraphAPI"] };
        var options = new TokenCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
        };

        var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);

        return new GraphServiceClient(clientSecretCredential, scopes);
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("getFreeSlots", async (DateTime start, DateTime end, [FromServices] ICalendarService calendarService, CancellationToken token) =>
    await calendarService.GetFreeSlotsAsync(new string[] { "vlad.doroh@gmail.com" }, start, end, token))
.WithName("getFreeSlots")
.WithOpenApi();

app.Run();