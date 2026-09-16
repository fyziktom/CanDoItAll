using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CanDoItAll.Infrastructure.Persistence;

public static class ApplicationManagedConcurrencyTokens {
    public static void Stamp(ChangeTracker changeTracker) {
        foreach (var entry in changeTracker.Entries<IHasConcurrencyToken>()) {
            if (entry.State == EntityState.Added) {
                if (entry.Entity.ConcurrencyToken == Guid.Empty) {
                    entry.Entity.ConcurrencyToken = Guid.NewGuid();
                }

                continue;
            }

            if (entry.State == EntityState.Modified) {
                entry.Entity.ConcurrencyToken = Guid.NewGuid();
            }
        }
    }
}
