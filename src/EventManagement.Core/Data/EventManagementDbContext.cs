using Microsoft.EntityFrameworkCore;

namespace EventManagement.Core.Data;

public sealed class EventManagementDbContext(DbContextOptions<EventManagementDbContext> options) : DbContext(options)
{
    public DbSet<EventRecord> Events => Set<EventRecord>();
    public DbSet<ParticipantRecord> Participants => Set<ParticipantRecord>();
    public DbSet<RegistrationRecord> Registrations => Set<RegistrationRecord>();
    public DbSet<TicketRecord> Tickets => Set<TicketRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RegistrationRecord>().HasIndex(x => new { x.EventId, x.UserId })
            .IsUnique().HasFilter("\"Status\" = 'Active'");
        modelBuilder.Entity<RegistrationRecord>().HasOne<EventRecord>().WithMany()
            .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RegistrationRecord>().HasOne<ParticipantRecord>().WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TicketRecord>().HasIndex(x => x.RegistrationId).IsUnique();
        modelBuilder.Entity<TicketRecord>().HasIndex(x => x.UniqueCode).IsUnique();
        modelBuilder.Entity<TicketRecord>().HasOne<RegistrationRecord>().WithMany()
            .HasForeignKey(x => x.RegistrationId).OnDelete(DeleteBehavior.Restrict);
    }
}
