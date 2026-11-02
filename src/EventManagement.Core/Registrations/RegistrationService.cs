using EventManagement.Core.Contracts;
using EventManagement.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventManagement.Core.Registrations;

public sealed class RegistrationService(
    EventManagementDbContext db,
    IEventCapacityService capacity,
    ITicketService tickets,
    ILogger<RegistrationService> logger)
{
    public async Task<RegistrationWithTicket> CreateAsync(CreateRegistrationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (!await db.Participants.AnyAsync(x => x.Id == request.UserId, cancellationToken))
                throw new DomainException("USER_NOT_FOUND", "Участник не найден.");
            if (await db.Registrations.AnyAsync(x => x.UserId == request.UserId && x.EventId == request.EventId && x.Status == "Active", cancellationToken))
                throw new DomainException("DUPLICATE_REGISTRATION", "Участник уже зарегистрирован.");

            var registration = new RegistrationRecord
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                EventId = request.EventId,
                RegisteredAt = DateTimeOffset.UtcNow
            };
            var reservation = await capacity.ReserveAsync(new ReserveSpotRequest(request.EventId, registration.Id), cancellationToken);
            if (!reservation.Success)
                throw new DomainException(reservation.ErrorCode ?? "RESERVATION_FAILED", "Не удалось зарезервировать место.");

            db.Registrations.Add(registration);
            await db.SaveChangesAsync(cancellationToken);
            var ticket = await tickets.GenerateAsync(new GenerateTicketRequest(registration.Id, registration.UserId, registration.EventId), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Registration created: EventId={EventId}, RegistrationId={RegistrationId}", registration.EventId, registration.Id);
            return new RegistrationWithTicket(ToDto(registration), ticket);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task CancelAsync(Guid registrationId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var registration = await db.Registrations.SingleOrDefaultAsync(x => x.Id == registrationId, cancellationToken)
                ?? throw new DomainException("REGISTRATION_NOT_FOUND", "Регистрация не найдена.");
            if (registration.Status == "Cancelled")
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.RegistrationId == registrationId, cancellationToken)
                ?? throw new DomainException("TICKET_NOT_FOUND", "Билет регистрации не найден.");
            if (ticket.Status == "Used")
                throw new DomainException("ALREADY_CHECKED_IN", "Нельзя отменить регистрацию после прохода.");
            var changed = await db.Tickets.Where(x => x.Id == ticket.Id && x.Status == "Active")
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Cancelled"), cancellationToken);
            if (changed != 1)
                throw new DomainException("TICKET_NOT_ACTIVE", "Билет уже использован или отменён.");
            registration.Status = "Cancelled";
            await capacity.ReleaseAsync(registration.EventId, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Registration cancelled: EventId={EventId}, RegistrationId={RegistrationId}", registration.EventId, registration.Id);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private static RegistrationDto ToDto(RegistrationRecord x) => new(x.Id, x.UserId, x.EventId, x.RegisteredAt, x.Status);
}
