using EventManagement.Core.Contracts;
using EventManagement.Core.Events;
using EventManagement.Core.Registrations;
using EventManagement.Core.Tickets;
using EventManagement.Core.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventManagement.Tests;

public class InspectionTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Reserve_RejectsNullRequestExplicitly()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.Open();
        await Assert.ThrowsAsync<ArgumentNullException>(() => new EventService(db).ReserveAsync(null!, Ct));
    }

    [Fact]
    public async Task MissingParticipant_DoesNotConsumeSeat()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.Open();
        var ev = await new EventService(db).CreateAsync(new CreateEventRequest("Семинар", "", DateTimeOffset.UtcNow.AddDays(1), "Зал", 1), Ct);
        var registrations = new RegistrationService(db, new EventService(db), new TicketService(db), NullLogger<RegistrationService>.Instance);
        var error = await Assert.ThrowsAsync<DomainException>(() => registrations.CreateAsync(new CreateRegistrationRequest(Guid.NewGuid(), ev.Id), Ct));
        Assert.Equal("USER_NOT_FOUND", error.Code);
        Assert.Equal(1, (await new EventService(db).GetAsync(ev.Id, Ct)).AvailableSeats);
        Assert.Empty(await db.Registrations.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CancelledToken_StopsDatabaseRead()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.Open();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new EventService(db).ListAsync(cancellation.Token));
    }

    [Fact]
    public async Task ConcurrentRequests_OnlyOneTakesLastSeat()
    {
        using var fixture = new SqliteFixture();
        Guid eventId;
        Guid firstId;
        Guid secondId;
        await using (var setup = fixture.Open())
        {
            eventId = (await new EventService(setup).CreateAsync(new CreateEventRequest("Встреча", "", DateTimeOffset.UtcNow.AddDays(1), "Зал", 1), Ct)).Id;
            var users = new ParticipantService(setup);
            firstId = (await users.CreateAsync(new CreateParticipantRequest("Первый"), Ct)).Id;
            secondId = (await users.CreateAsync(new CreateParticipantRequest("Второй"), Ct)).Id;
        }
        using var gate = new ManualResetEventSlim(false);
        async Task<string> Attempt(Guid userId)
        {
            await using var db = fixture.Open();
            gate.Wait();
            var service = new RegistrationService(db, new EventService(db), new TicketService(db), NullLogger<RegistrationService>.Instance);
            try
            {
                await service.CreateAsync(new CreateRegistrationRequest(userId, eventId), Ct);
                return "SUCCESS";
            }
            catch (DomainException error)
            {
                return error.Code;
            }
        }
        var first = Task.Run(() => Attempt(firstId));
        var second = Task.Run(() => Attempt(secondId));
        gate.Set();
        var results = await Task.WhenAll(first, second);
        Assert.Equal(1, results.Count(x => x == "SUCCESS"));
        Assert.Equal(1, results.Count(x => x == "EVENT_FULL"));
        await using var verification = fixture.Open();
        Assert.Equal(0, (await new EventService(verification).GetAsync(eventId, Ct)).AvailableSeats);
        Assert.Single(await verification.Registrations.AsNoTracking().ToListAsync());
        Assert.Single(await verification.Tickets.AsNoTracking().ToListAsync());
    }
}
