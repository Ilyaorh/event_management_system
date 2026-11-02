using EventManagement.Core.Contracts;
using EventManagement.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Core.Events;

public sealed class EventService(EventManagementDbContext db) : IEventCapacityService
{
    public async Task<EventDto> CreateAsync(CreateEventRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Location))
            throw new DomainException("INVALID_EVENT", "Название и место обязательны.");
        if (request.TotalCapacity < 1)
            throw new DomainException("INVALID_EVENT", "Вместимость должна быть больше нуля.");
        if (request.StartDate <= DateTimeOffset.UtcNow)
            throw new DomainException("INVALID_EVENT", "Дата мероприятия должна быть в будущем.");
        var item = new EventRecord
        {
            Id = Guid.NewGuid(), Title = request.Title.Trim(), Description = request.Description?.Trim() ?? "",
            StartDate = request.StartDate, Location = request.Location.Trim(), TotalCapacity = request.TotalCapacity,
            AvailableSeats = request.TotalCapacity
        };
        db.Events.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task<IReadOnlyList<EventDto>> ListAsync(CancellationToken cancellationToken)
    {
        var events = await db.Events.AsNoTracking().ToListAsync(cancellationToken);
        return events.OrderBy(item => item.StartDate).Select(ToDto).ToArray();
    }

    public async Task<EventDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new DomainException("EVENT_NOT_FOUND", "Мероприятие не найдено.");
        return ToDto(item);
    }

    public async Task<ReserveSpotResult> ReserveAsync(ReserveSpotRequest request, CancellationToken cancellationToken)
    {
        var current = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.EventId, cancellationToken);
        if (current is null)
            return new ReserveSpotResult(false, "EVENT_NOT_FOUND");
        if (current.Status != "Published")
            return new ReserveSpotResult(false, "EVENT_CANCELLED");
        if (current.StartDate <= DateTimeOffset.UtcNow)
            return new ReserveSpotResult(false, "EVENT_STARTED");
        var changed = await db.Events.Where(x => x.Id == request.EventId && x.Status == "Published" && x.AvailableSeats > 0)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.AvailableSeats, x => x.AvailableSeats - 1), cancellationToken);
        if (changed == 1)
            return new ReserveSpotResult(true, null);
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.EventId, cancellationToken);
        return new ReserveSpotResult(false, item is null ? "EVENT_NOT_FOUND" : item.Status != "Published" ? "EVENT_CANCELLED" : "EVENT_FULL");
    }

    public async Task ReleaseAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var changed = await db.Events.Where(x => x.Id == eventId && x.AvailableSeats < x.TotalCapacity)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.AvailableSeats, x => x.AvailableSeats + 1), cancellationToken);
        if (changed != 1)
            throw new DomainException("CAPACITY_INVALID", "Невозможно вернуть место для этой регистрации.");
    }

    private static EventDto ToDto(EventRecord x) => new(x.Id, x.Title, x.Description, x.StartDate, x.Location, x.TotalCapacity, x.AvailableSeats, x.Status);
}
