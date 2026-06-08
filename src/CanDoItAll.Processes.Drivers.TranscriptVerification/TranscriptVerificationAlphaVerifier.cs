using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.TranscriptVerification;

public sealed class TranscriptVerificationAlphaVerifier
{
    public ProcessDriverVerificationResponse Verify(TranscriptVerificationAlphaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var verificationRequest = request.VerificationRequest ?? throw new ArgumentException(
            "Verification request is required.",
            nameof(request));
        var evidenceReferences = NormalizeEvidenceReferences(verificationRequest.EvidenceReferences);
        var transcriptEvidence = CreateTranscriptEvidenceReference(request.TranscriptReference, request.TranscriptText);
        var primaryEvidence = evidenceReferences.FirstOrDefault() ?? transcriptEvidence;
        var redaction = Redact(request.TranscriptText);
        var diagnostics = new List<ProcessDriverDiagnostic>();
        var denialReason = ValidateRequest(request, evidenceReferences, diagnostics, primaryEvidence);

        if (denialReason == ProcessDriverDenialReason.None)
        {
            diagnostics.AddRange(ParseTranscriptDiagnostics(
                request.TranscriptReference.Language,
                request.TranscriptText,
                primaryEvidence,
                redaction));
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Info,
                ProcessDriverDiagnosticCategory.NoIssueDetected,
                "Transcript verification found no known .NET or Rust diagnostic markers.",
                primaryEvidence,
                redaction));
        }

        var accepted = denialReason == ProcessDriverDenialReason.None;
        var redactionDescriptor = new ProcessDriverRedactionDescriptor(
            redaction.Status,
            redaction.AppliedKinds,
            ComputeSha256(redaction.RedactedText));
        var auditFacts = CreateAuditFacts(
            request,
            diagnostics,
            redactionDescriptor,
            accepted,
            denialReason);

        return new ProcessDriverVerificationResponse(
            accepted,
            denialReason,
            diagnostics,
            evidenceReferences.Count == 0 ? [transcriptEvidence] : evidenceReferences,
            redactionDescriptor,
            NoMutationPerformed: true,
            auditFacts,
            ProcessDriverContractVersion.Current);
    }

    private static ProcessDriverDenialReason ValidateRequest(
        TranscriptVerificationAlphaRequest request,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        List<ProcessDriverDiagnostic> diagnostics,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        var verificationRequest = request.VerificationRequest;
        if (verificationRequest.PermissionMode == ProcessDriverPermissionMode.Unspecified)
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                "Verification request is missing a permission mode.",
                primaryEvidence,
                RedactionResult.None));

            return ProcessDriverDenialReason.MissingPermissionMode;
        }

        if (!ProcessDriverCapabilityScopeRules.IsDotNetRustTranscriptVerificationScope(
            verificationRequest.Scope,
            verificationRequest.PermissionMode))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                "Capability scope is not the read-only .NET/Rust transcript verification lane.",
                primaryEvidence,
                RedactionResult.None));

            return ProcessDriverDenialReason.CapabilityScopeDenied;
        }

        foreach (var operation in verificationRequest.RequestedOperations ?? [])
        {
            if (ProcessDriverOperationRules.IsReadonlyVerificationOperation(operation))
            {
                continue;
            }

            var denialReason = ProcessDriverOperationRules.ResolveReadonlyDenialReason(operation);
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.MutationAttemptDenied,
                $"Operation {operation} is denied for verification-only transcript inspection.",
                primaryEvidence,
                RedactionResult.None));

            return denialReason;
        }

        if ((verificationRequest.EvidenceReferences?.Count ?? 0) == 0)
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Verification requires at least one supplied evidence reference.",
                primaryEvidence,
                RedactionResult.None));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (string.IsNullOrWhiteSpace(request.TranscriptText))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.TranscriptMissing,
                "Verification requires supplied transcript content.",
                primaryEvidence,
                RedactionResult.None));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (evidenceReferences.Any(evidence => !IsSha256(evidence.ContentHash)))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Every evidence reference must include a valid SHA-256 content hash.",
                primaryEvidence,
                RedactionResult.None));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        var expectedTranscriptHash = NormalizeHash(request.TranscriptReference.TranscriptHash);
        if (!IsSha256(expectedTranscriptHash) || expectedTranscriptHash != ComputeSha256(request.TranscriptText))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.EvidenceHashMismatch,
                "Transcript content does not match the supplied transcript hash.",
                primaryEvidence,
                RedactionResult.None));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        return ProcessDriverDenialReason.None;
    }

    private static IReadOnlyList<ProcessDriverDiagnostic> ParseTranscriptDiagnostics(
        ProcessDriverTranscriptLanguage language,
        string transcriptText,
        ProcessDriverEvidenceReference evidence,
        RedactionResult redaction)
    {
        return language switch
        {
            ProcessDriverTranscriptLanguage.DotNet => ParseDotNetDiagnostics(transcriptText, evidence, redaction),
            ProcessDriverTranscriptLanguage.Rust => ParseRustDiagnostics(transcriptText, evidence, redaction),
            _ =>
            [
                CreateDiagnostic(
                    ProcessDriverDiagnosticSeverity.Error,
                    ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                    "Transcript language is not supported by the alpha verifier.",
                    evidence,
                    redaction)
            ]
        };
    }

    private static IReadOnlyList<ProcessDriverDiagnostic> ParseDotNetDiagnostics(
        string transcriptText,
        ProcessDriverEvidenceReference evidence,
        RedactionResult redaction)
    {
        var diagnostics = new List<ProcessDriverDiagnostic>();
        if (ContainsAny(transcriptText, "warning CS", "warning MSB", "warning NETSDK"))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Warning,
                ProcessDriverDiagnosticCategory.BuildWarning,
                "A .NET build warning marker was found in the transcript.",
                evidence,
                redaction));
        }

        if (ContainsAny(transcriptText, "CS8618", "nullable"))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Warning,
                ProcessDriverDiagnosticCategory.NullableWarning,
                "A nullable-reference warning marker was found in the transcript.",
                evidence,
                redaction));
        }

        if (ContainsAny(transcriptText, "error CS", "Build FAILED", "error MSB"))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.BuildError,
                "A .NET build error marker was found in the transcript.",
                evidence,
                redaction));
        }

        if (ContainsAny(transcriptText, "Test Failed", "Failed ", "Failed!"))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.TestFailure,
                "A .NET test failure marker was found in the transcript.",
                evidence,
                redaction));
        }

        if (ContainsAny(transcriptText, "NETSDK1045", "unsupported target framework", "does not support targeting"))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedTargetFramework,
                "An unsupported .NET target framework marker was found in the transcript.",
                evidence,
                redaction));
        }

        if (ContainsAny(transcriptText, "missing artifact", "artifact missing"))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.MissingArtifact,
                "A missing proof artifact marker was found in the transcript.",
                evidence,
                redaction));
        }

        if (ContainsAny(transcriptText, "runtime proof gap", "proof gap"))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.RuntimeProofGap,
                "A runtime proof gap marker was found in the transcript.",
                evidence,
                redaction));
        }

        if (ContainsAny(transcriptText, "CA1416", "platform compatibility"))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Warning,
                ProcessDriverDiagnosticCategory.PlatformCompatibilityWarning,
                "A platform compatibility warning marker was found in the transcript.",
                evidence,
                redaction));
        }

        if (ContainsAny(transcriptText, "analyzer", "CA"))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Warning,
                ProcessDriverDiagnosticCategory.AnalyzerWarning,
                "An analyzer warning marker was found in the transcript.",
                evidence,
                redaction));
        }

        return diagnostics;
    }

    private static IReadOnlyList<ProcessDriverDiagnostic> ParseRustDiagnostics(
        string transcriptText,
        ProcessDriverEvidenceReference evidence,
        RedactionResult redaction)
    {
        var diagnostics = new List<ProcessDriverDiagnostic>();
        if (ContainsAny(transcriptText, "error[", "error:", "could not compile"))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.CompileError,
                "A Rust compile error marker was found in the transcript.",
                evidence,
                redaction));
        }

        if (ContainsAny(transcriptText, "test result: FAILED", "failures:", "FAILED."))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.CargoTestFailure,
                "A Rust cargo test failure marker was found in the transcript.",
                evidence,
                redaction));
        }

        if (ContainsAny(transcriptText, "clippy", "clippy::"))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Warning,
                ProcessDriverDiagnosticCategory.ClippyWarning,
                "A Rust clippy warning marker was found in the transcript.",
                evidence,
                redaction));
        }

        if (ContainsAny(transcriptText, "missing cargo artifact", "target/debug/deps"))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.MissingCargoArtifact,
                "A missing cargo artifact marker was found in the transcript.",
                evidence,
                redaction));
        }

        if (ContainsAny(transcriptText, "unsupported toolchain", "toolchain unsupported"))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedToolchain,
                "An unsupported Rust toolchain marker was found in the transcript.",
                evidence,
                redaction));
        }

        if (ContainsAny(transcriptText, "panicked at", "thread '"))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.PanicDetected,
                "A Rust panic marker was found in the transcript.",
                evidence,
                redaction));
        }

        return diagnostics;
    }

    private static IReadOnlyList<ProcessDriverAuditFact> CreateAuditFacts(
        TranscriptVerificationAlphaRequest request,
        IReadOnlyList<ProcessDriverDiagnostic> diagnostics,
        ProcessDriverRedactionDescriptor redaction,
        bool accepted,
        ProcessDriverDenialReason denialReason)
    {
        var requestedOperations = request.VerificationRequest.RequestedOperations.Count == 0
            ? [ProcessDriverOperation.InspectExistingEvidence]
            : request.VerificationRequest.RequestedOperations;
        var diagnosticSummary = Redact(string.Join(
            " ",
            diagnostics.Select(diagnostic => diagnostic.Message))).RedactedText;
        var factKind = accepted
            ? ProcessDriverAuditFactKind.DiagnosticReturned
            : ProcessDriverAuditFactKind.OperationDenied;

        return requestedOperations
            .Select(operation => new ProcessDriverAuditFact(
                CreateStableAuditId(request, operation, denialReason),
                request.RequestedAt,
                factKind,
                request.VerificationRequest.CallerContext,
                request.VerificationRequest.PermissionMode,
                request.VerificationRequest.Scope,
                operation,
                denialReason,
                redaction,
                diagnosticSummary,
                ComputeSha256(diagnosticSummary)))
            .ToArray();
    }

    private static Guid CreateStableAuditId(
        TranscriptVerificationAlphaRequest request,
        ProcessDriverOperation operation,
        ProcessDriverDenialReason denialReason)
    {
        var material = string.Join(
            "|",
            request.RequestedAt.ToUnixTimeMilliseconds(),
            request.VerificationRequest.CallerContext,
            request.VerificationRequest.PermissionMode,
            request.VerificationRequest.Scope.Kind,
            operation,
            denialReason,
            request.TranscriptReference.Uri,
            NormalizeHash(request.TranscriptReference.TranscriptHash));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(material));

        return new Guid(bytes[..16]);
    }

    private static ProcessDriverDiagnostic CreateDiagnostic(
        ProcessDriverDiagnosticSeverity severity,
        ProcessDriverDiagnosticCategory category,
        string message,
        ProcessDriverEvidenceReference evidence,
        RedactionResult redaction)
    {
        return new ProcessDriverDiagnostic(
            severity,
            category,
            Redact(message, redaction).RedactedText,
            evidence);
    }

    private static IReadOnlyList<ProcessDriverEvidenceReference> NormalizeEvidenceReferences(
        IReadOnlyList<ProcessDriverEvidenceReference>? evidenceReferences)
    {
        var normalized = new List<ProcessDriverEvidenceReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var evidenceReference in evidenceReferences ?? [])
        {
            var normalizedReference = new ProcessDriverEvidenceReference(
                evidenceReference.Kind,
                evidenceReference.Uri.Trim(),
                NormalizeHash(evidenceReference.ContentHash),
                evidenceReference.CoreDescriptorFamily);
            var key = $"{normalizedReference.Kind}|{normalizedReference.Uri}|{normalizedReference.ContentHash}";
            if (seen.Add(key))
            {
                normalized.Add(normalizedReference);
            }
        }

        return normalized;
    }

    private static ProcessDriverEvidenceReference CreateTranscriptEvidenceReference(
        ProcessDriverTranscriptReference transcriptReference,
        string transcriptText)
    {
        var transcriptHash = NormalizeHash(transcriptReference.TranscriptHash);
        return new ProcessDriverEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CommandTranscript,
            transcriptReference.Uri.Trim(),
            IsSha256(transcriptHash) ? transcriptHash : ComputeSha256(transcriptText),
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);
    }

    private static RedactionResult Redact(string value, RedactionResult? existing = null)
    {
        var appliedKinds = new HashSet<ProcessDriverRedactionKind>(existing?.AppliedKinds ?? []);
        var redacted = value;
        var secretRedacted = SecretPattern.Replace(redacted, "[redacted-secret]");
        if (!string.Equals(secretRedacted, redacted, StringComparison.Ordinal))
        {
            appliedKinds.Add(ProcessDriverRedactionKind.Secret);
            redacted = secretRedacted;
        }

        var emailRedacted = EmailPattern.Replace(redacted, "[redacted-email]");
        if (!string.Equals(emailRedacted, redacted, StringComparison.Ordinal))
        {
            appliedKinds.Add(ProcessDriverRedactionKind.EmailAddress);
            redacted = emailRedacted;
        }

        return appliedKinds.Count == 0
            ? new RedactionResult(ProcessDriverRedactionStatus.None, [], redacted)
            : new RedactionResult(ProcessDriverRedactionStatus.Redacted, appliedKinds.Order().ToArray(), redacted);
    }

    private static bool ContainsAny(string value, params string[] markers)
    {
        return markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string ComputeSha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static string NormalizeHash(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static bool IsSha256(string value)
    {
        return Sha256Pattern.IsMatch(value);
    }

    private static readonly Regex SecretPattern = new(
        @"(?i)\b(token|password|secret|connectionstring|connection string)\s*[:=]\s*[^;\s]+",
        RegexOptions.CultureInvariant);

    private static readonly Regex EmailPattern = new(
        @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Sha256Pattern = new(
        "^[A-F0-9]{64}$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private sealed record RedactionResult(
        ProcessDriverRedactionStatus Status,
        IReadOnlyList<ProcessDriverRedactionKind> AppliedKinds,
        string RedactedText)
    {
        public static RedactionResult None { get; } = new(ProcessDriverRedactionStatus.None, [], string.Empty);
    }
}
