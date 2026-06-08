using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.BusinessAnalysis;

internal static class BusinessAnalysisDiagnosticRules
{
    public static IReadOnlyList<ProcessDriverDiagnostic> Evaluate(
        IReadOnlyList<BusinessAnalysisEvidenceItem> items,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        var diagnostics = new List<ProcessDriverDiagnostic>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ItemId))
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic("Business analysis item is missing an item id.", primaryEvidence));
            }

            if (string.IsNullOrWhiteSpace(item.Title))
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic("Business analysis item is missing title metadata.", primaryEvidence));
            }

            if (string.IsNullOrWhiteSpace(item.Text))
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic("Business analysis item is missing supplied text content.", primaryEvidence));
            }

            if (item.ObservedAt is null)
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic("Business analysis item is missing observed timestamp metadata.", primaryEvidence));
            }
        }

        if (!items.Any(item => item.Kind == BusinessAnalysisEvidenceItemKind.Deliverable))
        {
            diagnostics.Add(CreateMissingEvidenceDiagnostic("Business analysis evidence is missing a supplied deliverable item.", primaryEvidence));
        }

        if (items.Any(item => item.Kind == BusinessAnalysisEvidenceItemKind.Deliverable) &&
            !items.Any(item => item.Kind == BusinessAnalysisEvidenceItemKind.Deliverable &&
                ContainsAny(item.Text, "requirement:", "requirements:")))
        {
            diagnostics.Add(CreateBusinessDiagnostic(
                ProcessDriverDiagnosticCategory.BusinessRequirementMissing,
                "Business analysis deliverable is missing an explicit supplied requirement marker.",
                primaryEvidence));
        }

        if (!items.Any(item => item.Kind == BusinessAnalysisEvidenceItemKind.SupportingEvidence) ||
            !items.Any(item => item.Kind == BusinessAnalysisEvidenceItemKind.SupportingEvidence &&
                ContainsAny(item.Text, "evidence:", "source evidence:")))
        {
            diagnostics.Add(CreateBusinessDiagnostic(
                ProcessDriverDiagnosticCategory.BusinessEvidenceGap,
                "Business analysis evidence is missing an explicit supplied supporting evidence marker.",
                primaryEvidence));
        }

        if (items.Any(item => ContainsAny(item.Text, "assumption:", "unsupported assumption", "unverified assumption")))
        {
            diagnostics.Add(CreateBusinessDiagnostic(
                ProcessDriverDiagnosticCategory.BusinessUnsupportedAssumption,
                "Business analysis deliverable includes an unsupported assumption marker.",
                primaryEvidence));
        }

        if (items.Any(item => ContainsAny(item.Text, "contradiction:", "conflicts with", "inconsistent with")))
        {
            diagnostics.Add(CreateBusinessDiagnostic(
                ProcessDriverDiagnosticCategory.BusinessContradictionMarker,
                "Business analysis deliverable includes a contradiction marker.",
                primaryEvidence));
        }

        return diagnostics;
    }

    private static ProcessDriverDiagnostic CreateMissingEvidenceDiagnostic(
        string message,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        return BusinessAnalysisDiagnosticFactory.Create(
            ProcessDriverDiagnosticSeverity.Warning,
            ProcessDriverDiagnosticCategory.InsufficientProof,
            message,
            primaryEvidence);
    }

    private static ProcessDriverDiagnostic CreateBusinessDiagnostic(
        ProcessDriverDiagnosticCategory category,
        string message,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        return BusinessAnalysisDiagnosticFactory.Create(
            ProcessDriverDiagnosticSeverity.Warning,
            category,
            message,
            primaryEvidence);
    }

    private static bool ContainsAny(string value, params string[] markers)
    {
        return markers.Any(marker =>
            value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
