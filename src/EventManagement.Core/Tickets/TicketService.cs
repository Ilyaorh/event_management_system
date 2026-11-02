using System.Security.Cryptography;
using EventManagement.Core.Contracts;
using EventManagement.Core.Data;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace EventManagement.Core.Tickets;

public sealed class TicketService(EventManagementDbContext db) : ITicketService
{
    public async Task<TicketDto> GenerateAsync(GenerateTicketRequest request, CancellationToken cancellationToken)
    {
        var registration = await db.Registrations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.RegistrationId, cancellationToken);
        if (registration is null || registration.EventId != request.EventId || registration.UserId != request.UserId || registration.Status != "Active")
            throw new DomainException("REGISTRATION_NOT_FOUND", "Активная регистрация не найдена.");
        if (await db.Tickets.AnyAsync(x => x.RegistrationId == request.RegistrationId, cancellationToken))
            throw new DomainException("TICKET_ALREADY_GENERATED", "Билет для регистрации уже создан.");
        var ticket = new TicketRecord
        {
            Id = Guid.NewGuid(), RegistrationId = request.RegistrationId,
            UniqueCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(24))
        };
        db.Tickets.Add(ticket);
        return ToDto(ticket);
    }

    public async Task<TicketDto> GetAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ticketId, cancellationToken)
            ?? throw new DomainException("TICKET_NOT_FOUND", "Билет не найден.");
        return ToDto(ticket);
    }

    public async Task CheckInAsync(string uniqueCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(uniqueCode))
            throw new DomainException("INVALID_TICKET", "Укажите код билета.");
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.UniqueCode == uniqueCode, cancellationToken)
            ?? throw new DomainException("TICKET_NOT_FOUND", "Билет не найден.");
        var changed = await db.Tickets.Where(x => x.Id == ticket.Id && x.Status == "Active")
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Used"), cancellationToken);
        if (changed != 1)
            throw new DomainException("TICKET_NOT_ACTIVE", "Билет уже использован или отменён.");
    }

    private static TicketDto ToDto(TicketRecord ticket)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(ticket.UniqueCode, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(5);
        return new TicketDto(ticket.Id, ticket.RegistrationId, ticket.UniqueCode, png, ticket.Status);
    }
}
