using EventManagement.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Tests;

internal sealed class SqliteFixture : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"event-test-{Guid.NewGuid():N}.db");

    public SqliteFixture()
    {
        using var db = Open();
        db.Database.EnsureCreated();
    }

    public EventManagementDbContext Open()
    {
        var options = new DbContextOptionsBuilder<EventManagementDbContext>()
            .UseSqlite($"Data Source={path};Default Timeout=5").Options;
        return new EventManagementDbContext(options);
    }

    public void Dispose()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            File.Delete(path + suffix);
    }
}
