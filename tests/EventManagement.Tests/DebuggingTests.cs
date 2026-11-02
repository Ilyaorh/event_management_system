using EventManagement.Core.Contracts;
using EventManagement.Core.Data;
using EventManagement.Core.Events;
using EventManagement.Core.Registrations;
using EventManagement.Core.Tickets;
using EventManagement.Core.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventManagement.Tests;

public class DebuggingTests
{
    [Fact]
    public async Task TicketGeneration_RejectsNullRequestExplicitly()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.Open();
        var tickets = new TicketService(db);
        await Assert.ThrowsAsync<ArgumentNullException>(() => tickets.GenerateAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task StartedEvent_CannotAcceptNewRegistration()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.Open();
        var user = await new ParticipantService(db).CreateAsync(new CreateParticipantRequest("Участник"), CancellationToken.None);
        var ev = new EventRecord
        {
            Id = Guid.NewGuid(),
            Title = "Прошедшее мероприятие",
            Location = "Зал",
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            TotalCapacity = 1,
            AvailableSeats = 1
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        var service = new RegistrationService(db, new EventService(db), new TicketService(db), NullLogger<RegistrationService>.Instance);
        var error = await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(new CreateRegistrationRequest(user.Id, ev.Id), CancellationToken.None));
        Assert.Equal("EVENT_STARTED", error.Code);
        Assert.Equal(1, (await new EventService(db).GetAsync(ev.Id, CancellationToken.None)).AvailableSeats);
    }
}
