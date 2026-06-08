namespace CanDoItAll.Processes.Drivers.Abstractions.Verification;

public readonly record struct ProcessDriverContractVersion(
    int Major,
    int Minor,
    int Patch)
{
    public static ProcessDriverContractVersion Current => new(1, 1, 0);
}
