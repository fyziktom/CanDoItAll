using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryUiProfileOrigin(ICanonicalRuntimeDatabase database) {
    private readonly Guid profileId = database.Profile.Profile.Id;
    private readonly long generation = database.Generation;
    public bool IsCurrent => profileId == database.Profile.Profile.Id && generation == database.Generation;
    public void RequireCurrent() {
        if (!IsCurrent) {
            throw new MemoryActionRefusedException("The database profile changed. Open a new Memory workspace before continuing.");
        }
    }
}
