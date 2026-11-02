namespace EventManagement.Core.Contracts;

public sealed record CreateEventRequest(string Title, string Description, DateTimeOffset StartDate, string Location, int TotalCapacity);
public sealed record EventDto(Guid Id, string Title, string Description, DateTimeOffset StartDate, string Location, int TotalCapacity, int AvailableSeats, string Status);
public sealed record CreateParticipantRequest(string Name);
public sealed record ParticipantDto(Guid Id, string Name);
public sealed record CreateRegistrationRequest(Guid UserId, Guid EventId);
public sealed record RegistrationDto(Guid Id, Guid UserId, Guid EventId, DateTimeOffset RegisteredAt, string Status);
public sealed record ReserveSpotRequest(Guid EventId, Guid RegistrationId);
public sealed record ReserveSpotResult(bool Success, string? ErrorCode);
public sealed record GenerateTicketRequest(Guid RegistrationId, Guid UserId, Guid EventId);
public sealed record TicketDto(Guid Id, Guid RegistrationId, string UniqueCode, byte[] QrCodeImage, string Status);
public sealed record RegistrationWithTicket(RegistrationDto Registration, TicketDto Ticket);
public sealed record CheckInRequest(string UniqueCode);
