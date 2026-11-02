using EventManagement.Core.Contracts;
using EventManagement.Core.Data;

namespace EventManagement.Core.Users;

public sealed class ParticipantService(EventManagementDbContext db)
{
    public async Task<ParticipantDto> CreateAsync(CreateParticipantRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("INVALID_PARTICIPANT", "Укажите имя участника.");
        var user = new ParticipantRecord { Id = Guid.NewGuid(), Name = request.Name.Trim() };
        db.Participants.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return new ParticipantDto(user.Id, user.Name);
    }
}
