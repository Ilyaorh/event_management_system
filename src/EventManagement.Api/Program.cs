using EventManagement.Core.Contracts;
using EventManagement.Core.Data;
using EventManagement.Core.Events;
using EventManagement.Core.Registrations;
using EventManagement.Core.Tickets;
using EventManagement.Core.Users;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<EventManagementDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Events") ?? "Data Source=events.db"));
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<IEventCapacityService>(provider => provider.GetRequiredService<EventService>());
builder.Services.AddScoped<ParticipantService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<RegistrationService>();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<EventManagementDbContext>().Database.EnsureCreatedAsync();
}

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (DomainException error)
    {
        context.Response.StatusCode = error.Code switch
        {
            "EVENT_NOT_FOUND" or "USER_NOT_FOUND" or "REGISTRATION_NOT_FOUND" or "TICKET_NOT_FOUND" => StatusCodes.Status404NotFound,
            "EVENT_FULL" or "EVENT_CANCELLED" or "EVENT_STARTED" or "DUPLICATE_REGISTRATION" or "TICKET_ALREADY_GENERATED" or "TICKET_NOT_ACTIVE" or "ALREADY_CHECKED_IN" or "CAPACITY_INVALID" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        await context.Response.WriteAsJsonAsync(new { code = error.Code, message = error.Message }, context.RequestAborted);
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/events", async (CreateEventRequest request, EventService events, CancellationToken ct) =>
{
    var item = await events.CreateAsync(request, ct);
    return Results.Created($"/api/events/{item.Id}", item);
});
app.MapGet("/api/events", async (EventService events, CancellationToken ct) => Results.Ok(await events.ListAsync(ct)));
app.MapGet("/api/events/{id:guid}", async (Guid id, EventService events, CancellationToken ct) => Results.Ok(await events.GetAsync(id, ct)));
app.MapPost("/api/users", async (CreateParticipantRequest request, ParticipantService users, CancellationToken ct) =>
{
    var user = await users.CreateAsync(request, ct);
    return Results.Created($"/api/users/{user.Id}", user);
});
app.MapGet("/api/users/{id:guid}", async (Guid id, ParticipantService users, CancellationToken ct) => Results.Ok(await users.GetAsync(id, ct)));
app.MapPost("/api/registrations", async (CreateRegistrationRequest request, RegistrationService registrations, CancellationToken ct) =>
    Results.Ok(await registrations.CreateAsync(request, ct)));
app.MapDelete("/api/registrations/{id:guid}", async (Guid id, RegistrationService registrations, CancellationToken ct) =>
{
    await registrations.CancelAsync(id, ct);
    return Results.NoContent();
});
app.MapGet("/api/tickets/{id:guid}", async (Guid id, ITicketService tickets, CancellationToken ct) => Results.Ok(await tickets.GetAsync(id, ct)));
app.MapGet("/api/tickets/{id:guid}/qr", async (Guid id, ITicketService tickets, CancellationToken ct) =>
    Results.File((await tickets.GetAsync(id, ct)).QrCodeImage, "image/png"));
app.MapPost("/api/tickets/check-in", async (CheckInRequest request, ITicketService tickets, CancellationToken ct) =>
{
    await tickets.CheckInAsync(request.UniqueCode, ct);
    return Results.NoContent();
});
app.Run();
