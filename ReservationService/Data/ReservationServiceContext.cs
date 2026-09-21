using Microsoft.EntityFrameworkCore;
using ReservationService.Models;

namespace ReservationService.Data;

public class ReservationServiceContext : DbContext
{
    public ReservationServiceContext(DbContextOptions<ReservationServiceContext> options) : base(options) { }

    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
