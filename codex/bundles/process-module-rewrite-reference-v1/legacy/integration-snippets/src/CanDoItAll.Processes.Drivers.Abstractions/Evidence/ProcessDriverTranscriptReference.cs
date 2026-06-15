namespace CanDoItAll.Processes.Drivers.Abstractions.Evidence;

public sealed record ProcessDriverTranscriptReference(
    string Uri,
    string TranscriptHash,
    ProcessDriverTranscriptLanguage Language,
    string ToolchainName,
    string TargetFramework);

public enum ProcessDriverTranscriptLanguage
{
    DotNet = 1,
    Rust = 2
}
