using Microsoft.AspNetCore.Mvc;
using WebCalendarApp.Services.Interfaces.Calendar;
using WebCalendarApp.Services.Services.Calendar;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ICalendarService, GraphCalendarService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/getFreeSlots", async (DateTime start, DateTime end, [FromServices] ICalendarService calendarService, CancellationToken token) =>
    await calendarService.GetFreeSlotsAsync(new string[] { "vlad.doroh@gmail.com" }, start, end, token))
.WithName("getFreeSlots")
.WithOpenApi();

app.Run();