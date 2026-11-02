using EventManagement.Core.Contracts;
using EventManagement.Core.Data;
using EventManagement.Core.Events;
using EventManagement.Core.Registrations;
using EventManagement.Core.Tickets;
using EventManagement.Core.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventManagement.Tests;

public class RegistrationTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static RegistrationService Service(EventManagementDbContext db, ITicketService? tickets = null) =>
        new(db, new EventService(db), tickets ?? new TicketService(db), NullLogger<RegistrationService>.Instance);

    private static async Task<(EventDto Event, ParticipantDto User)> Prepare(EventManagementDbContext db, int capacity = 1)
    {
        var ev = await new EventService(db).CreateAsync(new CreateEventRequest("Конференция", "Учебная", DateTimeOffset.UtcNow.AddDays(7), "Аудитория", capacity), Ct);
        var user = await new ParticipantService(db).CreateAsync(new CreateParticipantRequest("Тестовый участник"), Ct);
        return (ev, user);
    }

    [Fact]
    public async Task InvalidCapacity_IsRejected()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.Open();
        var error = await Assert.ThrowsAsync<DomainException>(() => new EventService(db).CreateAsync(
            new CreateEventRequest("Встреча", "", DateTimeOffset.UtcNow.AddDays(1), "Зал", 0), Ct));
        Assert.Equal("INVALID_EVENT", error.Code);
    }

    [Fact]
    public async Task Registration_ReservesSeatAndCreatesPngTicket()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.Open();
        var (ev, user) = await Prepare(db);
        var result = await Service(db).CreateAsync(new CreateRegistrationRequest(user.Id, ev.Id), Ct);
        Assert.Equal("Active", result.Registration.Status);
        Assert.Equal("Active", result.Ticket.Status);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, result.Ticket.QrCodeImage.Take(8));
        Assert.Equal(0, (await new EventService(db).GetAsync(ev.Id, Ct)).AvailableSeats);
        Assert.Single(await db.Registrations.AsNoTracking().ToListAsync(Ct));
        Assert.Single(await db.Tickets.AsNoTracking().ToListAsync(Ct));
    }

    [Fact]
    public async Task DuplicateRegistration_DoesNotConsumeAnotherSeat()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.Open();
        var (ev, user) = await Prepare(db, 2);
        await Service(db).CreateAsync(new CreateRegistrationRequest(user.Id, ev.Id), Ct);
        var error = await Assert.ThrowsAsync<DomainException>(() => Service(db).CreateAsync(new CreateRegistrationRequest(user.Id, ev.Id), Ct));
        Assert.Equal("DUPLICATE_REGISTRATION", error.Code);
        Assert.Equal(1, (await new EventService(db).GetAsync(ev.Id, Ct)).AvailableSeats);
    }

    [Fact]
    public async Task FullEvent_RejectsAnotherParticipant()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.Open();
        var (ev, first) = await Prepare(db);
        var second = await new ParticipantService(db).CreateAsync(new CreateParticipantRequest("Второй участник"), Ct);
        await Service(db).CreateAsync(new CreateRegistrationRequest(first.Id, ev.Id), Ct);
        var error = await Assert.ThrowsAsync<DomainException>(() => Service(db).CreateAsync(new CreateRegistrationRequest(second.Id, ev.Id), Ct));
        Assert.Equal("EVENT_FULL", error.Code);
        Assert.Single(await db.Registrations.AsNoTracking().ToListAsync(Ct));
    }

    [Fact]
    public async Task Cancel_RestoresSeatOnlyOnceAndAllowsNewRegistration()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.Open();
        var (ev, user) = await Prepare(db);
        var service = Service(db);
        var first = await service.CreateAsync(new CreateRegistrationRequest(user.Id, ev.Id), Ct);
        await service.CancelAsync(first.Registration.Id, Ct);
        await service.CancelAsync(first.Registration.Id, Ct);
        Assert.Equal(1, (await new EventService(db).GetAsync(ev.Id, Ct)).AvailableSeats);
        Assert.Equal("Cancelled", (await new TicketService(db).GetAsync(first.Ticket.Id, Ct)).Status);
        var next = await service.CreateAsync(new CreateRegistrationRequest(user.Id, ev.Id), Ct);
        Assert.NotEqual(first.Registration.Id, next.Registration.Id);
    }

    [Fact]
    public async Task CheckIn_IsSingleUseAndPreventsCancellation()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.Open();
        var (ev, user) = await Prepare(db);
        var service = Service(db);
        var created = await service.CreateAsync(new CreateRegistrationRequest(user.Id, ev.Id), Ct);
        var tickets = new TicketService(db);
        await tickets.CheckInAsync(created.Ticket.UniqueCode, Ct);
        var repeated = await Assert.ThrowsAsync<DomainException>(() => tickets.CheckInAsync(created.Ticket.UniqueCode, Ct));
        Assert.Equal("TICKET_NOT_ACTIVE", repeated.Code);
        var cancelled = await Assert.ThrowsAsync<DomainException>(() => service.CancelAsync(created.Registration.Id, Ct));
        Assert.Equal("ALREADY_CHECKED_IN", cancelled.Code);
        Assert.Equal(0, (await new EventService(db).GetAsync(ev.Id, Ct)).AvailableSeats);
    }

    [Fact]
    public async Task TicketFailure_RollsBackRegistrationAndSeat()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.Open();
        var (ev, user) = await Prepare(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, new FailingTicketService()).CreateAsync(new CreateRegistrationRequest(user.Id, ev.Id), Ct));
        Assert.Equal(1, (await new EventService(db).GetAsync(ev.Id, Ct)).AvailableSeats);
        Assert.Empty(await db.Registrations.AsNoTracking().ToListAsync(Ct));
        Assert.Empty(await db.Tickets.AsNoTracking().ToListAsync(Ct));
    }

    private sealed class FailingTicketService : ITicketService
    {
        public Task<TicketDto> GenerateAsync(GenerateTicketRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("Учебный сбой выпуска");
        public Task<TicketDto> GetAsync(Guid ticketId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CheckInAsync(string uniqueCode, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
