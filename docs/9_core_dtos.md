9. Основные DTO проекта
Для передачи данных между слоями и модулями создаются DTO.

9.1. EventDto
public record EventDto(
    Guid Id,
    string Title,
    string Description,
    DateTime StartDate,
    string Location,
    int TotalCapacity,
    int AvailableSeats,
    string Status
);

9.2. CreateRegistrationRequest
public record CreateRegistrationRequest(
    Guid UserId,
    Guid EventId
);

9.3. RegistrationDto
public record RegistrationDto(
    Guid Id,
    Guid UserId,
    Guid EventId,
    DateTime RegisteredAt,
    string Status
);

9.4. ReserveSpotRequest
public record ReserveSpotRequest(
    Guid EventId,
    Guid RegistrationId
);

9.5. ReserveSpotResult
public record ReserveSpotResult(
    bool Success,
    string? ErrorCode
);

9.6. GenerateTicketRequest
public record GenerateTicketRequest(
    Guid RegistrationId,
    Guid UserId,
    Guid EventId
);

9.7. TicketDto
public record TicketDto(
    Guid Id,
    Guid RegistrationId,
    string UniqueCode,
    byte[] QrCodeImage,
    string Status
);