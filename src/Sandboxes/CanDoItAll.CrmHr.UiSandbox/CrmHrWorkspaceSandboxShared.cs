namespace CanDoItAll.CrmHr.UiSandbox;

// Deterministic identity helper shared by the workspace sandbox fixtures: the same FNV-1a folding the Home,
// account-activity and Financials fixtures already use, kept in one place instead of duplicated eight times.
internal static class CrmHrWorkspaceSandboxIds
{
    public static Guid Id(string seed, ulong salt = 0x5A5A5A5A5A5A5A5AUL)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var high = offset;
        var low = offset ^ salt;
        foreach (var character in seed)
        {
            high = (high ^ character) * prime;
            low = (low ^ (uint)(character * 31)) * prime;
        }

        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes[..8], high);
        BitConverter.TryWriteBytes(bytes[8..], low);
        return new Guid(bytes);
    }
}
