namespace EventManagement.Core.Contracts;

public interface IEventCapacityService
{
    Task<ReserveSpotResult> ReserveAsync(ReserveSpotRequest request, CancellationToken cancellationToken);
    Task ReleaseAsync(Guid eventId, CancellationToken cancellationToken);
}

public interface ITicketService
{
    Task<TicketDto> GenerateAsync(GenerateTicketRequest request, CancellationToken cancellationToken);
    Task<TicketDto> GetAsync(Guid ticketId, CancellationToken cancellationToken);
    Task CheckInAsync(string uniqueCode, CancellationToken cancellationToken);
}
