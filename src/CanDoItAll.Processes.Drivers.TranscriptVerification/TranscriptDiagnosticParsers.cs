using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.TranscriptVerification;

internal interface ITranscriptDiagnosticParser
{
    ProcessDriverTranscriptLanguage Language { get; }

    IReadOnlyList<ProcessDriverDiagnostic> Parse(
        string transcriptText,
        ProcessDriverEvidenceReference evidence,
        ProcessDriverRedactionResult redaction);
}

internal sealed class DotNetTranscriptDiagnosticParser : ITranscriptDiagnosticParser
{
    public ProcessDriverTranscriptLanguage Language => ProcessDriverTranscriptLanguage.DotNet;

    public IReadOnlyList<ProcessDriverDiagnostic> Parse(
        string transcriptText,
        ProcessDriverEvidenceReference evidence,
        ProcessDriverRedactionResult redaction)
    {
        var diagnostics = new List<ProcessDriverDiagnostic>();
        AddIfContains(
            diagnostics,
            transcriptText,
            evidence,
            redaction,
            ProcessDriverDiagnosticSeverity.Warning,
            ProcessDriverDiagnosticCategory.BuildWarning,
            "A .NET build warning marker was found in the transcript.",
            "warning CS",
            "warning MSB",
            "warning NETSDK");
        AddIfContains(
            diagnostics,
            transcriptText,
            evidence,
            redaction,
            ProcessDriverDiagnosticSeverity.Warning,
            ProcessDriverDiagnosticCategory.NullableWarning,
            "A nullable-reference warning marker was found in the transcript.",
            "CS8618",
            "nullable");
        AddIfContains(
            diagnostics,
            transcriptText,
            evidence,
            redaction,
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.BuildError,
            "A .NET build error marker was found in the transcript.",
            "error CS",
            "Build FAILED",
            "error MSB");
        AddIfContains(
            diagnostics,
            transcriptText,
            evidence,
            redaction,
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.TestFailure,
            "A .NET test failure marker was found in the transcript.",
            "Test Failed",
            "Failed ",
            "Failed!");
        AddIfContains(
            diagnostics,
            transcriptText,
            evidence,
            redaction,
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.UnsupportedTargetFramework,
            "An unsupported .NET target framework marker was found in the transcript.",
            "NETSDK1045",
            "unsupported target framework",
            "does not support targeting");
        AddIfContains(
            diagnostics,
            transcriptText,
            evidence,
            redaction,
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.MissingArtifact,
            "A missing proof artifact marker was found in the transcript.",
            "missing artifact",
            "artifact missing");
        AddIfContains(
            diagnostics,
            transcriptText,
            evidence,
            redaction,
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.RuntimeProofGap,
            "A runtime proof gap marker was found in the transcript.",
            "runtime proof gap",
            "proof gap");
        AddIfContains(
            diagnostics,
            transcriptText,
            evidence,
            redaction,
            ProcessDriverDiagnosticSeverity.Warning,
            ProcessDriverDiagnosticCategory.PlatformCompatibilityWarning,
            "A platform compatibility warning marker was found in the transcript.",
            "CA1416",
            "platform compatibility");
        AddIfContains(
            diagnostics,
            transcriptText,
            evidence,
            redaction,
            ProcessDriverDiagnosticSeverity.Warning,
            ProcessDriverDiagnosticCategory.AnalyzerWarning,
            "An analyzer warning marker was found in the transcript.",
            "analyzer",
            "CA");

        return diagnostics;
    }

    private static void AddIfContains(
        List<ProcessDriverDiagnostic> diagnostics,
        string transcriptText,
        ProcessDriverEvidenceReference evidence,
        ProcessDriverRedactionResult redaction,
        ProcessDriverDiagnosticSeverity severity,
        ProcessDriverDiagnosticCategory category,
        string message,
        params string[] markers)
    {
        if (!ContainsAny(transcriptText, markers))
        {
            return;
        }

        diagnostics.Add(TranscriptVerificationDiagnosticFactory.Create(
            severity,
            category,
            message,
            evidence,
            redaction));
    }

    private static bool ContainsAny(string value, params string[] markers)
    {
        return markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class RustTranscriptDiagnosticParser : ITranscriptDiagnosticParser
{
    public ProcessDriverTranscriptLanguage Language => ProcessDriverTranscriptLanguage.Rust;

    public IReadOnlyList<ProcessDriverDiagnostic> Parse(
        string transcriptText,
        ProcessDriverEvidenceReference evidence,
        ProcessDriverRedactionResult redaction)
    {
        var diagnostics = new List<ProcessDriverDiagnostic>();
        AddIfContains(
            diagnostics,
            transcriptText,
            evidence,
            redaction,
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.CompileError,
            "A Rust compile error marker was found in the transcript.",
            "error[",
            "error:",
            "could not compile");
        AddIfContains(
            diagnostics,
            transcriptText,
            evidence,
            redaction,
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.CargoTestFailure,
            "A Rust cargo test failure marker was found in the transcript.",
            "test result: FAILED",
            "failures:",
            "FAILED.");
        AddIfContains(
            diagnostics,
            transcriptText,
            evidence,
            redaction,
            ProcessDriverDiagnosticSeverity.Warning,
            ProcessDriverDiagnosticCategory.ClippyWarning,
            "A Rust clippy warning marker was found in the transcript.",
            "clippy",
            "clippy::");
        AddIfContains(
            diagnostics,
            transcriptText,
            evidence,
            redaction,
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.MissingCargoArtifact,
            "A missing cargo artifact marker was found in the transcript.",
            "missing cargo artifact",
            "target/debug/deps");
        AddIfContains(
            diagnostics,
            transcriptText,
            evidence,
            redaction,
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.UnsupportedToolchain,
            "An unsupported Rust toolchain marker was found in the transcript.",
            "unsupported toolchain",
            "toolchain unsupported");
        AddIfContains(
            diagnostics,
            transcriptText,
            evidence,
            redaction,
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.PanicDetected,
            "A Rust panic marker was found in the transcript.",
            "panicked at",
            "thread '");

        return diagnostics;
    }

    private static void AddIfContains(
        List<ProcessDriverDiagnostic> diagnostics,
        string transcriptText,
        ProcessDriverEvidenceReference evidence,
        ProcessDriverRedactionResult redaction,
        ProcessDriverDiagnosticSeverity severity,
        ProcessDriverDiagnosticCategory category,
        string message,
        params string[] markers)
    {
        if (!ContainsAny(transcriptText, markers))
        {
            return;
        }

        diagnostics.Add(TranscriptVerificationDiagnosticFactory.Create(
            severity,
            category,
            message,
            evidence,
            redaction));
    }

    private static bool ContainsAny(string value, params string[] markers)
    {
        return markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class TranscriptDiagnosticParserSet
{
    private readonly IReadOnlyDictionary<ProcessDriverTranscriptLanguage, ITranscriptDiagnosticParser> parsers;

    public TranscriptDiagnosticParserSet()
        : this([new DotNetTranscriptDiagnosticParser(), new RustTranscriptDiagnosticParser()])
    {
    }

    private TranscriptDiagnosticParserSet(IReadOnlyList<ITranscriptDiagnosticParser> parsers)
    {
        this.parsers = parsers.ToDictionary(parser => parser.Language);
    }

    public IReadOnlyList<ProcessDriverDiagnostic> Parse(
        ProcessDriverTranscriptLanguage language,
        string transcriptText,
        ProcessDriverEvidenceReference evidence,
        ProcessDriverRedactionResult redaction)
    {
        return parsers.TryGetValue(language, out var parser)
            ? parser.Parse(transcriptText, evidence, redaction)
            :
            [
                TranscriptVerificationDiagnosticFactory.Create(
                    ProcessDriverDiagnosticSeverity.Error,
                    ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                    "Transcript language is not supported by the alpha verifier.",
                    evidence,
                    redaction)
            ];
    }
}
