namespace EventManagement.Core.Data;

public sealed class EventRecord
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset StartDate { get; set; }
    public string Location { get; set; } = "";
    public int TotalCapacity { get; set; }
    public int AvailableSeats { get; set; }
    public string Status { get; set; } = "Published";
}

public sealed class ParticipantRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class RegistrationRecord
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public string Status { get; set; } = "Active";
}

public sealed class TicketRecord
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public string UniqueCode { get; set; } = "";
    public string Status { get; set; } = "Active";
}
