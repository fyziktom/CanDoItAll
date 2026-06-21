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
        return TranscriptDiagnosticRuleEvaluator.Evaluate(
            TranscriptDiagnosticRules.DotNet,
            transcriptText,
            evidence,
            redaction);
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
        return TranscriptDiagnosticRuleEvaluator.Evaluate(
            TranscriptDiagnosticRules.Rust,
            transcriptText,
            evidence,
            redaction);
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
