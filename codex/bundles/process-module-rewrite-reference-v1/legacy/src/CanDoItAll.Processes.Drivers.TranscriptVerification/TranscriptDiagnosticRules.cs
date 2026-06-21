using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.TranscriptVerification;

internal readonly record struct TranscriptDiagnosticRule(
    ProcessDriverDiagnosticSeverity Severity,
    ProcessDriverDiagnosticCategory Category,
    string Message,
    IReadOnlyList<string> Markers);

internal static class TranscriptDiagnosticRules
{
    public static readonly IReadOnlyList<TranscriptDiagnosticRule> DotNet =
    [
        new(
            ProcessDriverDiagnosticSeverity.Warning,
            ProcessDriverDiagnosticCategory.BuildWarning,
            "A .NET build warning marker was found in the transcript.",
            ["warning CS", "warning MSB", "warning NETSDK"]),
        new(
            ProcessDriverDiagnosticSeverity.Warning,
            ProcessDriverDiagnosticCategory.NullableWarning,
            "A nullable-reference warning marker was found in the transcript.",
            ["CS8618", "nullable"]),
        new(
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.BuildError,
            "A .NET build error marker was found in the transcript.",
            ["error CS", "Build FAILED", "error MSB"]),
        new(
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.TestFailure,
            "A .NET test failure marker was found in the transcript.",
            ["Test Failed", "Failed ", "Failed!"]),
        new(
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.UnsupportedTargetFramework,
            "An unsupported .NET target framework marker was found in the transcript.",
            ["NETSDK1045", "unsupported target framework", "does not support targeting"]),
        new(
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.MissingArtifact,
            "A missing proof artifact marker was found in the transcript.",
            ["missing artifact", "artifact missing"]),
        new(
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.RuntimeProofGap,
            "A runtime proof gap marker was found in the transcript.",
            ["runtime proof gap", "proof gap"]),
        new(
            ProcessDriverDiagnosticSeverity.Warning,
            ProcessDriverDiagnosticCategory.PlatformCompatibilityWarning,
            "A platform compatibility warning marker was found in the transcript.",
            ["CA1416", "platform compatibility"]),
        new(
            ProcessDriverDiagnosticSeverity.Warning,
            ProcessDriverDiagnosticCategory.AnalyzerWarning,
            "An analyzer warning marker was found in the transcript.",
            ["analyzer", "CA"])
    ];

    public static readonly IReadOnlyList<TranscriptDiagnosticRule> Rust =
    [
        new(
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.CompileError,
            "A Rust compile error marker was found in the transcript.",
            ["error[", "error:", "could not compile"]),
        new(
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.CargoTestFailure,
            "A Rust cargo test failure marker was found in the transcript.",
            ["test result: FAILED", "failures:", "FAILED."]),
        new(
            ProcessDriverDiagnosticSeverity.Warning,
            ProcessDriverDiagnosticCategory.ClippyWarning,
            "A Rust clippy warning marker was found in the transcript.",
            ["clippy", "clippy::"]),
        new(
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.MissingCargoArtifact,
            "A missing cargo artifact marker was found in the transcript.",
            ["missing cargo artifact", "target/debug/deps"]),
        new(
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.UnsupportedToolchain,
            "An unsupported Rust toolchain marker was found in the transcript.",
            ["unsupported toolchain", "toolchain unsupported"]),
        new(
            ProcessDriverDiagnosticSeverity.Error,
            ProcessDriverDiagnosticCategory.PanicDetected,
            "A Rust panic marker was found in the transcript.",
            ["panicked at", "thread '"])
    ];
}

internal static class TranscriptDiagnosticRuleEvaluator
{
    public static IReadOnlyList<ProcessDriverDiagnostic> Evaluate(
        IReadOnlyList<TranscriptDiagnosticRule> rules,
        string transcriptText,
        ProcessDriverEvidenceReference evidence,
        ProcessDriverRedactionResult redaction)
    {
        return rules
            .Where(rule => ContainsAny(transcriptText, rule.Markers))
            .Select(rule => TranscriptVerificationDiagnosticFactory.Create(
                rule.Severity,
                rule.Category,
                rule.Message,
                evidence,
                redaction))
            .ToArray();
    }

    private static bool ContainsAny(string value, IReadOnlyList<string> markers)
    {
        return markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
