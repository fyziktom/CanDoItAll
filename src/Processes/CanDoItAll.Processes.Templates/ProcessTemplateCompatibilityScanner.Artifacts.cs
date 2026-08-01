using System.Text.Json;

namespace CanDoItAll.Processes.Templates;

public static partial class ProcessTemplateCompatibilityScanner
{
    private static async Task<IReadOnlyList<ProcessArtifactContractDiagnostic>> AnalyzeArtifactContractsAsync(
        string root,
        TemplateProcessEntry processEntry,
        CancellationToken cancellationToken)
    {
        var artifactRoot = Path.Combine(Path.GetFullPath(Path.Combine(root, processEntry.RelativePath)), "artifacts");
        if (!Directory.Exists(artifactRoot))
        {
            return [];
        }

        var diagnostics = new List<ProcessArtifactContractDiagnostic>();
        foreach (var artifactPath in Directory.EnumerateFiles(artifactRoot, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var document = await ReadJsonDocumentAsync(artifactPath, cancellationToken).ConfigureAwait(false);
            var artifact = document.RootElement;
            var artifactKey = TryGetString(artifact, "key", out var key)
                ? key
                : Path.GetFileNameWithoutExtension(artifactPath);

            if (!TryGetProperty(artifact, "semanticAcceptanceContract", out var contract) ||
                contract.ValueKind != JsonValueKind.Object)
            {
                AddArtifactDiagnostic(
                    diagnostics,
                    processEntry.Key,
                    artifactKey,
                    ProcessArtifactContractDiagnosticKind.MissingSemanticAcceptanceContract,
                    "Artifact template must declare semanticAcceptanceContract so file existence cannot satisfy acceptance.");
                continue;
            }

            ValidateArtifactSemanticAcceptanceContract(processEntry.Key, artifactKey, contract, diagnostics);
        }

        return diagnostics;
    }

    private static void ValidateArtifactSemanticAcceptanceContract(
        string processKey,
        string artifactKey,
        JsonElement contract,
        List<ProcessArtifactContractDiagnostic> diagnostics)
    {
        if (!TryGetString(contract, "acceptanceMode", out var acceptanceMode) ||
            !string.Equals(acceptanceMode, "SemanticReview", StringComparison.OrdinalIgnoreCase))
        {
            AddArtifactDiagnostic(
                diagnostics,
                processKey,
                artifactKey,
                ProcessArtifactContractDiagnosticKind.InvalidSemanticAcceptanceContract,
                "semanticAcceptanceContract.acceptanceMode must be SemanticReview.");
        }

        if (!TryGetProperty(contract, "fileExistenceIsSufficient", out var fileOnly) ||
            fileOnly.ValueKind != JsonValueKind.False)
        {
            AddArtifactDiagnostic(
                diagnostics,
                processKey,
                artifactKey,
                ProcessArtifactContractDiagnosticKind.FileOnlyAcceptanceAllowed,
                "semanticAcceptanceContract.fileExistenceIsSufficient must be false.");
        }

        if (!TryGetString(contract, "requiredArtifactSlotKey", out _))
        {
            AddArtifactDiagnostic(
                diagnostics,
                processKey,
                artifactKey,
                ProcessArtifactContractDiagnosticKind.MissingArtifactSlot,
                "semanticAcceptanceContract.requiredArtifactSlotKey is required.");
        }

        if (!TryGetProperty(contract, "requiredEvidenceKinds", out var evidenceKinds) ||
            evidenceKinds.ValueKind != JsonValueKind.Array ||
            evidenceKinds.GetArrayLength() < 3)
        {
            AddArtifactDiagnostic(
                diagnostics,
                processKey,
                artifactKey,
                ProcessArtifactContractDiagnosticKind.MissingEvidenceKinds,
                "semanticAcceptanceContract.requiredEvidenceKinds must declare at least three evidence kinds.");
        }

        if (!TryGetString(contract, "requiredReviewSummary", out _))
        {
            AddArtifactDiagnostic(
                diagnostics,
                processKey,
                artifactKey,
                ProcessArtifactContractDiagnosticKind.InvalidSemanticAcceptanceContract,
                "semanticAcceptanceContract.requiredReviewSummary is required.");
        }
    }

    private static void AddArtifactDiagnostic(
        List<ProcessArtifactContractDiagnostic> diagnostics,
        string processKey,
        string artifactKey,
        ProcessArtifactContractDiagnosticKind kind,
        string message)
    {
        diagnostics.Add(new ProcessArtifactContractDiagnostic(processKey, artifactKey, kind, message));
    }
}
