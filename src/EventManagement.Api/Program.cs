using EventManagement.Core.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<EventManagementDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Events") ?? "Data Source=events.db"));
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<EventManagementDbContext>().Database.EnsureCreatedAsync();
}
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();
