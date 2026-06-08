using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessAgentExecutionBoundaryArchitectureTests
{
    [Fact]
    public void Process_core_seed_is_narrow_and_driver_pack_projects_are_not_introduced_prematurely()
    {
        var root = FindRepositoryRoot();
        var projectFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToArray();

        Assert.Contains(projectFiles, name =>
            string.Equals(name, "CanDoItAll.Processes.Core", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(projectFiles, name =>
            string.Equals(name, "CanDoItAll.Modules.Processes.Core", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("ProcessDriver", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("DriverPack", StringComparison.OrdinalIgnoreCase));
        AssertNarrowProcessCoreSeed(root);
    }

    [Fact]
    public void Process_core_pure_rules_expansion_stays_inside_approved_namespaces_without_side_effect_dependencies()
    {
        var root = FindRepositoryRoot();
        var coreRoot = Path.Combine(root, "src", "CanDoItAll.Processes.Core");
        var coreProject = File.ReadAllText(Path.Combine(coreRoot, "CanDoItAll.Processes.Core.csproj"));
        var coreSource = ReadNarrowProcessCoreSource(root);
        var topLevelDirectories = Directory
            .EnumerateDirectories(coreRoot)
            .Select(Path.GetFileName)
            .Where(name => !string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase))
            .Where(name => !string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase))
            .Select(static name => name!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var forbiddenCoreTokens = new[]
        {
            "Microsoft.EntityFrameworkCore",
            "DbContext",
            "AppDbContext",
            "CanDoItAll.Modules",
            "CanDoItAll.Infrastructure",
            "CanDoItAll.AgentFramework",
            "IStorage",
            "StoragePlacement",
            "Workspace",
            "File.",
            "Directory.",
            "IServiceScopeFactory",
            "TransitionStep",
            "DispatchClaim",
            "ProcessDispatchFinalizer",
            "FinalizeSubprocess",
            "FinalizeWorkflow",
            "FinalizeDirectAgent",
            "IProcessDriverPack",
            "ProcessDriverRegistry",
            "DriverPack"
        };

        Assert.Equal(["Artifacts", "Diagnostics", "Execution", "Finalization", "Routing", "Subprocess"], topLevelDirectories);
        Assert.Contains(@"<ProjectReference Include=""..\CanDoItAll.Processes.Contracts\CanDoItAll.Processes.Contracts.csproj"" />", coreProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", coreProject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("namespace CanDoItAll.Processes.Core.Routing", coreSource, StringComparison.Ordinal);
        Assert.Contains("namespace CanDoItAll.Processes.Core.Subprocess", coreSource, StringComparison.Ordinal);
        Assert.Contains("namespace CanDoItAll.Processes.Core.Artifacts", coreSource, StringComparison.Ordinal);
        Assert.Contains("namespace CanDoItAll.Processes.Core.Diagnostics", coreSource, StringComparison.Ordinal);
        Assert.Contains("namespace CanDoItAll.Processes.Core.Execution", coreSource, StringComparison.Ordinal);
        Assert.Contains("namespace CanDoItAll.Processes.Core.Finalization", coreSource, StringComparison.Ordinal);
        Assert.Contains("public static class ProcessSubprocessLifecycleRules", coreSource, StringComparison.Ordinal);
        Assert.Contains("public static class ProcessSubprocessArtifactSourceResolver", coreSource, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessArtifactExpectationSnapshot", coreSource, StringComparison.Ordinal);
        Assert.Contains("public static class ProcessArtifactExpectationMatcher", coreSource, StringComparison.Ordinal);

        foreach (var forbiddenCoreToken in forbiddenCoreTokens)
        {
            Assert.DoesNotContain(forbiddenCoreToken, coreSource, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Process_core_public_api_surface_is_explicitly_guarded()
    {
        var expected = """
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchDiagnostic
Constructor:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchDiagnostic(System.Nullable<System.Guid>,CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchReason,System.Int32,System.Int32)
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchDiagnostic.KindMatchCount:System.Int32
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchDiagnostic.MatchedArtifactId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchDiagnostic.Reason:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchReason
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchDiagnostic.StrongMatchCount:System.Int32
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchReason
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchReason.StrongMatch
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchReason.NoStrongMatch
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchReason.AmbiguousStrongMatch
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchReason.KindDisambiguated
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchReason.AmbiguousKindMatch
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatcher
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatcher.DiagnoseStrongExpectedArtifactMatch(System.Collections.Generic.IReadOnlyList<CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot>,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind,System.Func<CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot,System.Boolean>):CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchDiagnostic
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatcher.DiagnoseStrongExpectedArtifactMatch(System.Collections.Generic.IReadOnlyList<TExpectation>,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind,System.Func<TExpectation,System.Boolean>,System.Func<TExpectation,System.Guid>,System.Func<TExpectation,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind>):CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchDiagnostic
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatcher.MatchStrongExpectedArtifactId(System.Collections.Generic.IReadOnlyList<CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot>,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind,System.Func<CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot,System.Boolean>):System.Nullable<System.Guid>
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatcher.MatchStrongExpectedArtifactId(System.Collections.Generic.IReadOnlyList<TExpectation>,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind,System.Func<TExpectation,System.Boolean>,System.Func<TExpectation,System.Guid>,System.Func<TExpectation,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind>):System.Nullable<System.Guid>
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionDiagnostic
Constructor:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionDiagnostic(System.Boolean,CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionReason)
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionDiagnostic.IsSatisfied:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionDiagnostic.Reason:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionReason
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionReason
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionReason.Satisfied
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionReason.ArtifactKindMismatch
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionReason.SensitivityTooLow
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionReason.TrustRequirementNotSatisfied
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionReason.ExpectationIdMismatch
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionReason.TitleMismatch
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionRules
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionRules.Diagnose(CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot,CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot):CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionDiagnostic
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionRules.SatisfiesArtifactExpectation(CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot,CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot):System.Boolean
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionRules.SatisfiesTrustRequirement(CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustStatus,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement):System.Boolean
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot
Constructor:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot(System.Guid,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind,System.String,System.Boolean,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement,CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel,System.String,System.String,System.Nullable<System.Guid>)
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot.AllowedFutureUsageSummary:System.String
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot.ArtifactKind:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot.Id:System.Guid
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot.IsRequired:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot.SensitivityLevel:CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot.SubprocessChildArtifactExpectationId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot.Title:System.String
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot.TrustRequirement:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot.ValidationRequirementSummary:System.String
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityDescriptor
Constructor:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityDescriptor(CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind,System.Boolean,System.Boolean)
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityDescriptor.IsRecordOnlySource:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityDescriptor.IsRuntimeEvidenceSource:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityDescriptor.ProducerKind:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityDescriptor.SourceKind:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityRules
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityRules.Describe(CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind):CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityDescriptor
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityRules.IsRecordOnlySource(CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind):System.Boolean
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityRules.IsRuntimeEvidenceSource(CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind):System.Boolean
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityRules.ResolveProducerKind(CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind):CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEvidenceDescriptorRules
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEvidenceDescriptorRules.DescribeLineage(CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.String,System.String,System.String):CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEvidenceDescriptorRules.DescribeProviderNativeBrowserEvidence(System.String,System.Boolean,System.Boolean):CanDoItAll.Processes.Core.Artifacts.ProcessProviderNativeBrowserEvidenceDescriptor
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind):CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionSourceOrderDescriptor
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEvidenceDescriptorRules.IsDefaultProjectionOrder(System.Collections.Generic.IReadOnlyList<CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind>):System.Boolean
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEvidenceDescriptorRules.ResolveProviderNativeBrowserEvidenceKind(System.String):CanDoItAll.Processes.Core.Artifacts.ProcessCoreProviderNativeBrowserEvidenceKind
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor
Constructor:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor(CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.String,System.String,System.String,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean)
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.ContentHash:System.String
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.HasRecordOnlySource:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.HasRecoveryLineage:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.HasRuntimeSource:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.HasSourceArtifact:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.IsProviderNativeBrowserEvidence:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.ProjectedExecutionRunId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.ProjectionIdentityHash:System.String
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.RecoveredForExecutionRunId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.RecoveryExecutionRunId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.ReworkPacketId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.SourceArtifactId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.SourceExecutionRunId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.SourceExternalReferenceKey:System.String
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.SourceKind:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.SubprocessRunId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.WorkflowArtifactId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor.WorkflowRunId:System.Nullable<System.Guid>
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionSourceOrderDescriptor
Constructor:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionSourceOrderDescriptor(CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind,System.Int32,System.Boolean,System.Boolean,System.Boolean,System.Boolean)
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionSourceOrderDescriptor.IsProviderNativeBrowserEvidence:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionSourceOrderDescriptor.IsRecordOnlySource:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionSourceOrderDescriptor.IsRuntimeEvidenceSource:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionSourceOrderDescriptor.ProducerKind:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionSourceOrderDescriptor.ProjectionOrder:System.Int32
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionSourceOrderDescriptor.RunsBeforeRecordOnlySources:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionSourceOrderDescriptor.SourceKind:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot
Constructor:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot(System.Guid,System.Nullable<System.Guid>,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind,System.String,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustStatus,CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel,System.DateTimeOffset)
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot.ArtifactExpectationId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot.ArtifactKind:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot.CreatedAtUtc:System.DateTimeOffset
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot.Id:System.Guid
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot.SensitivityLevel:CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot.Title:System.String
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot.TrustStatus:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustStatus
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordedSatisfactionRules
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordedSatisfactionRules.HasRecordedExpectedArtifact(System.Guid,System.Collections.Generic.IReadOnlySet<System.Guid>,System.Collections.Generic.IEnumerable<System.Nullable<System.Guid>>):System.Boolean
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationPolicyRules
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationPolicyRules.IsProducerAllowedForMode(CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind):System.Boolean
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationPolicyRules.RequiresManagedEvidencePath(CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind):System.Boolean
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationPolicyRules.RequiresStoredArtifactContent(System.Boolean,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind,System.String):System.Boolean
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptor
Constructor:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptor(System.Guid,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind,System.String,System.Boolean,System.String,System.String,CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode)
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptor.AllowedFutureUsageSummary:System.String
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptor.ArtifactKind:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptor.ExpectationId:System.Guid
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptor.IsRequired:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptor.Mode:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptor.Title:System.String
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptor.ValidationRequirementSummary:System.String
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptorRules
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptorRules.Describe(CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot):CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptor
Method:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptorRules.ResolveExpectationMode(CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot):CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode
Type:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationSnapshot
Constructor:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationSnapshot(System.Collections.Generic.IReadOnlyList<CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot>,System.String)
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationSnapshot.Empty:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationSnapshot
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationSnapshot.ExpectedArtifacts:System.Collections.Generic.IReadOnlyList<CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot>
Property:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationSnapshot.ProjectStructureContractText:System.String
Type:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.Narrative
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.Decision
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.Evidence
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.Deliverable
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.RuntimeProof
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.RecoveryDiagnostic
Type:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind.Brief
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind.Evidence
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind.Decision
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind.Deliverable
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind.Transcript
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind.Checklist
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind.Prompt
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind.Dataset
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind.Other
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind.DecisionRecord
Type:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.Unknown
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.AgentExecutionArtifact
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.FileWrite
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.ExistingManagedFile
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.AssistantResponse
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.CompletedDecision
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.ProcessMock
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.ProviderNativeBrowser
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.WorkflowRun
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.WorkflowArtifact
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.SubprocessArtifact
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.ManagerRecovery
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.Manual
Type:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.Unknown
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.AgentExecutionArtifact
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.FileWrite
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.ExistingManagedFile
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.AssistantResponse
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.WorkflowRun
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.WorkflowArtifact
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.SubprocessArtifact
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.CompletedDecision
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.ProcessMock
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.ProviderNativeBrowser
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.Manual
Type:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement.None
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement.ReviewRequired
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement.HumanApproved
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement.TrustedSource
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement.ApprovalRequired
Type:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustStatus
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustStatus.Draft
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustStatus.ReviewRequired
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustStatus.Approved
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustStatus.Rejected
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustStatus.TrustedSource
Type:CanDoItAll.Processes.Core.Artifacts.ProcessCoreProviderNativeBrowserEvidenceKind
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreProviderNativeBrowserEvidenceKind.Unknown
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreProviderNativeBrowserEvidenceKind.Screenshot
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreProviderNativeBrowserEvidenceKind.Snapshot
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreProviderNativeBrowserEvidenceKind.ConsoleLog
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreProviderNativeBrowserEvidenceKind.DomOrState
Type:CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel.Public
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel.Internal
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel.Confidential
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel.Restricted
Type:CanDoItAll.Processes.Core.Artifacts.ProcessProviderNativeBrowserEvidenceDescriptor
Constructor:CanDoItAll.Processes.Core.Artifacts.ProcessProviderNativeBrowserEvidenceDescriptor(CanDoItAll.Processes.Core.Artifacts.ProcessCoreProviderNativeBrowserEvidenceKind,System.String,System.Boolean,System.Boolean,System.Boolean)
Property:CanDoItAll.Processes.Core.Artifacts.ProcessProviderNativeBrowserEvidenceDescriptor.CanSatisfyRequiredArtifact:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessProviderNativeBrowserEvidenceDescriptor.EvidenceKind:CanDoItAll.Processes.Core.Artifacts.ProcessCoreProviderNativeBrowserEvidenceKind
Property:CanDoItAll.Processes.Core.Artifacts.ProcessProviderNativeBrowserEvidenceDescriptor.HasDeclaredPath:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessProviderNativeBrowserEvidenceDescriptor.HasMatchedOutput:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessProviderNativeBrowserEvidenceDescriptor.ToolName:System.String
Type:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnostic
Constructor:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnostic(CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot,CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnosticReason,System.String,System.Int32)
Property:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnostic.EligibleArtifactCount:System.Int32
Property:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnostic.HasSourceArtifact:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnostic.Message:System.String
Property:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnostic.Reason:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnosticReason
Property:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnostic.SourceArtifact:CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot
Method:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnostic.FromSource(CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot,CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnosticReason,System.Int32,System.String):CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnostic
Method:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnostic.None(CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnosticReason,System.String,System.Int32):CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnostic
Type:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnosticReason
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnosticReason.LatestEligibleMappedArtifact
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnosticReason.LegacyTextMapping
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnosticReason.LegacySameKindFallback
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnosticReason.AmbiguousMapping
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnosticReason.MissingExplicitMapping
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnosticReason.MissingEligibleMappedArtifact
Enum:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnosticReason.AmbiguousFallback
Type:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceResolver
Method:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceResolver.DiagnoseSourceArtifact(System.Collections.Generic.IReadOnlyList<CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot>,System.Collections.Generic.IReadOnlyList<CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot>,CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot):CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnostic
Method:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceResolver.IsCompletionProjectionAllowed(CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot):System.Boolean
Method:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceResolver.ResolveOutputArtifactMappings(System.Collections.Generic.IReadOnlyList<CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot>):System.Collections.Generic.IReadOnlyList<CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessOutputArtifactMapping>
Method:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceResolver.ResolveSourceArtifact(System.Collections.Generic.IReadOnlyList<CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot>,System.Collections.Generic.IReadOnlyList<CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot>,CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot,System.String&):CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot
Type:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessOutputArtifactMapping
Constructor:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessOutputArtifactMapping(System.Guid,System.Guid,System.Boolean)
Property:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessOutputArtifactMapping.ChildExpectationId:System.Guid
Property:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessOutputArtifactMapping.IsLegacyTextMapping:System.Boolean
Property:CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessOutputArtifactMapping.ParentExpectationId:System.Guid
Type:CanDoItAll.Processes.Core.Diagnostics.ProcessNoProgressRetryDiagnosticDescriptor
Constructor:CanDoItAll.Processes.Core.Diagnostics.ProcessNoProgressRetryDiagnosticDescriptor(System.Boolean,System.String,System.Nullable<System.Guid>,System.String,System.String,System.String,System.String)
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessNoProgressRetryDiagnosticDescriptor.ArtifactValidationFingerprint:System.String
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessNoProgressRetryDiagnosticDescriptor.ExecutionRunId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessNoProgressRetryDiagnosticDescriptor.Fingerprint:System.String
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessNoProgressRetryDiagnosticDescriptor.HasSignal:System.Boolean
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessNoProgressRetryDiagnosticDescriptor.MutationDelta:System.String
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessNoProgressRetryDiagnosticDescriptor.ProofDelta:System.String
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessNoProgressRetryDiagnosticDescriptor.ToolSignature:System.String
Type:CanDoItAll.Processes.Core.Diagnostics.ProcessProviderRepairDiagnosticDescriptor
Constructor:CanDoItAll.Processes.Core.Diagnostics.ProcessProviderRepairDiagnosticDescriptor(System.Boolean,System.Boolean,System.String,System.String,System.String,System.String,System.Int32)
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessProviderRepairDiagnosticDescriptor.AffectedAgentCount:System.Int32
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessProviderRepairDiagnosticDescriptor.FailedProviderName:System.String
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessProviderRepairDiagnosticDescriptor.FailureSummary:System.String
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessProviderRepairDiagnosticDescriptor.FallbackModel:System.String
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessProviderRepairDiagnosticDescriptor.FallbackProviderName:System.String
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessProviderRepairDiagnosticDescriptor.HasRecoverableProviderFailure:System.Boolean
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessProviderRepairDiagnosticDescriptor.HasRepairOutcome:System.Boolean
Type:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor
Constructor:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor(System.Boolean,System.Int32,System.Int32,System.Collections.Generic.IReadOnlyList<System.String>,System.String,System.Collections.Generic.IReadOnlyList<System.String>,System.Collections.Generic.IReadOnlyList<System.String>,System.Int32,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticFailureKind)
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.AttemptNumber:System.Int32
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.FailedToolNames:System.Collections.Generic.IReadOnlyList<System.String>
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.HasBuildFailure:System.Boolean
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.HasMissingRequiredTools:System.Boolean
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.HasRecoverableExecutionInterruption:System.Boolean
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.HasRecoverableFinalizerFailure:System.Boolean
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.HasRecoverableProviderFailure:System.Boolean
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.HasTestFailure:System.Boolean
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.HasUnresolvedCriticalToolFailures:System.Boolean
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.MaxExecutionAttempts:System.Int32
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.MissingRequiredTools:System.Collections.Generic.IReadOnlyList<System.String>
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.PrimaryFailureKind:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticFailureKind
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.RetryReasonSummary:System.String
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.RetryReasons:System.Collections.Generic.IReadOnlyList<System.String>
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.ShouldRetry:System.Boolean
Property:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor.UnresolvedCriticalToolFailureCount:System.Int32
Type:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptorRules
Method:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptorRules.DescribeNoProgressSignal(System.String,System.Guid,System.String,System.String,System.String,System.String):CanDoItAll.Processes.Core.Diagnostics.ProcessNoProgressRetryDiagnosticDescriptor
Method:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptorRules.DescribeNoProgressSignalAbsent():CanDoItAll.Processes.Core.Diagnostics.ProcessNoProgressRetryDiagnosticDescriptor
Method:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptorRules.DescribeProviderRepair(System.Boolean,System.String,System.String,System.String,System.String,System.Int32):CanDoItAll.Processes.Core.Diagnostics.ProcessProviderRepairDiagnosticDescriptor
Method:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptorRules.DescribeRetry(System.Boolean,System.Int32,System.Int32,System.Collections.Generic.IEnumerable<System.String>,System.Collections.Generic.IEnumerable<System.String>,System.Collections.Generic.IEnumerable<System.String>,System.Int32,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean):CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor
Type:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticFailureKind
Enum:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticFailureKind.None
Enum:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticFailureKind.MissingRequiredTool
Enum:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticFailureKind.CriticalToolFailure
Enum:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticFailureKind.BuildFailure
Enum:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticFailureKind.TestFailure
Enum:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticFailureKind.ProviderFailure
Enum:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticFailureKind.ExecutionInterruption
Enum:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticFailureKind.FinalizerValidationFailure
Enum:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticFailureKind.NoProgress
Enum:CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticFailureKind.Unknown
Type:CanDoItAll.Processes.Core.Execution.ProcessCoreExecutionRunObservationKind
Enum:CanDoItAll.Processes.Core.Execution.ProcessCoreExecutionRunObservationKind.Active
Enum:CanDoItAll.Processes.Core.Execution.ProcessCoreExecutionRunObservationKind.Succeeded
Enum:CanDoItAll.Processes.Core.Execution.ProcessCoreExecutionRunObservationKind.Failed
Enum:CanDoItAll.Processes.Core.Execution.ProcessCoreExecutionRunObservationKind.Cancelled
Enum:CanDoItAll.Processes.Core.Execution.ProcessCoreExecutionRunObservationKind.TerminalWithoutOutcome
Type:CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor
Constructor:CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor(System.Guid,System.Int32,CanDoItAll.Processes.Contracts.ProcessStepRunStatus,System.String,System.Collections.Generic.IReadOnlyList<System.String>,System.Boolean,System.Int32,System.Boolean,System.Int32,System.Nullable<System.Guid>)
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor.AttemptNumber:System.Int32
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor.CompletionReason:System.String
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor.CompletionStatus:CanDoItAll.Processes.Contracts.ProcessStepRunStatus
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor.ExecutionRunId:System.Guid
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor.HasMissingRequiredTools:System.Boolean
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor.HasUnresolvedCriticalToolFailures:System.Boolean
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor.MissingRequiredToolCount:System.Int32
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor.MissingRequiredTools:System.Collections.Generic.IReadOnlyList<System.String>
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor.SelectedBranchOutcomeId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor.UnresolvedCriticalToolFailureCount:System.Int32
Type:CanDoItAll.Processes.Core.Execution.ProcessExecutionCarriedProofDescriptor
Constructor:CanDoItAll.Processes.Core.Execution.ProcessExecutionCarriedProofDescriptor(System.Boolean,System.Boolean,System.Boolean)
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionCarriedProofDescriptor.HasConcreteImplementationProof:System.Boolean
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionCarriedProofDescriptor.HasConcreteProductMutation:System.Boolean
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionCarriedProofDescriptor.HasRunnableApplicationProof:System.Boolean
Type:CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptor
Constructor:CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptor(CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor,CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor,CanDoItAll.Processes.Core.Execution.ProcessExecutionCarriedProofDescriptor)
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptor.Attempt:CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptor.CarriedProof:CanDoItAll.Processes.Core.Execution.ProcessExecutionCarriedProofDescriptor
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptor.Run:CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor
Type:CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptorRules
Method:CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptorRules.DescribeAttempt(System.Guid,System.Int32,CanDoItAll.Processes.Contracts.ProcessStepRunStatus,System.String,System.Collections.Generic.IEnumerable<System.String>,System.Int32,System.Nullable<System.Guid>):CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor
Method:CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptorRules.DescribeCarriedProof(System.Boolean,System.Boolean,System.Boolean):CanDoItAll.Processes.Core.Execution.ProcessExecutionCarriedProofDescriptor
Method:CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptorRules.DescribeRun(CanDoItAll.Processes.Contracts.ProcessAutomationExecutionRunRecord):CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor
Method:CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptorRules.IsTerminalState(CanDoItAll.Processes.Contracts.ProcessAutomationExecutionState):System.Boolean
Method:CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptorRules.ResolveObservationKind(CanDoItAll.Processes.Contracts.ProcessAutomationExecutionState,System.Nullable<CanDoItAll.Processes.Contracts.ProcessAutomationRunOutcome>):CanDoItAll.Processes.Core.Execution.ProcessCoreExecutionRunObservationKind
Type:CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor
Constructor:CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor(System.Guid,CanDoItAll.Processes.Contracts.ProcessAutomationExecutionState,System.Nullable<CanDoItAll.Processes.Contracts.ProcessAutomationRunOutcome>,System.Boolean,System.Boolean,System.Boolean,System.DateTimeOffset,System.Nullable<System.DateTimeOffset>,System.Nullable<System.DateTimeOffset>,CanDoItAll.Processes.Core.Execution.ProcessCoreExecutionRunObservationKind)
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor.CompletedAtUtc:System.Nullable<System.DateTimeOffset>
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor.CreatedAtUtc:System.DateTimeOffset
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor.ExecutionRunId:System.Guid
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor.HasPendingToolApprovals:System.Boolean
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor.IsActive:System.Boolean
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor.IsTerminal:System.Boolean
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor.ObservationKind:CanDoItAll.Processes.Core.Execution.ProcessCoreExecutionRunObservationKind
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor.Outcome:System.Nullable<CanDoItAll.Processes.Contracts.ProcessAutomationRunOutcome>
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor.StartedAtUtc:System.Nullable<System.DateTimeOffset>
Property:CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor.State:CanDoItAll.Processes.Contracts.ProcessAutomationExecutionState
Type:CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerBlockCauseKind
Enum:CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerBlockCauseKind.None
Enum:CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerBlockCauseKind.OwnOutput
Enum:CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerBlockCauseKind.UpstreamInput
Enum:CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerBlockCauseKind.RuntimeEvidence
Enum:CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerBlockCauseKind.PolicyDenied
Type:CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerKind
Enum:CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerKind.DirectAgent
Enum:CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerKind.WorkflowBackedRole
Enum:CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerKind.SubprocessParent
Enum:CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerKind.ManagerArtifactRecovery
Enum:CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerKind.Manual
Type:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerEvidenceDescriptor
Constructor:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerEvidenceDescriptor(CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor,CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor)
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerEvidenceDescriptor.Intent:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerEvidenceDescriptor.Result:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor
Type:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerEvidenceDescriptorRules
Method:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerEvidenceDescriptorRules.DescribeAppliedResult(CanDoItAll.Processes.Contracts.ProcessStepRunStatus,System.String,CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerBlockCauseKind,System.Nullable<System.Guid>,System.Guid,System.Int32):CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor
Method:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerEvidenceDescriptorRules.DescribeIntent(CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerKind,System.Guid,System.Guid,CanDoItAll.Processes.Contracts.ProcessStepRunStatus,System.String,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Boolean,System.Boolean,System.String,System.Boolean,System.Nullable<System.Guid>,System.Nullable<System.Guid>):CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor
Method:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerEvidenceDescriptorRules.DescribeNoResult():CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor
Type:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor
Constructor:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor(CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerKind,System.Guid,System.Guid,CanDoItAll.Processes.Contracts.ProcessStepRunStatus,System.String,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Boolean,System.Boolean,System.String,System.Boolean,System.Nullable<System.Guid>,System.Nullable<System.Guid>)
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor.AllowsManagerArtifactRecovery:System.Boolean
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor.CompletionReason:System.String
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor.CompletionStatus:CanDoItAll.Processes.Contracts.ProcessStepRunStatus
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor.ExecutionRunId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor.FinalizerKind:CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerKind
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor.ProcessRunId:System.Guid
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor.ProjectsExecutionArtifacts:System.Boolean
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor.RecoveredForExecutionRunId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor.RecoveryExecutionRunId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor.RequiresLeaseRenewal:System.Boolean
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor.SelectedBranchOutcomeId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor.StepRunId:System.Guid
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor.SubprocessRunId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor.Trigger:System.String
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor.WorkflowRunId:System.Nullable<System.Guid>
Type:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor
Constructor:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor(System.Boolean,System.Boolean,System.Nullable<CanDoItAll.Processes.Contracts.ProcessStepRunStatus>,System.String,CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerBlockCauseKind,System.Nullable<System.Guid>,System.Nullable<System.Guid>,System.Int32,System.Boolean)
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor.ArtifactValidationResultCount:System.Int32
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor.BlockCauseKind:CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerBlockCauseKind
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor.CompletionReason:System.String
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor.CompletionStatus:System.Nullable<CanDoItAll.Processes.Contracts.ProcessStepRunStatus>
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor.HasArtifactValidationResults:System.Boolean
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor.HasResult:System.Boolean
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor.SelectedBranchOutcomeId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor.ShouldApplyTransition:System.Boolean
Property:CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor.StepRunConcurrencyToken:System.Nullable<System.Guid>
Type:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision
Constructor:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision(CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteKind)
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision.AgentExecution:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision.Continue:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision.DatabaseRequirement:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision.Kind:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteKind
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision.StrandedRecovery:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision.Subprocess:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision.UpstreamMaterialization:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision.Workflow:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision
Type:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecisionReason
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecisionReason.DatabaseRequirementFailure
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecisionReason.DatabaseRequirementNotFailed
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecisionReason.DatabaseRequirementIgnoredForSubprocess
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecisionReason.UpstreamMaterializationRequested
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecisionReason.UpstreamMaterializationNotRequested
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecisionReason.StrandedRecoveryCompleted
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecisionReason.StrandedRecoveryNotCompleted
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecisionReason.SubprocessStep
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecisionReason.NotSubprocessStep
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecisionReason.WorkflowHandled
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecisionReason.WorkflowNotHandled
Type:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDiagnostic
Constructor:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDiagnostic(CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision,CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecisionReason)
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDiagnostic.Decision:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDiagnostic.Reason:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecisionReason
Type:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteEligibility
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteEligibility.IsRunClosedToAutomation(System.Nullable<CanDoItAll.Processes.Contracts.ProcessRunStatus>,System.Nullable<CanDoItAll.Processes.Contracts.ProcessStepRunStatus>):System.Boolean
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteEligibility.IsRunEligibleForDispatchCandidate(System.Nullable<CanDoItAll.Processes.Contracts.ProcessRunStatus>):System.Boolean
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteEligibility.IsStepStatusDispatchableForRun(CanDoItAll.Processes.Contracts.ProcessRunStatus,CanDoItAll.Processes.Contracts.ProcessStepRunStatus):System.Boolean
Type:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteKind
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteKind.Continue
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteKind.DatabaseRequirement
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteKind.UpstreamMaterialization
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteKind.StrandedRecovery
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteKind.Subprocess
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteKind.Workflow
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteKind.AgentExecution
Type:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteOrderAssertion
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteOrderAssertion.ThrowIfStageOrderInvalid(System.Collections.Generic.IReadOnlyList<CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteStage>):System.Void
Type:CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePipeline
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePipeline.StageOrder:System.Collections.Generic.IReadOnlyList<CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteStage>
Type:CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePlanner
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePlanner.DiagnoseDatabaseRequirement(CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot,System.Boolean):CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDiagnostic
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePlanner.DiagnoseStrandedRecovery(System.Boolean):CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDiagnostic
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePlanner.DiagnoseSubprocess(CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot):CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDiagnostic
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePlanner.DiagnoseUpstreamMaterialization(System.Boolean):CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDiagnostic
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePlanner.DiagnoseWorkflow(System.Boolean):CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDiagnostic
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePlanner.ResolveDatabaseRequirement(CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot,System.Boolean):CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePlanner.ResolveStrandedRecovery(System.Boolean):CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePlanner.ResolveSubprocess(CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot):CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePlanner.ResolveUpstreamMaterialization(System.Boolean):CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePlanner.ResolveWorkflow(System.Boolean):CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision
Type:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot
Constructor:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot(System.Guid,System.Guid,CanDoItAll.Processes.Contracts.ProcessRunStatus,CanDoItAll.Processes.Contracts.ProcessStepRunStatus,CanDoItAll.Processes.Contracts.ProcessStepKind,System.Guid,System.Nullable<System.Guid>,System.Nullable<System.DateTimeOffset>,CanDoItAll.Processes.Core.Routing.ProcessDispatchTriggerFacts)
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.CurrentAttemptStartedAtUtc:System.Nullable<System.DateTimeOffset>
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.HasRecoverableExecutionRun:System.Boolean
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.IsRunEligibleForDispatchCandidate:System.Boolean
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.IsStepStatusDispatchableForRun:System.Boolean
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.IsSubprocess:System.Boolean
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.ProcessRunId:System.Guid
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.RecoveryExecutionRunId:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.RequiresStartTransition:System.Boolean
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.RunStatus:CanDoItAll.Processes.Contracts.ProcessRunStatus
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.StepKind:CanDoItAll.Processes.Contracts.ProcessStepKind
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.StepRunId:System.Guid
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.StepStatus:CanDoItAll.Processes.Contracts.ProcessStepRunStatus
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.TechnicalAgentId:System.Guid
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.Trigger:CanDoItAll.Processes.Core.Routing.ProcessDispatchTriggerFacts
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.UsesAgentAutomation:System.Boolean
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot.Create(System.Guid,System.Guid,CanDoItAll.Processes.Contracts.ProcessRunStatus,CanDoItAll.Processes.Contracts.ProcessStepRunStatus,CanDoItAll.Processes.Contracts.ProcessStepKind,System.Guid,System.Nullable<System.Guid>,System.Nullable<System.DateTimeOffset>,System.String,System.Nullable<System.Guid>):CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot
Type:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteStage
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteStage.FreshRecoverySkip
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteStage.DatabaseRequirement
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteStage.UpstreamMaterialization
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteStage.StrandedArtifactRecovery
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteStage.Subprocess
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteStage.StartTransition
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteStage.Workflow
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteStage.DirectAgentExecution
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteStage.CompetingExecutionGuard
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteStage.RunClosedGuard
Enum:CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteStage.FinalizerTransition
Type:CanDoItAll.Processes.Core.Routing.ProcessDispatchStepTransitionIntent
Constructor:CanDoItAll.Processes.Core.Routing.ProcessDispatchStepTransitionIntent(System.Guid,System.Nullable<System.Guid>,CanDoItAll.Processes.Contracts.ProcessStepRunStatus,System.String,System.String,System.Boolean)
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchStepTransitionIntent.DecidedBy:System.String
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchStepTransitionIntent.Reason:System.String
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchStepTransitionIntent.StepRunConcurrencyToken:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchStepTransitionIntent.StepRunId:System.Guid
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchStepTransitionIntent.SuppressAutomationDispatch:System.Boolean
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchStepTransitionIntent.TargetStatus:CanDoItAll.Processes.Contracts.ProcessStepRunStatus
Type:CanDoItAll.Processes.Core.Routing.ProcessDispatchTransitionIntentRules
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchTransitionIntentRules.BuildStartTransitionIntent(CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot,System.Guid,System.String):CanDoItAll.Processes.Core.Routing.ProcessDispatchStepTransitionIntent
Type:CanDoItAll.Processes.Core.Routing.ProcessDispatchTriggerFacts
Constructor:CanDoItAll.Processes.Core.Routing.ProcessDispatchTriggerFacts(System.String,System.Nullable<System.Guid>,System.String)
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchTriggerFacts.NormalizedTrigger:System.String
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchTriggerFacts.RawTrigger:System.String
Property:CanDoItAll.Processes.Core.Routing.ProcessDispatchTriggerFacts.TriggerStepRunId:System.Nullable<System.Guid>
Method:CanDoItAll.Processes.Core.Routing.ProcessDispatchTriggerFacts.Create(System.String,System.Nullable<System.Guid>):CanDoItAll.Processes.Core.Routing.ProcessDispatchTriggerFacts
Type:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessLifecycleRules
Method:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessLifecycleRules.BuildBlockTransitionFacts(System.Guid,System.String,System.String):CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessParentTransitionFacts
Method:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessLifecycleRules.BuildParentTransitionReason(CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessRunFacts):System.String
Method:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessLifecycleRules.BuildStartTransitionFacts(System.Guid,System.Guid,System.String,System.String):CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessParentTransitionFacts
Method:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessLifecycleRules.BuildTerminalMirrorTransitionFacts(System.Guid,CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessRunFacts,CanDoItAll.Processes.Contracts.ProcessStepRunStatus,System.String):CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessParentTransitionFacts
Method:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessLifecycleRules.ResolveParentStepStatus(CanDoItAll.Processes.Contracts.ProcessRunStatus):System.Nullable<CanDoItAll.Processes.Contracts.ProcessStepRunStatus>
Type:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessParentTransitionFacts
Constructor:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessParentTransitionFacts(System.Guid,System.Nullable<System.Guid>,CanDoItAll.Processes.Contracts.ProcessStepRunStatus,System.String,System.String,System.Boolean)
Property:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessParentTransitionFacts.DecidedBy:System.String
Property:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessParentTransitionFacts.Reason:System.String
Property:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessParentTransitionFacts.StepRunConcurrencyToken:System.Nullable<System.Guid>
Property:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessParentTransitionFacts.StepRunId:System.Guid
Property:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessParentTransitionFacts.SuppressAutomationDispatch:System.Boolean
Property:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessParentTransitionFacts.TargetStatus:CanDoItAll.Processes.Contracts.ProcessStepRunStatus
Type:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessRunFacts
Constructor:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessRunFacts(System.Guid,System.String,CanDoItAll.Processes.Contracts.ProcessRunStatus)
Property:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessRunFacts.RunId:System.Guid
Property:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessRunFacts.RunName:System.String
Property:CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessRunFacts.Status:CanDoItAll.Processes.Contracts.ProcessRunStatus
""".Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(expected, ReadProcessCorePublicApiSurface());
    }

    [Fact]
    public void Execution_boundary_design_stays_on_staging_facade_cutline()
    {
        var design = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-agent-execution-boundary-foundation-v1",
            "architecture",
            "02-execution-boundary-staging.md");

        Assert.Contains("IProcessAutomationExecutionClient", design, StringComparison.Ordinal);
        Assert.Contains("SB06 Movement Cutline", design, StringComparison.Ordinal);
        Assert.Contains("The first stage may still return selected AgentFramework types", design, StringComparison.Ordinal);
        Assert.Contains("not a final `Processes.Core` contract", design, StringComparison.Ordinal);
        Assert.Contains("manager chat, observation services, recovery worker, UI run-detail loaders", design, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_execution_boundary_inventory_records_direct_dispatcher_calls_before_movement()
    {
        var inventory = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-agent-execution-boundary-foundation-v1",
            "inventories",
            "02-agentframework-usage-in-processes.md");

        Assert.Contains("ProcessRunAutomationDispatchService.Execution.cs", inventory, StringComparison.Ordinal);
        Assert.Contains("ExecuteRunAsync", inventory, StringComparison.Ordinal);
        Assert.Contains("GetExecutionRunDetailAsync", inventory, StringComparison.Ordinal);
        Assert.Contains("ListExecutionRunsAsync", inventory, StringComparison.Ordinal);
        Assert.Contains("Process automation execution client facade where execution-path related", inventory, StringComparison.Ordinal);
        Assert.Contains("Out of dispatcher-boundary scope for this bundle", inventory, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispatcher_execution_path_uses_process_owned_execution_client_after_migration()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var dispatcherSource = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(dispatchDirectory, "ProcessRunAutomationDispatchService*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("workspaceService.", dispatcherSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IAgentFrameworkWorkspaceService workspaceService", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("IProcessAutomationExecutionClient executionClient", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("executionClient.ExecuteRunAsync", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("executionClient.GetExecutionRunDetailAsync", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("executionClient.ListExecutionRunsAsync", dispatcherSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_contracts_project_is_solution_registered_and_stays_neutral()
    {
        var root = FindRepositoryRoot();
        var solution = ReadRepositoryFile("CanDoItAll.slnx");
        var projectPath = Path.Combine(
            root,
            "src",
            "CanDoItAll.Processes.Contracts",
            "CanDoItAll.Processes.Contracts.csproj");
        var project = File.ReadAllText(projectPath);
        var source = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(Path.GetDirectoryName(projectPath)!, "*.cs", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.Contains("src/CanDoItAll.Processes.Contracts/CanDoItAll.Processes.Contracts.csproj", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("<ProjectReference", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<PackageReference", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore.Components", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.AgentFramework", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ComponentBase", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_contracts_project_contains_only_minimal_execution_boundary_snapshots()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Contracts",
            "Automation",
            "ProcessAutomationExecutionContracts.cs");

        Assert.Contains("public sealed record ProcessAutomationExecutionRequest", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationInvocationSource", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationInvocationPolicy", source, StringComparison.Ordinal);
        Assert.Contains("public enum ProcessAutomationFinalizerMode", source, StringComparison.Ordinal);
        Assert.Contains("public enum ProcessAutomationStructuredOutputKind", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationExecutionRunQuery", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationExecutionRunResult", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationExecutionRunDetail", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationExecutionRunRecord", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationToolExecutionReceipt", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationProviderUsageObservation", source, StringComparison.Ordinal);
        Assert.Contains("public sealed class ProcessAutomationExecutionFailedException", source, StringComparison.Ordinal);
        Assert.Contains("public enum ProcessAutomationExecutionState", source, StringComparison.Ordinal);
        Assert.Contains("public enum ProcessAutomationRunOutcome", source, StringComparison.Ordinal);
        Assert.Contains("public enum ProcessAutomationProviderUsageStatus", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRun ", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessStepRun", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionRunRequest", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispatcher_partials_excluding_execution_client_do_not_use_agent_framework_execution_snapshots()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var source = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(dispatchDirectory, "*.cs")
                .Where(path => !string.Equals(
                    Path.GetFileName(path),
                    "ProcessAutomationExecutionClient.cs",
                    StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var forbiddenTokens = new[]
        {
            "ExecutionRunResult",
            "ExecutionRunDetail",
            "ExecutionRunRecord",
            "ExecutionRunQuery",
            "AgentChatRunFailedException",
            "AgentRunFailedException",
            "AgentStructuredOutputContracts"
        };

        foreach (var forbiddenToken in forbiddenTokens)
        {
            Assert.False(
                Regex.IsMatch(source, $@"\b{Regex.Escape(forbiddenToken)}\b", RegexOptions.CultureInvariant),
                forbiddenToken);
        }
    }

    [Fact]
    public void Receipt_observation_helper_uses_process_snapshots_without_agent_framework_references()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessAutomationReceiptObservationHelper.cs");

        Assert.Contains("ProcessAutomationExecutionRunDetail", source, StringComparison.Ordinal);
        Assert.Contains("ProcessAutomationToolExecutionReceipt", source, StringComparison.Ordinal);
        Assert.Contains("ResolveSuccessfulToolNames", source, StringComparison.Ordinal);
        Assert.Contains("ResolveReceiptFamilies", source, StringComparison.Ordinal);
        Assert.Contains("ResolveProviderMetadata", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.AgentFramework", source, StringComparison.Ordinal);
        Assert.False(Regex.IsMatch(source, @"\bExecutionRunDetail\b", RegexOptions.CultureInvariant));
        Assert.False(Regex.IsMatch(source, @"\bToolExecutionReceiptRecord\b", RegexOptions.CultureInvariant));
    }

    [Fact]
    public void Bundle_proof_paths_do_not_contain_mobile_or_small_screen_artifacts()
    {
        var proofRoot = Path.Combine(
            FindRepositoryRoot(),
            "codex",
            "bundles",
            "process-dispatch-execution-snapshot-boundary-v1",
            "proof");
        var forbiddenPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet"
        };
        IEnumerable<string> proofArtifactPaths = Directory.Exists(proofRoot)
            ? Directory.EnumerateFiles(proofRoot, "*", SearchOption.AllDirectories)
            : [];

        Assert.DoesNotContain(proofArtifactPaths, path =>
        {
            var relativePath = Path.GetRelativePath(proofRoot, path);
            return forbiddenPathTokens.Any(token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Artifact_boundary_bundle_proof_paths_do_not_contain_mobile_or_small_screen_artifacts()
    {
        var proofRoot = Path.Combine(
            FindRepositoryRoot(),
            "codex",
            "bundles",
            "process-dispatch-artifact-boundary-foundation-v1",
            "proof");
        var forbiddenPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet"
        };
        IEnumerable<string> proofArtifactPaths = Directory.Exists(proofRoot)
            ? Directory.EnumerateFiles(proofRoot, "*", SearchOption.AllDirectories)
            : [];

        Assert.DoesNotContain(proofArtifactPaths, path =>
        {
            var relativePath = Path.GetRelativePath(proofRoot, path);
            return forbiddenPathTokens.Any(token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Artifact_boundary_helpers_stay_inside_processes_module_without_core_project()
    {
        var root = FindRepositoryRoot();
        var processesDispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");

        Assert.True(File.Exists(Path.Combine(processesDispatchDirectory, "ProcessArtifactExpectationMatcher.cs")));
        Assert.True(File.Exists(Path.Combine(processesDispatchDirectory, "ProcessArtifactProjectionLineageBuilder.cs")));
        Assert.True(File.Exists(Path.Combine(processesDispatchDirectory, "ProcessArtifactProjectionPlanner.cs")));
        Assert.True(File.Exists(Path.Combine(processesDispatchDirectory, "ProcessArtifactEvidenceValidationRules.cs")));
        AssertNarrowProcessCoreSeed(root);
        Assert.False(Directory.Exists(Path.Combine(root, "src", "CanDoItAll.Modules.Processes.Core")));
    }

    [Fact]
    public void Artifact_validation_snapshot_boundary_is_process_module_local_without_driver_contracts()
    {
        var root = FindRepositoryRoot();
        var processesDispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var snapshotPath = Path.Combine(processesDispatchDirectory, "ProcessArtifactValidationSnapshot.cs");
        var builderPath = Path.Combine(processesDispatchDirectory, "ProcessArtifactValidationSnapshotBuilder.cs");
        var helperSource = string.Join(
            Environment.NewLine,
            File.ReadAllText(snapshotPath),
            File.ReadAllText(builderPath));

        Assert.True(File.Exists(snapshotPath));
        Assert.True(File.Exists(builderPath));
        Assert.Contains("internal sealed record ProcessArtifactValidationSnapshot", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessArtifactExpectationSnapshot", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", helperSource, StringComparison.Ordinal);
        AssertNarrowProcessCoreSeed(root);
    }

    [Fact]
    public void Tool_validation_boundary_helpers_are_module_local_without_core_or_driver_contracts()
    {
        var root = FindRepositoryRoot();
        var processesDispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var helperFiles = new[]
        {
            "ProcessToolReceiptFacts.cs",
            "ProcessRequiredToolValidationRules.cs",
            "ProcessCriticalToolFailureRules.cs",
            "ProcessCompletionBlockerRules.cs",
            "ProcessCompletionDecisionRules.cs",
            "ProcessRecoveryRetryDecisionRules.cs",
            "ProcessAutomationSessionObservation.cs",
            "ProcessAutomationExecutionLogObservation.cs",
            "ProcessAutomationObservationSnapshot.cs",
            "ProcessDeclaredStepOutcomeRules.cs"
        };
        var helperSource = string.Join(
            Environment.NewLine,
            helperFiles.Select(file =>
            {
                var path = Path.Combine(processesDispatchDirectory, file);
                Assert.True(File.Exists(path), file);
                return File.ReadAllText(path);
            }));

        Assert.Contains("internal sealed record ProcessToolReceiptFact", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessRequiredToolDecision", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessCriticalToolFailureStackContext", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessCompletionBlockerSummary", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessCompletionDecisionInput", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessRecoveryRetryFacts", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessAutomationSessionObservation", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessAutomationExecutionLogObservation", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessAutomationObservationSnapshot", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessDeclaredStepOutcomeRules", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync", helperSource, StringComparison.Ordinal);
        AssertNarrowProcessCoreSeed(root);
    }

    [Fact]
    public void Implementation_proof_helpers_are_module_local_without_core_or_driver_contracts()
    {
        var root = FindRepositoryRoot();
        var processesDispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var helperFiles = new[]
        {
            "ProcessImplementationStackRules.cs",
            "ProcessConcreteProductPathRules.cs",
            "ProcessImplementationReceiptTimeline.cs",
            "ProcessDotNetHostEvidenceRules.cs",
            "ProcessCarriedImplementationProofRules.cs",
            "ProcessRunAutomationDispatchService.ImplementationProofBridges.cs"
        };
        var dispatcherPath = Path.Combine(processesDispatchDirectory, "ProcessRunAutomationDispatchService.ImplementationProof.cs");
        var artifactValidationPath = Path.Combine(processesDispatchDirectory, "ProcessRunAutomationDispatchService.ArtifactValidation.cs");

        var helperSource = string.Join(
            Environment.NewLine,
            helperFiles.Select(file =>
            {
                var path = Path.Combine(processesDispatchDirectory, file);
                Assert.True(File.Exists(path), file);
                return File.ReadAllText(path);
            }));
        var dispatcherSource = File.ReadAllText(dispatcherPath);
        var artifactValidationSource = File.ReadAllText(artifactValidationPath);

        Assert.Contains("internal sealed record ProcessImplementationContractSnapshot", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessImplementationStackRules", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessConcreteProductPathRules", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessImplementationReceiptTimeline", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessDotNetHostEvidenceRules", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessCarriedImplementationProofRules", helperSource, StringComparison.Ordinal);
        Assert.Contains("private static class ProcessMockImplementationProofBridge", helperSource, StringComparison.Ordinal);
        Assert.Contains("private static class ProcessImplementationArtifactWriteSatisfactionBridge", helperSource, StringComparison.Ordinal);
        Assert.Contains("ProcessImplementationContractSnapshot.RequiresCurrentAttemptProductMutation", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("ProcessImplementationStackRules.ImplementationContractMentionsDotNet", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("ProcessConcreteProductPathRules.HasConcreteProductPath", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("ProcessImplementationReceiptTimeline.ResolveLatestImplementationProofReadReceipt", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDotNetHostEvidenceRules.ResolveRunnableDotNetHostProjectPaths", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("ProcessCarriedImplementationProofRules.ResolveCarriedImplementationProof", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("ProcessMockImplementationProofBridge.MatchesExpectedArtifact", artifactValidationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessImplementationArtifactWriteSatisfactionBridge.CanProjectWorkspaceWrittenArtifact", artifactValidationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", helperSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NotImplemented", helperSource, StringComparison.Ordinal);
        Assert.True(dispatcherSource.Split(Environment.NewLine).Length < 1120);
        AssertNarrowProcessCoreSeed(root);
    }

    [Fact]
    public void Tool_validation_dispatcher_delegates_to_local_fact_and_rule_boundaries()
    {
        var toolValidationSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ToolValidation.cs");
        var artifactValidationSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactValidation.cs");
        var recoveryPacketsSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.RecoveryPackets.cs");
        var receiptObservationSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessAutomationReceiptObservationHelper.cs");

        Assert.Contains("ProcessCriticalToolFailureRules.ResolveUnresolvedCriticalToolFailures", toolValidationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRequiredToolValidationRules.ResolveMissingRequiredTools", toolValidationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessCompletionBlockerRules.CreateSummary", toolValidationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessCompletionDecisionRules.TryResolveRunStateDecision", toolValidationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessToolReceiptFacts.IsCriticalWorkspaceProcessReceipt", artifactValidationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessToolReceiptFacts.ResolveSuccessfulReceipts", receiptObservationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRecoveryRetryDecisionRules.CreateFacts", recoveryPacketsSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Tool_validation_boundary_bundle_proof_paths_do_not_contain_mobile_or_small_screen_artifacts()
    {
        var proofRoot = Path.Combine(
            FindRepositoryRoot(),
            "codex",
            "bundles",
            "process-dispatch-tool-validation-recovery-boundary-v1",
            "proof");
        var forbiddenPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet",
            "android",
            "iphone",
            "responsive"
        };
        IEnumerable<string> proofArtifactPaths = Directory.Exists(proofRoot)
            ? Directory.EnumerateFiles(proofRoot, "*", SearchOption.AllDirectories)
            : [];

        Assert.DoesNotContain(proofArtifactPaths, path =>
        {
            var relativePath = Path.GetRelativePath(proofRoot, path);
            return forbiddenPathTokens.Any(token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Artifact_validation_gate_a_records_live_inventory_and_blocks_driver_or_viewport_drift()
    {
        var root = FindRepositoryRoot();
        var inventory = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-dispatch-artifact-validation-rule-boundary-v1",
            "inventories",
            "02-artifact-validation-method-inventory-seed.md");
        var driverReadiness = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-dispatch-artifact-validation-rule-boundary-v1",
            "inventories",
            "04-driver-readiness-map.md");
        var proofRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-dispatch-artifact-validation-rule-boundary-v1",
            "proof");
        var forbiddenPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet"
        };
        IEnumerable<string> proofArtifactPaths = Directory.Exists(proofRoot)
            ? Directory.EnumerateFiles(proofRoot, "*", SearchOption.AllDirectories)
            : [];

        Assert.Contains("Status: refreshed from live source in SB02.", inventory, StringComparison.Ordinal);
        Assert.Contains("Current line count: 3931", inventory, StringComparison.Ordinal);
        Assert.Contains("Method declaration rows found: 188", inventory, StringComparison.Ordinal);
        Assert.Contains("Side-effect indicator rows found: 57", inventory, StringComparison.Ordinal);
        Assert.Contains("File.Copy", inventory, StringComparison.Ordinal);
        Assert.Contains("Do not implement driver APIs", driverReadiness, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", driverReadiness, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", driverReadiness, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", driverReadiness, StringComparison.Ordinal);
        Assert.DoesNotContain(proofArtifactPaths, path =>
        {
            var relativePath = Path.GetRelativePath(proofRoot, path);
            return forbiddenPathTokens.Any(token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Process_dispatch_claim_route_gate_a_SB04_INV_001_records_live_inventory_and_blocks_core_driver_or_viewport_drift()
    {
        var root = FindRepositoryRoot();
        var routeInventory = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-dispatch-claim-route-boundary-v1",
            "inventories",
            "02-current-dispatch-route-map.md");
        var concurrencyInventory = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-dispatch-claim-route-boundary-v1",
            "inventories",
            "03-concurrency-rule-inventory.md");
        var mafProject = ReadRepositoryFile(
            "src",
            "CanDoItAll.AgentFramework.Maf",
            "CanDoItAll.AgentFramework.Maf.csproj");
        var processesModuleSource = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(Path.Combine(root, "src", "CanDoItAll.Modules.Processes"), "*.*", SearchOption.AllDirectories)
                .Where(path => Path.GetExtension(path) is ".cs" or ".csproj")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var proofRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-dispatch-claim-route-boundary-v1",
            "proof");
        var forbiddenPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet"
        };
        IEnumerable<string> proofArtifactPaths = Directory.Exists(proofRoot)
            ? Directory.EnumerateFiles(proofRoot, "*", SearchOption.AllDirectories)
            : [];

        Assert.Contains("Durable dispatch claim", routeInventory, StringComparison.Ordinal);
        Assert.Contains("Heartbeat session", routeInventory, StringComparison.Ordinal);
        Assert.Contains("Route planners must not execute EF writes", routeInventory, StringComparison.Ordinal);
        Assert.Contains("ResolveBlockingAutomationExecutionRunId(stepRun, executionRuns, now)", concurrencyInventory, StringComparison.Ordinal);
        Assert.Contains("ResolveRecoverableAutomationExecutionRunId(stepRun, executionRuns)", concurrencyInventory, StringComparison.Ordinal);
        Assert.Contains("Keep `executionClient.ListExecutionRunsAsync`", concurrencyInventory, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.Processes", mafProject, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.Projects", mafProject, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.Workbench", mafProject, StringComparison.Ordinal);
        Assert.DoesNotContain("interface IProcessDriverPack", processesModuleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("class ProcessDriverPack", processesModuleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.Processes.Core", processesModuleSource, StringComparison.Ordinal);
        AssertNarrowProcessCoreSeed(root);
        Assert.False(Directory.Exists(Path.Combine(root, "src", "CanDoItAll.Modules.Processes.Core")));
        Assert.DoesNotContain(proofArtifactPaths, path =>
        {
            var relativePath = Path.GetRelativePath(proofRoot, path);
            return forbiddenPathTokens.Any(token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Process_dispatch_claim_route_gate_a_SB04_INV_002_rejects_placeholder_or_stale_inventories()
    {
        var routeInventory = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-dispatch-claim-route-boundary-v1",
            "inventories",
            "02-current-dispatch-route-map.md");
        var concurrencyInventory = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-dispatch-claim-route-boundary-v1",
            "inventories",
            "03-concurrency-rule-inventory.md");

        Assert.DoesNotContain("Codex must fill this", routeInventory, StringComparison.Ordinal);
        Assert.DoesNotContain("Initial observed route sequence", routeInventory, StringComparison.Ordinal);
        Assert.DoesNotContain("Codex must update this", concurrencyInventory, StringComparison.Ordinal);
        Assert.DoesNotContain("Initial candidate methods", concurrencyInventory, StringComparison.Ordinal);
        Assert.Contains("Live source captured in SB02", routeInventory, StringComparison.Ordinal);
        Assert.Contains("Live source captured in SB03", concurrencyInventory, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_dispatch_claim_route_gate_b_SB08_INV_001_records_concurrency_helper_parity_and_blocks_side_effect_drift()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var helperSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessAutomationExecutionRunSelection.cs"));
        var concurrencySource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Concurrency.cs"));
        var adoptionCoordinatorSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessConcurrentExecutionAdoptionCoordinator.cs"));
        var integrationTestSource = ReadRepositoryFile(
            "tests",
            "CanDoItAll.Tests.Integration",
            "ProcessRunAutomationDispatchServiceTests.cs");
        var proofRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-dispatch-claim-route-boundary-v1",
            "proof");
        var forbiddenPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet"
        };
        IEnumerable<string> proofArtifactPaths = Directory.Exists(proofRoot)
            ? Directory.EnumerateFiles(proofRoot, "*", SearchOption.AllDirectories)
            : [];

        Assert.Contains("internal static class ProcessAutomationExecutionRunSelection", helperSource, StringComparison.Ordinal);
        Assert.Contains("ResolveBlockingAutomationExecutionRunId", helperSource, StringComparison.Ordinal);
        Assert.Contains("ResolveRecoverableAutomationExecutionRunId", helperSource, StringComparison.Ordinal);
        Assert.Contains("ResolveCompetingActiveAutomationExecutionRun", helperSource, StringComparison.Ordinal);
        Assert.Contains("IsStaleAutomationExecutionRun", helperSource, StringComparison.Ordinal);
        Assert.Contains("IsConcurrentAutomationSessionBusyException", helperSource, StringComparison.Ordinal);
        Assert.Contains("ShouldSkipFreshAutomationDispatch", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("executionClient.", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ConcurrentAutomationExecution", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", helperSource, StringComparison.Ordinal);

        Assert.Contains("ProcessAutomationExecutionRunSelection.ResolveBlockingAutomationExecutionRunId", concurrencySource, StringComparison.Ordinal);
        Assert.Contains("ProcessAutomationExecutionRunSelection.ResolveRecoverableAutomationExecutionRunId", concurrencySource, StringComparison.Ordinal);
        Assert.Contains("ProcessAutomationExecutionRunSelection.ResolveCompetingActiveAutomationExecutionRun", concurrencySource, StringComparison.Ordinal);
        Assert.Contains("ProcessAutomationExecutionRunSelection.IsConcurrentAutomationSessionBusyException", concurrencySource, StringComparison.Ordinal);
        Assert.Contains("executionClient.ListExecutionRunsAsync", concurrencySource, StringComparison.Ordinal);
        Assert.Contains("ProcessConcurrentExecutionAdoptionCoordinator.TryAdoptAsync", concurrencySource, StringComparison.Ordinal);
        Assert.DoesNotContain("executionClient.GetExecutionRunDetailAsync", concurrencySource, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", concurrencySource, StringComparison.Ordinal);
        Assert.DoesNotContain("new ConcurrentAutomationExecution", concurrencySource, StringComparison.Ordinal);
        Assert.Contains("executionClient.GetExecutionRunDetailAsync", adoptionCoordinatorSource, StringComparison.Ordinal);
        Assert.Contains("Task.Delay", adoptionCoordinatorSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessRunAutomationDispatchService.ConcurrentAutomationExecution", adoptionCoordinatorSource, StringComparison.Ordinal);

        Assert.Contains("ProcessAutomationExecutionRunSelection_SB06_INV_001", integrationTestSource, StringComparison.Ordinal);
        Assert.Contains("ProcessAutomationExecutionRunSelection_SB06_INV_002", integrationTestSource, StringComparison.Ordinal);
        Assert.Contains("ProcessAutomationExecutionRunSelection_SB06_INV_003", integrationTestSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRunAutomationDispatchService_SB07_INV_001", integrationTestSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRunAutomationDispatchService_SB07_INV_002", integrationTestSource, StringComparison.Ordinal);
        Assert.DoesNotContain(proofArtifactPaths, path =>
        {
            var relativePath = Path.GetRelativePath(proofRoot, path);
            return forbiddenPathTokens.Any(token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Process_dispatch_claim_route_gate_b_SB08_INV_002_rejects_shallow_wrapper_migration_with_duplicate_selection_logic()
    {
        var concurrencySource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.Concurrency.cs");
        var helperDelegationCount = Regex.Matches(
            concurrencySource,
            "ProcessAutomationExecutionRunSelection\\.",
            RegexOptions.CultureInvariant).Count;

        Assert.True(helperDelegationCount >= 8);
        Assert.DoesNotContain("private static bool IsBlockingAutomationExecutionRun", concurrencySource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool IsRecoveryTrigger", concurrencySource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool IsRecoverableExecutionRunForCurrentAttempt", concurrencySource, StringComparison.Ordinal);
        Assert.DoesNotContain(".Where(executionRun => IsBlockingAutomationExecutionRun", concurrencySource, StringComparison.Ordinal);
        Assert.Contains("private static bool IsStaleAutomationExecutionRun", concurrencySource, StringComparison.Ordinal);
        Assert.Contains("ProcessAutomationExecutionRunSelection.IsStaleAutomationExecutionRun", concurrencySource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_dispatch_claim_route_gate_c_SB12_INV_001_proves_route_claim_start_and_heartbeat_boundaries_without_side_effect_drift()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var dispatchSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Dispatch.cs"));
        var coreRoutingDirectory = Path.Combine(root, "src", "CanDoItAll.Processes.Core", "Routing");
        var routePlannerSource = File.ReadAllText(Path.Combine(coreRoutingDirectory, "ProcessDispatchRoutePlanner.cs"));
        var routePipelineSource = File.ReadAllText(Path.Combine(coreRoutingDirectory, "ProcessDispatchRoutePipeline.cs"));
        var routeExecutionSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.RouteExecution.cs"));
        var routeBoundarySource = string.Join(
            Environment.NewLine,
            new[]
            {
                "ProcessDispatchRouteModels.cs",
                "ProcessDispatchRouteExecutionModels.cs",
                "ProcessDispatchRouteFacets.cs",
                "ProcessDispatchRouteHandlerPipeline.cs",
                "ProcessDispatchRouteHandlerFactory.cs",
                "ProcessDispatchRouteHandlers.cs",
                "ProcessDispatchRouteServices.cs",
                "ProcessRunAutomationDispatchService.RouteHandlers.cs"
            }.Select(file => File.ReadAllText(Path.Combine(dispatchDirectory, file))));
        var exceptionClosureSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.ExceptionClosure.cs"));
        var claimLifecycleSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchClaimLease.cs"));
        var claimServiceSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.ClaimLifecycle.cs"));
        var startTransitionSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchStartTransitionPlanner.cs"));
        var transitionAdapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessTransitionIntentAdapters.cs"));
        var guardLeaseSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchGuardLease.cs"));
        var heartbeatSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchLeaseHeartbeat.cs"));
        var integrationTestSource = ReadRepositoryFile(
            "tests",
            "CanDoItAll.Tests.Integration",
            "ProcessRunAutomationDispatchServiceTests.cs");

        Assert.Contains("public static class ProcessDispatchRoutePlanner", routePlannerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteKind.DatabaseRequirement", routePlannerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteKind.UpstreamMaterialization", routePlannerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteKind.StrandedRecovery", routePlannerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteKind.Subprocess", routePlannerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteKind.Workflow", routePlannerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteKind.AgentExecution", routePlannerSource, StringComparison.Ordinal);
        Assert.Contains("public enum ProcessDispatchRouteStage", routePipelineSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteStage.FreshRecoverySkip", routePipelineSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteStage.FinalizerTransition", routePipelineSource, StringComparison.Ordinal);
        AssertNarrowProcessCoreSeed(root);
        Assert.Contains("IProcessDispatchRouteHandler", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteContext", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteExecutionContext", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteCandidate", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteDispatchClaim", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteExecutionOutcome", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteHandlerPipeline", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteOrderAssertion.ThrowIfStageOrderInvalid", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteHandlerFactory", routeBoundarySource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteFacetSet", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchDatabaseRequirementRouteService", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchFinalizerRouteService", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("IProcessDispatchDatabaseRequirementRouteFacet", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("IProcessDispatchFinalizerRouteFacet", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("FreshRecoverySkipRouteHandler", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("DatabaseRequirementRouteHandler", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("UpstreamMaterializationRouteHandler", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("StrandedArtifactRecoveryRouteHandler", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("SubprocessRouteHandler", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("StartTransitionRouteHandler", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("WorkflowRouteHandler", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("DirectAgentExecutionRouteHandler", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("CompetingExecutionGuardRouteHandler", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("RunClosedGuardRouteHandler", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("FinalizerTransitionRouteHandler", routeBoundarySource, StringComparison.Ordinal);
        Assert.DoesNotContain("RouteHandler(ProcessRunAutomationDispatchService", routeBoundarySource, StringComparison.Ordinal);
        Assert.DoesNotContain("RouteHandler(\r\n        ProcessRunAutomationDispatchService", routeBoundarySource, StringComparison.Ordinal);

        var routePlannerForbiddenTokens = new[]
        {
            "await ",
            "Task<",
            "DbContext",
            "ExecuteUpdateAsync",
            "SaveChangesAsync",
            "serviceScopeFactory",
            "workflowRunCoordinator",
            "executionClient",
            "TransitionStepWithClaimAsync",
            "ExecuteUntilSettledAsync",
            "FinalizeStepCompletionAsync",
            "HandleSubprocessDispatchAsync",
            "logger",
            "RecordArtifactAsync"
        };

        foreach (var token in routePlannerForbiddenTokens)
        {
            Assert.DoesNotContain(token, routePlannerSource, StringComparison.Ordinal);
        }

        Assert.Contains("RunClaimedDispatchAsync", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("TryClaimStepDispatchAsync", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("CreateClaimedDispatchRouteHandlerPipeline", routeExecutionSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRoutePlanner.ResolveDatabaseRequirement", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRoutePlanner.ResolveUpstreamMaterialization", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRoutePlanner.ResolveStrandedRecovery", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRoutePlanner.ResolveSubprocess", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRoutePlanner.ResolveWorkflow", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("TransitionStepWithClaimAsync", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("workflowRunCoordinator.TryRunOrObserveAsync", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ExecuteUntilSettledAsync", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("FinalizeDirectAgentCompletionAsync", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("claimCoordinator.ReleaseAsync", routeExecutionSource, StringComparison.Ordinal);
        Assert.Contains("HandleDispatchFailureAsync", exceptionClosureSource, StringComparison.Ordinal);
        Assert.Contains("IProcessDispatchClaimStore", claimLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchClaimStore", claimLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchClaimCoordinator", claimLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchLeaseHeartbeat.Start", claimLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("CreateDispatchClaimCoordinator", claimServiceSource, StringComparison.Ordinal);

        Assert.Contains("BuildStartTransitionRequest", startTransitionSource, StringComparison.Ordinal);
        Assert.Contains("StepRunConcurrencyToken = transitionIntent.StepRunConcurrencyToken", transitionAdapterSource, StringComparison.Ordinal);
        Assert.Contains("SuppressAutomationDispatch = transitionIntent.SuppressAutomationDispatch", transitionAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TransitionStepWithClaimAsync", startTransitionSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchGuardLease", guardLeaseSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchLeaseHeartbeat", heartbeatSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchClaimLostException", heartbeatSource, StringComparison.Ordinal);

        Assert.Contains("ProcessDispatchGuardLease_SB09_INV_001", integrationTestSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchLeaseHeartbeat_renews_outer_and_step_claims_during_long_work", integrationTestSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchStartTransitionPlanner_SB10_INV_001", integrationTestSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRoutePlanner_SB11_INV_001", integrationTestSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRoutePlanner_SB11_INV_002", integrationTestSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_dispatch_claim_route_gate_c_SB12_INV_002_records_line_counts_and_blocks_core_driver_or_viewport_drift()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var dispatchSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Dispatch.cs"));
        var concurrencySource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Concurrency.cs"));
        var finalizerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs"));
        var routePlannerSource = File.ReadAllText(Path.Combine(root, "src", "CanDoItAll.Processes.Core", "Routing", "ProcessDispatchRoutePlanner.cs"));
        var coreRoutingSource = ReadNarrowProcessCoreSource(root);
        var helperSource = string.Join(
            Environment.NewLine,
            coreRoutingSource,
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteModels.cs")),
            routePlannerSource,
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.RouteExecution.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteExecutionModels.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteFacets.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteHandlerPipeline.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteHandlerFactory.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteHandlers.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteServices.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.RouteHandlers.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.ExceptionClosure.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchClaimLease.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.ClaimLifecycle.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchStartTransitionPlanner.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchGuardLease.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchLeaseHeartbeat.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessAutomationExecutionRunSelection.cs")));
        var proofRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-dispatch-claim-route-boundary-v1",
            "proof");
        var forbiddenPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet"
        };
        IEnumerable<string> proofArtifactPaths = Directory.Exists(proofRoot)
            ? Directory.EnumerateFiles(proofRoot, "*", SearchOption.AllDirectories)
            : [];

        Assert.True(dispatchSource.Split(Environment.NewLine).Length < 2056);
        Assert.True(dispatchSource.Split(Environment.NewLine).Length < 1050);
        Assert.True(concurrencySource.Split(Environment.NewLine).Length < 1477);
        Assert.True(finalizerSource.Split(Environment.NewLine).Length <= 1433);
        Assert.True(routePlannerSource.Split(Environment.NewLine).Length < 120);

        var combinedSource = string.Join(Environment.NewLine, dispatchSource, concurrencySource, finalizerSource, helperSource);
        AssertNarrowProcessCoreSeed(root);
        Assert.DoesNotContain("IProcessDriverPack", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", helperSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NotImplementedException", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("throw new Exception", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain(proofArtifactPaths, path =>
        {
            var relativePath = Path.GetRelativePath(proofRoot, path);
            return forbiddenPathTokens.Any(token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Process_dispatch_main_loop_claim_lifecycle_boundary_SB88_INV_001_keeps_dispatch_facade_thin_and_side_effects_named()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var dispatchSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Dispatch.cs"));
        var claimSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchClaimLease.cs"));
        var routeSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.RouteExecution.cs"));
        var routeBoundarySource = string.Join(
            Environment.NewLine,
            new[]
            {
                "ProcessDispatchRouteModels.cs",
                "ProcessDispatchRouteExecutionModels.cs",
                "ProcessDispatchRouteFacets.cs",
                "ProcessDispatchRouteHandlerPipeline.cs",
                "ProcessDispatchRouteHandlerFactory.cs",
                "ProcessDispatchRouteHandlers.cs",
                "ProcessDispatchRouteServices.cs",
                "ProcessRunAutomationDispatchService.RouteHandlers.cs"
            }.Select(file => File.ReadAllText(Path.Combine(dispatchDirectory, file))));
        var exceptionSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.ExceptionClosure.cs"));
        var pipelineSource = File.ReadAllText(Path.Combine(root, "src", "CanDoItAll.Processes.Core", "Routing", "ProcessDispatchRoutePipeline.cs"));
        var combinedBoundarySource = string.Join(Environment.NewLine, claimSource, routeSource, routeBoundarySource, exceptionSource, pipelineSource);

        Assert.Contains("RunClaimedDispatchAsync", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteUpdateAsync", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AutomationDispatchClaimToken", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("ExecuteUpdateAsync", claimSource, StringComparison.Ordinal);
        Assert.Contains("AutomationDispatchClaimToken", claimSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchClaimCoordinator", claimSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchClaimStore", claimSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteStage.CompetingExecutionGuard", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("ExecuteClaimedDispatchRouteAsync", routeSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteHandlerPipeline", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteHandlerFactory", routeBoundarySource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteFacetSet", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchGuardRouteService", routeBoundarySource, StringComparison.Ordinal);
        Assert.DoesNotContain("RouteHandler(ProcessRunAutomationDispatchService", routeBoundarySource, StringComparison.Ordinal);
        Assert.Contains("HandleDispatchHeartbeatClaimLost", exceptionSource, StringComparison.Ordinal);
        Assert.Contains("HandleDispatchClaimLost", exceptionSource, StringComparison.Ordinal);
        Assert.Contains("HandleDispatchFailureAsync", exceptionSource, StringComparison.Ordinal);
        AssertNarrowProcessCoreSeed(root);
        Assert.DoesNotContain("IProcessDriverPack", combinedBoundarySource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverRegistry", combinedBoundarySource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_dispatch_route_service_model_decoupling_boundary_uses_route_models_and_narrow_services()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var routeFacingSource = string.Join(
            Environment.NewLine,
            new[]
            {
                "ProcessDispatchRouteModels.cs",
                "ProcessDispatchRouteExecutionModels.cs",
                "ProcessDispatchRouteFacets.cs",
                "ProcessDispatchRouteHandlerPipeline.cs",
                "ProcessDispatchRouteHandlerFactory.cs",
                "ProcessDispatchRouteHandlers.cs",
                "ProcessDispatchRouteServices.cs"
            }.Select(file => File.ReadAllText(Path.Combine(dispatchDirectory, file)))
                .Prepend(ReadNarrowProcessCoreSource(root)));
        var adapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteModelAdapters.cs"));
        var factorySource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteHandlerFactory.cs"));
        var routeServiceSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteServices.cs"));
        var routeExecutionSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.RouteExecution.cs"));
        var routeHandlerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.RouteHandlers.cs"));

        Assert.Contains("internal sealed record ProcessRouteCandidate", routeFacingSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessRouteDispatchClaim", routeFacingSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessRouteExecutionRunSnapshot", routeFacingSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessRouteExecutionOutcome", routeFacingSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteExecutionRunSnapshot ExecutionRun", routeFacingSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteExecutionContext", routeFacingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteFacetSet", routeFacingSource, StringComparison.Ordinal);
        Assert.Contains("IProcessDispatchDatabaseRequirementRouteFacet databaseRequirementFacet", factorySource, StringComparison.Ordinal);
        Assert.Contains("IProcessDispatchFinalizerRouteFacet finalizerFacet", factorySource, StringComparison.Ordinal);
        Assert.Contains("LoadRouteCandidateAsync", routeExecutionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters.FromDispatcherCandidate", routeExecutionSource, StringComparison.Ordinal);

        var narrowServiceNames = new[]
        {
            "ProcessDispatchDatabaseRequirementRouteService",
            "ProcessDispatchUpstreamMaterializationRouteService",
            "ProcessDispatchRecoveryRouteService",
            "ProcessDispatchSubprocessRouteService",
            "ProcessDispatchStartTransitionRouteService",
            "ProcessDispatchWorkflowRouteService",
            "ProcessDispatchDirectAgentRouteService",
            "ProcessDispatchGuardRouteService",
            "ProcessDispatchFinalizerRouteService"
        };

        foreach (var narrowServiceName in narrowServiceNames)
        {
            Assert.Contains($"internal sealed class {narrowServiceName}", routeServiceSource, StringComparison.Ordinal);
            Assert.Contains($"new {narrowServiceName}", routeHandlerSource, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("internal sealed class ProcessDispatchRouteServices", routeServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteServices", factorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("using DispatchCandidate =", routeFacingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("using DispatchExecutionOutcome =", routeFacingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("using ProcessStepDispatchClaim =", routeFacingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.DispatchCandidate", routeFacingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.DispatchExecutionOutcome", routeFacingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.ProcessStepDispatchClaim", routeFacingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessAutomationExecutionRunDetail Detail", routeFacingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchCandidate candidate", routeFacingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchExecutionOutcome executionOutcome", routeFacingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessStepDispatchClaim dispatchClaim", routeFacingSource, StringComparison.Ordinal);

        Assert.Contains("ProcessRunAutomationDispatchService.DispatchCandidate", adapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRunAutomationDispatchService.DispatchExecutionOutcome", adapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRunAutomationDispatchService.ProcessStepDispatchClaim", adapterSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_dispatch_route_service_ownership_gate_uses_route_models_for_pre_execution_and_failure_closure()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var routeServicesSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteServices.cs"));
        var routeModelsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteModels.cs"));
        var hydrationSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateHydrationService.cs"));
        var recoveryRuntimeSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRecoveryRuntimeService.cs"));
        var directAgentRuntimeSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchDirectAgentRuntimeService.cs"));
        var directAgentExecutionAdapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchDirectAgentExecutionAdapter.cs"));
        var competingGuardSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCompetingExecutionGuardService.cs"));
        var failureClosureSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchFailureClosureService.cs"));
        var exceptionClosureSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.ExceptionClosure.cs"));

        Assert.Contains("internal sealed record ProcessRouteArtifactInput", routeModelsSource, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<ProcessRouteArtifactInput> ArtifactInputs", routeModelsSource, StringComparison.Ordinal);
        Assert.Contains("preExecutionGuardHandler.BuildDatabaseRequirementDecision(", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("databaseRequirementFailure.Message", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("preExecutionGuardHandler.PlanMissingUpstreamArtifactMaterialization(routeFacts)", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("LoadRouteCandidateAsync", hydrationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRecoveryRuntimeService recoveryRuntimeService", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchDirectAgentRuntimeService directAgentRuntimeService", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCompetingExecutionGuardService competingExecutionGuardService", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate)", recoveryRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(input.Candidate)", directAgentExecutionAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.FromDispatcherExecutionOutcome", directAgentExecutionAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchDirectAgentExecutionInput input", directAgentRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters.ToDispatcherCandidate", directAgentRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.DispatchCandidate", directAgentRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("ProcessExecutionRunQueryBuilder.ForCandidate(dispatcherCandidate)", competingGuardSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters", routeServicesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService dispatcher", routeServicesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var dispatcherCandidate = ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate);", routeServicesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var dispatcherClaim = ProcessDispatchRouteModelAdapters.ToDispatcherClaim(dispatchClaim);", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("finalizerApplicationService.FinalizeRecoveredCompletionAsync", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("finalizerApplicationService.FinalizeWorkflowCompletionAsync", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("finalizerApplicationService.FinalizeDirectAgentCompletionAsync", routeServicesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchWorkflowRouteService(\n    ProcessRunAutomationDispatchService dispatcher", routeServicesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchDirectAgentRouteService(\n    ProcessRunAutomationDispatchService dispatcher", routeServicesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchGuardRouteService(\n    ProcessRunAutomationDispatchService dispatcher", routeServicesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchFinalizerRouteService(\n    ProcessRunAutomationDispatchService dispatcher", routeServicesSource, StringComparison.Ordinal);

        Assert.Contains("internal sealed class ProcessDispatchFailureClosureService", failureClosureSource, StringComparison.Ordinal);
        Assert.Contains("runClosureGuardService.IsRunClosedToAutomationAsync", failureClosureSource, StringComparison.Ordinal);
        Assert.Contains("isStepDispatchClaimHeldAsync", failureClosureSource, StringComparison.Ordinal);
        Assert.Contains("stepTransitionService.TransitionStepWithClaimAsync", failureClosureSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRunAutomationDispatchService.AutomationActor", failureClosureSource, StringComparison.Ordinal);
        Assert.Contains("CreateFailureClosureService", exceptionClosureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetStatus = ProcessStepRunStatus.Failed", exceptionClosureSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB013_INV_001_uses_pre_execution_route_facts_without_source_payload()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var routeFactsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchPreExecutionRouteFacts.cs"));
        var routeServicesSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteServices.cs"));
        var preExecutionGuardHandlerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchPreExecutionGuardHandler.cs"));
        var materializationSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessMissingUpstreamArtifactMaterialization.cs"));
        var materializationSideEffectsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessMissingUpstreamArtifactMaterializationSideEffects.cs"));
        var routeModelsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteModels.cs"));
        var routeAdaptersSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteModelAdapters.cs"));
        var startTransitionSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchStartTransitionPlanner.cs"));
        var combinedPreExecutionSource = string.Join(
            Environment.NewLine,
            routeFactsSource,
            routeServicesSource,
            preExecutionGuardHandlerSource,
            materializationSource,
            materializationSideEffectsSource,
            startTransitionSource);

        Assert.Contains("internal sealed record ProcessDispatchPreExecutionRouteFacts", routeFactsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteRunSnapshot Run", routeFactsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteStepSnapshot StepRun", routeFactsSource, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<ProcessRouteArtifactInput> ArtifactInputs", routeFactsSource, StringComparison.Ordinal);
        Assert.Contains("FromCandidate(ProcessRouteCandidate candidate)", routeFactsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessRouteCandidateSource Source", routeFactsSource, StringComparison.Ordinal);

        Assert.Contains("var routeFacts = ProcessDispatchPreExecutionRouteFacts.FromCandidate(candidate);", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("preExecutionGuardHandler.BuildDatabaseRequirementDecision(", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("preExecutionGuardHandler.PlanMissingUpstreamArtifactMaterialization(routeFacts)", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("preExecutionGuardHandler.RecordAndRequestMissingUpstreamArtifactMaterializationAsync(", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("routeFacts", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("stepTransitionService.TransitionStepWithClaimAsync", routeServicesSource, StringComparison.Ordinal);

        Assert.Contains("BuildDatabaseRequirementDecision(", preExecutionGuardHandlerSource, StringComparison.Ordinal);
        Assert.Contains("PlanMissingUpstreamArtifactMaterialization(", preExecutionGuardHandlerSource, StringComparison.Ordinal);
        Assert.Contains("RecordAndRequestMissingUpstreamArtifactMaterializationAsync(", preExecutionGuardHandlerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchPreExecutionRouteFacts routeFacts", preExecutionGuardHandlerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRouteCandidate candidate", preExecutionGuardHandlerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TransitionStepWithClaimAsync", preExecutionGuardHandlerSource, StringComparison.Ordinal);

        Assert.Contains("ProcessDispatchPreExecutionRouteFacts routeFacts", materializationSource, StringComparison.Ordinal);
        Assert.Contains("BuildRequest(", materializationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRouteCandidate candidate", materializationSource, StringComparison.Ordinal);
        Assert.Contains("RecordAsync(", materializationSideEffectsSource, StringComparison.Ordinal);
        Assert.Contains("RecordAndRequestAsync(", materializationSideEffectsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchPreExecutionRouteFacts routeFacts", materializationSideEffectsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRouteCandidate candidate", materializationSideEffectsSource, StringComparison.Ordinal);

        Assert.DoesNotContain("IProcessRouteCandidateSource", routeModelsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessRouteDispatchClaimSource", routeModelsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessRouteExecutionOutcomeSource", routeModelsSource, StringComparison.Ordinal);
        Assert.DoesNotContain(" Source);", routeModelsSource, StringComparison.Ordinal);
        Assert.Contains("ConditionalWeakTable<ProcessRouteCandidate, DispatcherCandidateSource>", routeAdaptersSource, StringComparison.Ordinal);
        Assert.Contains("ConditionalWeakTable<ProcessRouteDispatchClaim, DispatcherDispatchClaimSource>", routeAdaptersSource, StringComparison.Ordinal);
        Assert.Contains("ConditionalWeakTable<ProcessRouteExecutionOutcome, DispatcherExecutionOutcomeSource>", routeAdaptersSource, StringComparison.Ordinal);
        Assert.Contains("BuildStartTransitionRequest", startTransitionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters", combinedPreExecutionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", ReadDispatchSourceExcludingCorePureRuleAdapters(dispatchDirectory), StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", combinedPreExecutionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", combinedPreExecutionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB014_INV_001_separates_materialization_pure_rules_from_side_effects()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var pureSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessMissingUpstreamArtifactMaterialization.cs"));
        var sideEffectsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessMissingUpstreamArtifactMaterializationSideEffects.cs"));
        var dispatchSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Dispatch.cs"));

        Assert.Contains("internal sealed record ProcessMissingUpstreamArtifactMaterializationFacts", pureSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessMissingUpstreamArtifactMaterializationFactsResolver", pureSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessMissingUpstreamArtifactMaterializationBlocker", pureSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessMissingUpstreamArtifactMaterializationFingerprint", pureSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessMissingUpstreamArtifactRerunRequestBuilder", pureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IDbContextFactory", pureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AppDbContext", pureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChangesAsync", pureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceScopeFactory", pureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateAsyncScope", pureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RerunAgentStepAsync", pureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ILogger", pureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer", pureSource, StringComparison.Ordinal);

        Assert.Contains("internal sealed class ProcessMissingUpstreamArtifactMaterializationJournalCoordinator", sideEffectsSource, StringComparison.Ordinal);
        Assert.Contains("IDbContextFactory<AppDbContext> dbContextFactory", sideEffectsSource, StringComparison.Ordinal);
        Assert.Contains("dbContext.SaveChangesAsync", sideEffectsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessJournalEntry", sideEffectsSource, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Serialize", sideEffectsSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessMissingUpstreamArtifactMaterializationCoordinator", sideEffectsSource, StringComparison.Ordinal);
        Assert.Contains("IServiceScopeFactory serviceScopeFactory", sideEffectsSource, StringComparison.Ordinal);
        Assert.Contains("serviceScopeFactory.CreateAsyncScope", sideEffectsSource, StringComparison.Ordinal);
        Assert.Contains("processesService.RerunAgentStepAsync", sideEffectsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessMissingUpstreamArtifactRerunRequestBuilder.BuildRequest(routeFacts, facts)", sideEffectsSource, StringComparison.Ordinal);
        Assert.Contains("logger.LogWarning", sideEffectsSource, StringComparison.Ordinal);
        Assert.Contains("logger.LogInformation", sideEffectsSource, StringComparison.Ordinal);
        Assert.Contains("CreateMissingUpstreamArtifactMaterializationJournalCoordinator", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessMissingUpstreamArtifactMaterializationCoordinator(", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", pureSource + sideEffectsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", pureSource + sideEffectsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", pureSource + sideEffectsSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB016_INV_001_moves_subprocess_runtime_to_route_input_model()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var runtimeModelsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchSubprocessRuntimeModels.cs"));
        var routeFacetsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteFacets.cs"));
        var routeHandlersSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteHandlers.cs"));
        var routeServicesSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteServices.cs"));
        var subprocessRuntimeSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchSubprocessRuntimeService.cs"));
        var subprocessLifecycleRulesSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessSubprocessLifecycleRules.cs"));
        var projectionPlanBuilderSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessSubprocessProjectionPlanBuilder.cs"));
        var projectionWriterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessSubprocessProjectionWriterCoordinator.cs"));
        var projectionGapJournalSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessSubprocessProjectionGapJournalCoordinator.cs"));
        var routeHandlerFactorySource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.RouteHandlers.cs"));
        var combinedSubprocessSource = string.Join(
            Environment.NewLine,
            runtimeModelsSource,
            routeFacetsSource,
            routeHandlersSource,
            routeServicesSource,
            subprocessRuntimeSource,
            subprocessLifecycleRulesSource,
            projectionPlanBuilderSource,
            projectionWriterSource,
            projectionGapJournalSource,
            routeHandlerFactorySource);

        Assert.Contains("internal sealed record ProcessDispatchSubprocessRuntimeInput(", runtimeModelsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteCandidate Candidate", runtimeModelsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteDispatchClaim DispatchClaim", runtimeModelsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteRunSnapshot Run => Candidate.Run", runtimeModelsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteStepSnapshot StepRun => Candidate.StepRun", runtimeModelsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchSubprocessRuntimeInput input", routeFacetsSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessDispatchSubprocessRuntimeInput(", routeHandlersSource, StringComparison.Ordinal);
        Assert.Contains("subprocessRuntimeService.HandleSubprocessDispatchAsync(", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchSubprocessRuntimeInput input", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("ProcessSubprocessLifecycleRules.BuildStartTransitionRequest(\n                    stepRunSnapshot", subprocessRuntimeSource.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("new ProcessDispatchSubprocessFinalizerInput(", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("input.Candidate", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("input.DispatchClaim", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchSubprocessRuntimeInput input", projectionPlanBuilderSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchSubprocessRuntimeInput input", projectionWriterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchSubprocessRuntimeInput input", projectionGapJournalSource, StringComparison.Ordinal);
        Assert.Contains("(claim, token) => EnsureStepDispatchClaimHeldAsync(", routeHandlerFactorySource, StringComparison.Ordinal);
        Assert.Contains("new ProcessStepDispatchClaim(claim.StepRunId, claim.ClaimToken)", routeHandlerFactorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("using DispatchCandidate", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("using ProcessStepDispatchClaim", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.DispatchCandidate", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.ProcessStepDispatchClaim", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters.ToDispatcherCandidate", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchFinalizerAdapter finalizerAdapter", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("finalizerAdapter.FinalizeSubprocessCompletionAsync", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRouteCandidate? routeCandidate", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRouteDispatchClaim? routeDispatchClaim", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("global::CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessLifecycleRules", subprocessLifecycleRulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", ReadDispatchSourceExcludingCorePureRuleAdapters(dispatchDirectory), StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", combinedSubprocessSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", combinedSubprocessSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB017_INV_001_extracts_subprocess_projection_persistence()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var subprocessRuntimeSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchSubprocessRuntimeService.cs"));
        var projectionPersistenceSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessSubprocessProjectionPersistenceService.cs"));
        var routeHandlerFactorySource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.RouteHandlers.cs"));
        var combinedSource = subprocessRuntimeSource + projectionPersistenceSource + routeHandlerFactorySource;

        Assert.Contains("internal sealed class ProcessSubprocessProjectionPersistenceService(", projectionPersistenceSource, StringComparison.Ordinal);
        Assert.Contains("ProjectCompletedArtifactsAsync(", projectionPersistenceSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchSubprocessRuntimeInput input", projectionPersistenceSource, StringComparison.Ordinal);
        Assert.Contains("dbContextFactory.CreateDbContextAsync", projectionPersistenceSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactExpectation", projectionPersistenceSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactRecord", projectionPersistenceSource, StringComparison.Ordinal);
        Assert.Contains("ProcessSubprocessProjectionPlanBuilder.SatisfiesCurrentArtifactExpectation", projectionPersistenceSource, StringComparison.Ordinal);
        Assert.Contains("ProcessSubprocessArtifactSourceResolver.ResolveSourceArtifact", projectionPersistenceSource, StringComparison.Ordinal);
        Assert.Contains("ProcessSubprocessProjectionGapJournalCoordinator", projectionPersistenceSource, StringComparison.Ordinal);
        Assert.Contains("ProcessSubprocessProjectionWriterCoordinator", projectionPersistenceSource, StringComparison.Ordinal);
        Assert.Contains("ProcessSubprocessProjectionPlanBuilder.Build", projectionPersistenceSource, StringComparison.Ordinal);
        Assert.Contains("projectionWriterCoordinator.WriteAsync", projectionPersistenceSource, StringComparison.Ordinal);
        Assert.Contains("dbContext.SaveChangesAsync", projectionPersistenceSource, StringComparison.Ordinal);
        Assert.Contains("ensureStepDispatchClaimHeldAsync(input.DispatchClaim", projectionPersistenceSource, StringComparison.Ordinal);

        Assert.Contains("ProcessSubprocessProjectionPersistenceService projectionPersistenceService", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("projectionPersistenceService.ProjectCompletedArtifactsAsync(", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectCompletedSubprocessArtifactsAsync", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateDbContextAsync", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChangesAsync", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessSubprocessProjectionPlanBuilder.Build", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("projectionWriterCoordinator.WriteAsync", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessSubprocessProjectionGapJournalCoordinator", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessSubprocessProjectionWriterCoordinator", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IWorkspacePathResolver", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IDatabaseProfileRuntimeAccessor", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IClock clock", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ensureStepDispatchClaimHeldAsync", subprocessRuntimeSource, StringComparison.Ordinal);

        Assert.Contains("new ProcessSubprocessProjectionPersistenceService(", routeHandlerFactorySource, StringComparison.Ordinal);
        Assert.Contains("new ProcessStepDispatchClaim(claim.StepRunId, claim.ClaimToken)", routeHandlerFactorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", combinedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB019_INV_001_moves_direct_agent_runtime_to_execution_input_model()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var executionModelsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchDirectAgentExecutionModels.cs"));
        var executionAdapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchDirectAgentExecutionAdapter.cs"));
        var routeFacetsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteFacets.cs"));
        var routeHandlersSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteHandlers.cs"));
        var routeServicesSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteServices.cs"));
        var runtimeSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchDirectAgentRuntimeService.cs"));
        var routeHandlerFactorySource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.RouteHandlers.cs"));
        var routeOwnedDirectAgentBoundarySource = string.Join(
            Environment.NewLine,
            executionModelsSource,
            routeFacetsSource,
            routeHandlersSource,
            routeServicesSource,
            runtimeSource);

        Assert.Contains("internal sealed record ProcessDispatchDirectAgentExecutionInput(", executionModelsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteCandidate Candidate", executionModelsSource, StringComparison.Ordinal);
        Assert.Contains("string Trigger", executionModelsSource, StringComparison.Ordinal);
        Assert.Contains("Func<CancellationToken, Task> RenewLeaseAsync", executionModelsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteRunSnapshot Run => Candidate.Run", executionModelsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteStepSnapshot StepRun => Candidate.StepRun", executionModelsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchDirectAgentExecutionInput input", routeFacetsSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessDispatchDirectAgentExecutionInput(", routeHandlersSource, StringComparison.Ordinal);
        Assert.Contains("context.Execution.DispatchRenewLeaseAsync", routeHandlersSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchDirectAgentExecutionInput input", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchDirectAgentExecutionInput input", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("executeUntilSettledAsync(\n            input,", runtimeSource.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("new ProcessDispatchDirectAgentExecutionAdapter(ExecuteUntilSettledAsync)", routeHandlerFactorySource, StringComparison.Ordinal);
        Assert.Contains("executionAdapter.ExecuteUntilSettledAsync", routeHandlerFactorySource, StringComparison.Ordinal);

        Assert.Contains("internal sealed class ProcessDispatchDirectAgentExecutionAdapter(", executionAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRunAutomationDispatchService.DispatchCandidate", executionAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(input.Candidate)", executionAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.FromDispatcherExecutionOutcome(executionOutcome)", executionAdapterSource, StringComparison.Ordinal);
        Assert.Contains("input.Trigger", executionAdapterSource, StringComparison.Ordinal);
        Assert.Contains("input.RenewLeaseAsync", executionAdapterSource, StringComparison.Ordinal);

        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters.ToDispatcherCandidate", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters.FromDispatcherExecutionOutcome", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.DispatchCandidate", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.DispatchExecutionOutcome", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchCandidate candidate", routeOwnedDirectAgentBoundarySource, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchExecutionOutcome executionOutcome", routeOwnedDirectAgentBoundarySource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", ReadDispatchSourceExcludingCorePureRuleAdapters(dispatchDirectory), StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", routeOwnedDirectAgentBoundarySource + routeHandlerFactorySource + executionAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", routeOwnedDirectAgentBoundarySource + routeHandlerFactorySource + executionAdapterSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB020_INV_001_slims_route_execution_outcome_to_run_snapshot()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var routeModelsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteModels.cs"));
        var adapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteModelAdapters.cs"));
        var competingGuardSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCompetingExecutionGuardService.cs"));
        var routeHandlersSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteHandlers.cs"));
        var finalizerAdapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchFinalizerAdapter.cs"));
        var routeConsumerSource = string.Join(
            Environment.NewLine,
            routeModelsSource,
            competingGuardSource,
            routeHandlersSource);

        Assert.Contains("internal sealed record ProcessRouteExecutionRunSnapshot(", routeModelsSource, StringComparison.Ordinal);
        Assert.Contains("Guid Id", routeModelsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteExecutionRunSnapshot ExecutionRun", routeModelsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessAutomationExecutionRunDetail Detail", routeModelsSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessRouteExecutionRunSnapshot(executionOutcome.Detail.Run.Id)", adapterSource, StringComparison.Ordinal);
        Assert.Contains("new DispatcherExecutionOutcomeSource(executionOutcome)", adapterSource, StringComparison.Ordinal);
        Assert.Contains("RequireSource(ExecutionOutcomeSources, executionOutcome, nameof(executionOutcome)).ExecutionOutcome", adapterSource, StringComparison.Ordinal);

        Assert.Contains("executionOutcome.ExecutionRun.Id", competingGuardSource, StringComparison.Ordinal);
        Assert.Contains("executionOutcome.ExecutionRun.Id", routeHandlersSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(executionOutcome)", competingGuardSource, StringComparison.Ordinal);
        Assert.DoesNotContain("executionOutcome.Detail", routeConsumerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("recoveryOutcome.Detail", routeConsumerSource, StringComparison.Ordinal);

        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(input.RecoveryOutcome)", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(input.ExecutionOutcome)", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", ReadDispatchSourceExcludingCorePureRuleAdapters(dispatchDirectory), StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", routeConsumerSource + adapterSource + finalizerAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", routeConsumerSource + adapterSource + finalizerAdapterSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB021_INV_001_preserves_execution_retry_provider_and_finalizer_paths()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var executionSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Execution.cs"));
        var recoveryPacketsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.RecoveryPackets.cs"));
        var concurrencySource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Concurrency.cs"));
        var providerRecoverySource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.ProviderRecovery.cs"));
        var executionAttemptLoopFacadeSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessExecutionAttemptLoopFacade.cs"));
        var providerRepairSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessProviderRepairCoordinator.cs"));
        var directAgentModelsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchDirectAgentExecutionModels.cs"));
        var directAgentAdapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchDirectAgentExecutionAdapter.cs"));
        var directAgentRuntimeSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchDirectAgentRuntimeService.cs"));
        var routeModelsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteModels.cs"));
        var routeHandlersSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteHandlers.cs"));
        var routeServicesSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteServices.cs"));
        var competingGuardSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCompetingExecutionGuardService.cs"));
        var finalizerInputsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchFinalizerInputs.cs"));
        var finalizerAdapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchFinalizerAdapter.cs"));
        var combinedSource = string.Join(
            Environment.NewLine,
            executionSource,
            recoveryPacketsSource,
            concurrencySource,
            providerRecoverySource,
            executionAttemptLoopFacadeSource,
            providerRepairSource,
            directAgentModelsSource,
            directAgentAdapterSource,
            directAgentRuntimeSource,
            routeModelsSource,
            routeHandlersSource,
            routeServicesSource,
            competingGuardSource,
            finalizerInputsSource,
            finalizerAdapterSource);

        Assert.Contains("ProcessDispatchDirectAgentExecutionInput", directAgentModelsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(input.Candidate)", directAgentAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.FromDispatcherExecutionOutcome(executionOutcome)", directAgentAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchDirectAgentExecutionInput input", directAgentRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters.ToDispatcherCandidate", directAgentRuntimeSource, StringComparison.Ordinal);

        Assert.Contains("ShouldRetryIncompleteSuccessfulRun(", executionSource, StringComparison.Ordinal);
        Assert.Contains("ShouldRetryRecoverableFailedRun(", executionSource, StringComparison.Ordinal);
        Assert.Contains("TryCreateNoProgressRetrySignal(", executionSource, StringComparison.Ordinal);
        Assert.Contains("HasPriorNoProgressRetrySignalAsync(candidate, noProgressSignal, cancellationToken)", executionSource, StringComparison.Ordinal);
        Assert.Contains("PersistNoProgressRetryCompressedDiagnosticAsync(", executionSource, StringComparison.Ordinal);
        Assert.Contains("PersistNoProgressRetryObservedAsync(", executionSource, StringComparison.Ordinal);
        Assert.Contains("CreateRecoveryDecisionForRetry(", executionSource, StringComparison.Ordinal);
        Assert.Contains("CreateReworkPacketForDecision(", executionSource, StringComparison.Ordinal);
        Assert.Contains("BuildTypedRecoveryDirective(", executionSource, StringComparison.Ordinal);

        Assert.Contains("TryRepairAssignedAgentProvidersAsync(", executionSource, StringComparison.Ordinal);
        Assert.Contains("ProcessProviderRecoveryDirectiveBuilder.CreateRecoveryDecision(", executionSource, StringComparison.Ordinal);
        Assert.Contains("providerFallbackCount: 1", executionSource, StringComparison.Ordinal);
        Assert.Contains("BuildProviderRepairRecoveryDirective(", providerRecoverySource, StringComparison.Ordinal);
        Assert.Contains("new ProcessProviderRepairCoordinator(", executionAttemptLoopFacadeSource, StringComparison.Ordinal);
        Assert.Contains("ProcessAssignedAgentProviderRepairCoordinator", providerRepairSource, StringComparison.Ordinal);

        Assert.Contains("ProcessRouteExecutionRunSnapshot ExecutionRun", routeModelsSource, StringComparison.Ordinal);
        Assert.Contains("executionOutcome.ExecutionRun.Id", competingGuardSource, StringComparison.Ordinal);
        Assert.Contains("ProcessAutomationExecutionRunSelection.ResolveCompetingActiveAutomationExecutionRun", competingGuardSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(executionOutcome)", competingGuardSource, StringComparison.Ordinal);

        Assert.Contains("new ProcessDispatchDirectAgentFinalizerInput(", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteExecutionOutcome ExecutionOutcome", finalizerInputsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(input.ExecutionOutcome)", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchFinalizerContextFactory.ForDirectAgent", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("finalizerFacet.FinalizeDirectAgentCompletionAsync(", routeHandlersSource, StringComparison.Ordinal);

        Assert.DoesNotContain("CanDoItAll.Processes.Core", ReadDispatchSourceExcludingCorePureRuleAdapters(dispatchDirectory), StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverRegistry", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverRegistry", combinedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_dispatch_pre_execution_guard_gate_a_SB04_INV_001_locks_local_boundary_without_core_driver_or_viewport_drift()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var bundleRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-dispatch-pre-execution-guard-materialization-boundary-v1");
        var dispatchSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Dispatch.cs"));
        var routeServicesSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteServices.cs"));
        var routeHandlersSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.RouteHandlers.cs"));
        var preExecutionGuardHandlerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchPreExecutionGuardHandler.cs"));
        var targetSolution = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "01-target-solution.md"));
        var hardConstraints = File.ReadAllText(Path.Combine(bundleRoot, "requirements", "02-hard-constraints.md"));
        var sourceText = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
                .Where(path => Path.GetExtension(path) is ".cs" or ".csproj")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var proofRoot = Path.Combine(bundleRoot, "proof");
        var forbiddenPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet",
            "android",
            "iphone",
            "responsive"
        };
        IEnumerable<string> proofArtifactPaths = Directory.Exists(proofRoot)
            ? Directory.EnumerateFiles(proofRoot, "*", SearchOption.AllDirectories)
            : [];

        Assert.Contains("Dispatch.cs", targetSolution, StringComparison.Ordinal);
        Assert.Contains("database requirement blocker", targetSolution, StringComparison.Ordinal);
        Assert.Contains("upstream materialization coordinator", targetSolution, StringComparison.Ordinal);
        Assert.Contains("No Process Core project", hardConstraints, StringComparison.Ordinal);
        Assert.Contains("No production driver API", hardConstraints, StringComparison.Ordinal);
        Assert.DoesNotContain("BlockDispatchForDatabaseRequirementAsync", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryRequestMissingUpstreamArtifactMaterializationAsync", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("BlockDispatchForCurrentDatabaseRequirementAsync", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("TryRequestMissingUpstreamArtifactMaterializationAsync", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("CreatePreExecutionGuardHandler", routeHandlersSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessDispatchPreExecutionGuardHandler", preExecutionGuardHandlerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchDatabaseRequirementDecision", preExecutionGuardHandlerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchMissingUpstreamArtifactMaterializationPlan", preExecutionGuardHandlerSource, StringComparison.Ordinal);
        AssertNarrowProcessCoreSeed(root);
        Assert.False(Directory.Exists(Path.Combine(root, "src", "CanDoItAll.Modules.Processes.Core")));
        Assert.DoesNotContain("IProcessDriverPack", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", sourceText, StringComparison.Ordinal);
        foreach (var forbiddenDriverRuntimeToken in new[]
        {
            "IProcessDriverRegistry",
            "ProcessDriverRegistry",
            "ProcessDriverRuntimeSelector",
            "ProcessDriverManagerCommand",
            "ProcessDriverRuntime",
            "ProcessDriverProvider",
            "ProcessDriverHost",
            "ProcessDriverServiceCollectionExtensions",
            "AddProcessDriver",
            "MapProcessDriver"
        })
        {
            Assert.DoesNotContain(forbiddenDriverRuntimeToken, sourceText, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("CanDoItAll.Modules.Processes.Core", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain(proofArtifactPaths, path =>
        {
            var relativePath = Path.GetRelativePath(proofRoot, path);
            return forbiddenPathTokens.Any(token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Process_dispatch_claim_route_SB13_INV_001_extracts_finalizer_context_factory_with_route_field_parity()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var dispatchSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Dispatch.cs"));
        var factorySource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.FinalizerContextFactory.cs"));

        Assert.Contains("internal static class ProcessDispatchFinalizerContextFactory", factorySource, StringComparison.Ordinal);
        Assert.Contains("ForManagerArtifactRecovery", factorySource, StringComparison.Ordinal);
        Assert.Contains("ForDirectAgent", factorySource, StringComparison.Ordinal);
        Assert.Contains("ForWorkflow", factorySource, StringComparison.Ordinal);
        Assert.Contains("ForSubprocess", factorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("new ProcessStepCompletionFinalizerContext", dispatchSource, StringComparison.Ordinal);
        var routeExecutionSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.RouteExecution.cs"));
        var routeHandlerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.RouteHandlers.cs"));
        var finalizerApplicationSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchFinalizerApplicationService.cs"));

        Assert.Contains("CreateClaimedDispatchRouteHandlerPipeline", routeExecutionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchFinalizerContextFactory.ForWorkflow", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchFinalizerContextFactory.ForSubprocess", dispatchSource, StringComparison.Ordinal);
        var finalizerAdapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchFinalizerAdapter.cs"));

        Assert.Contains("ProcessDispatchFinalizerContextFactory.ForManagerArtifactRecovery", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchFinalizerContextFactory.ForDirectAgent", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchFinalizerContextFactory.ForWorkflow", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchFinalizerContextFactory.ForSubprocess", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchFinalizerContextFactory.", finalizerApplicationSource, StringComparison.Ordinal);

        var requiredFactoryFields = new[]
        {
            "ProcessStepCompletionExecutorKind.ManagerArtifactRecovery",
            "ProcessStepCompletionExecutorKind.DirectAgent",
            "ProcessStepCompletionExecutorKind.WorkflowBackedRole",
            "ProcessStepCompletionExecutorKind.SubprocessParent",
            "ProjectExecutionArtifacts: true",
            "ProjectExecutionArtifacts: false",
            "AllowManagerArtifactRecovery: true",
            "AllowManagerArtifactRecovery: false",
            "RecoveryExecutionRunId: recoveryOutcome.Detail.Run.Id",
            "RecoveredForExecutionRunId: candidate.RecoveryExecutionRunId",
            "WorkflowRunId: workflowOutcome.Link?.WorkflowRunId",
            "SubprocessRunId: subprocessRunId",
            "Trigger: \"workflow-execution-outcome\"",
            "Trigger: \"subprocess-execution-outcome\""
        };

        foreach (var field in requiredFactoryFields)
        {
            Assert.Contains(field, factorySource, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("FinalizeStepCompletionAsync", factorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyFinalizedStepTransitionAsync", factorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TransitionStepWithClaimAsync", factorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", factorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("executionClient", factorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("workflowRunCoordinator", factorySource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB007_INV_001_uses_route_finalizer_input_models()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var finalizerInputsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchFinalizerInputs.cs"));
        var finalizerApplicationSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchFinalizerApplicationService.cs"));
        var routeServicesSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteServices.cs"));
        var subprocessRuntimeSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchSubprocessRuntimeService.cs"));
        var normalizedRouteServicesSource = routeServicesSource.Replace("\r\n", "\n");

        var requiredInputRecords = new[]
        {
            "internal sealed record ProcessDispatchWorkflowFinalizerIntent(",
            "internal sealed record ProcessDispatchRecoveredFinalizerIntent(",
            "internal sealed record ProcessDispatchDirectAgentFinalizerIntent(",
            "internal sealed record ProcessDispatchSubprocessFinalizerIntent(",
            "internal sealed record ProcessDispatchWorkflowFinalizerInput(",
            "internal sealed record ProcessDispatchRecoveredFinalizerInput(",
            "internal sealed record ProcessDispatchDirectAgentFinalizerInput(",
            "internal sealed record ProcessDispatchSubprocessFinalizerInput("
        };

        foreach (var inputRecord in requiredInputRecords)
        {
            Assert.Contains(inputRecord, finalizerInputsSource, StringComparison.Ordinal);
        }

        Assert.Contains("ProcessRouteCandidate Candidate", finalizerInputsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchWorkflowFinalizerIntent Intent", finalizerInputsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRecoveredFinalizerIntent Intent", finalizerInputsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchDirectAgentFinalizerIntent Intent", finalizerInputsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchSubprocessFinalizerIntent Intent", finalizerInputsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteDispatchClaim DispatchClaim", finalizerInputsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessWorkflowExecutionOutcome WorkflowOutcome", finalizerInputsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteExecutionOutcome RecoveryOutcome", finalizerInputsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRouteExecutionOutcome ExecutionOutcome", finalizerInputsSource, StringComparison.Ordinal);
        Assert.Contains("Guid SubprocessRunId", finalizerInputsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchWorkflowFinalizerInput input", finalizerApplicationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRecoveredFinalizerInput input", finalizerApplicationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchDirectAgentFinalizerInput input", finalizerApplicationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchSubprocessFinalizerInput input", finalizerApplicationSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessDispatchRecoveredFinalizerInput(", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessDispatchWorkflowFinalizerInput(", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessDispatchDirectAgentFinalizerInput(", routeServicesSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchSubprocessRuntimeInput input", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessDispatchSubprocessFinalizerInput(", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("input.Candidate", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("input.DispatchClaim", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("routeCandidate is not null && routeDispatchClaim is not null", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FinalizeRecoveredCompletionAsync(\n            candidate,\n            recoveryOutcome",
            normalizedRouteServicesSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FinalizeWorkflowCompletionAsync(\n            candidate,\n            workflowOutcome",
            normalizedRouteServicesSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FinalizeDirectAgentCompletionAsync(\n            candidate,\n            executionOutcome",
            normalizedRouteServicesSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB008_INV_001_moves_dispatcher_aliases_to_finalizer_adapter()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var finalizerApplicationSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchFinalizerApplicationService.cs"));
        var finalizerAdapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchFinalizerAdapter.cs"));
        var dispatchSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Dispatch.cs"));
        var routeHandlersSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.RouteHandlers.cs"));
        var subprocessRuntimeSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchSubprocessRuntimeService.cs"));

        Assert.Contains("internal sealed class ProcessDispatchFinalizerApplicationService(ProcessDispatchFinalizerAdapter finalizerAdapter)", finalizerApplicationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("using DispatchCandidate", finalizerApplicationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("using DispatchExecutionOutcome", finalizerApplicationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("using ProcessStepDispatchClaim", finalizerApplicationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.", finalizerApplicationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters.", finalizerApplicationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ToDispatcherClaim", finalizerApplicationSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessDispatchFinalizerAdapter(", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("using DispatchCandidate = ProcessRunAutomationDispatchService.DispatchCandidate;", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("using DispatchExecutionOutcome = ProcessRunAutomationDispatchService.DispatchExecutionOutcome;", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("using ProcessStepDispatchClaim = ProcessRunAutomationDispatchService.ProcessStepDispatchClaim;", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherCandidate", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherClaim(input.DispatchClaim)", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static ProcessStepDispatchClaim ToDispatcherClaim", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new ProcessStepDispatchClaim(dispatchClaim.StepRunId, dispatchClaim.ClaimToken)", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchFinalizerContextFactory.ForWorkflow", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchFinalizerContextFactory.ForManagerArtifactRecovery", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchFinalizerContextFactory.ForDirectAgent", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchFinalizerContextFactory.ForSubprocess", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public async Task FinalizeWorkflowCompletionAsync(\r\n        DispatchCandidate candidate", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public async Task FinalizeRecoveredCompletionAsync(\r\n        DispatchCandidate candidate", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public async Task FinalizeDirectAgentCompletionAsync(\r\n        DispatchCandidate candidate", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public async Task FinalizeSubprocessCompletionAsync(\r\n        DispatchCandidate candidate", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("CreateFinalizerAdapter().FinalizeWorkflowCompletionAsync(", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("CreateFinalizerAdapter().FinalizeRecoveredCompletionAsync(", routeHandlersSource, StringComparison.Ordinal);
        Assert.Contains("CreateFinalizerAdapter().FinalizeDirectAgentCompletionAsync(", routeHandlersSource, StringComparison.Ordinal);
        Assert.Contains("CreateFinalizerApplicationService(finalizerAdapter)", routeHandlersSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchFinalizerAdapter finalizerAdapter", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("finalizerAdapter.FinalizeSubprocessCompletionAsync(", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchFinalizerApplicationService finalizerApplicationService", subprocessRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("finalizerApplicationService.FinalizeSubprocessCompletionAsync(", subprocessRuntimeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_stabilization_SB010_SB011_INV_001_confines_route_model_payload_bridge_to_adapters()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var routeModelAdapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteModelAdapters.cs"));
        var finalizerAdapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchFinalizerAdapter.cs"));
        var directAgentExecutionAdapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchDirectAgentExecutionAdapter.cs"));
        var finalizerApplicationSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchFinalizerApplicationService.cs"));
        var routeOwnedSource = string.Join(
            Environment.NewLine,
            new[]
            {
                "ProcessDispatchRouteModels.cs",
                "ProcessDispatchRouteFacets.cs",
                "ProcessDispatchRouteHandlers.cs",
                "ProcessDispatchRouteServices.cs",
                "ProcessDispatchDirectAgentRuntimeService.cs",
                "ProcessDispatchFinalizerApplicationService.cs"
            }.Select(file => File.ReadAllText(Path.Combine(dispatchDirectory, file))));

        Assert.Contains("RequireSource(CandidateSources, candidate, nameof(candidate)).Candidate", routeModelAdapterSource, StringComparison.Ordinal);
        Assert.Contains("RequireSource(DispatchClaimSources, dispatchClaim, nameof(dispatchClaim)).DispatchClaim", routeModelAdapterSource, StringComparison.Ordinal);
        Assert.Contains("RequireSource(ExecutionOutcomeSources, executionOutcome, nameof(executionOutcome)).ExecutionOutcome", routeModelAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(input.Candidate)", directAgentExecutionAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.FromDispatcherExecutionOutcome(executionOutcome)", directAgentExecutionAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(input.Candidate)", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherClaim(input.DispatchClaim)", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(input.ExecutionOutcome)", finalizerAdapterSource, StringComparison.Ordinal);

        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters.ToDispatcherCandidate", routeOwnedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters.ToDispatcherClaim", routeOwnedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome", routeOwnedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.DispatchCandidate", routeOwnedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.DispatchExecutionOutcome", routeOwnedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.ProcessStepDispatchClaim", routeOwnedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters", finalizerApplicationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static ProcessStepDispatchClaim ToDispatcherClaim", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new ProcessStepDispatchClaim(dispatchClaim.StepRunId, dispatchClaim.ClaimToken)", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", finalizerAdapterSource + directAgentExecutionAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", routeOwnedSource + finalizerAdapterSource + directAgentExecutionAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", routeOwnedSource + finalizerAdapterSource + directAgentExecutionAdapterSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_stabilization_SB013_SB014_INV_001_keeps_transition_intents_pure_and_adapter_owned()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var coreSource = ReadNarrowProcessCoreSource(root);
        var startTransitionSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchStartTransitionPlanner.cs"));
        var subprocessLifecycleSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessSubprocessLifecycleRules.cs"));
        var transitionAdapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessTransitionIntentAdapters.cs"));

        Assert.Contains("public readonly record struct ProcessDispatchStepTransitionIntent", coreSource, StringComparison.Ordinal);
        Assert.Contains("public static class ProcessDispatchTransitionIntentRules", coreSource, StringComparison.Ordinal);
        Assert.Contains("BuildStartTransitionIntent", coreSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessStepTransitionRequest", coreSource, StringComparison.Ordinal);
        Assert.Contains("ProcessTransitionIntentAdapters.ToTransitionRequest(", startTransitionSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchTransitionIntentRules.BuildStartTransitionIntent", startTransitionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new ProcessStepTransitionRequest", startTransitionSource, StringComparison.Ordinal);
        Assert.Contains("ProcessTransitionIntentAdapters.ToTransitionRequest(", subprocessLifecycleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new ProcessStepTransitionRequest", subprocessLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("global::CanDoItAll.Processes.Core.Routing.ProcessDispatchStepTransitionIntent", transitionAdapterSource, StringComparison.Ordinal);
        Assert.Contains("global::CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessParentTransitionFacts", transitionAdapterSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessStepTransitionRequest", transitionAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TransitionStepWithClaimAsync", coreSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", coreSource + transitionAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", coreSource + transitionAdapterSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_stabilization_SB019_SB020_INV_001_keeps_projection_validation_descriptors_pure_and_adapter_owned()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var coreDescriptorSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Processes.Core",
            "Artifacts",
            "ProcessArtifactProjectionValidationDescriptors.cs"));
        var adapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessArtifactValidationDescriptorAdapter.cs"));
        var finalizerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs"));
        var evidenceValidationSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessArtifactEvidenceValidationRules.cs"));
        var dispatchSideEffectSource = ReadDispatchSourceExcludingCorePureRuleAdapters(dispatchDirectory);
        var forbiddenCoreTokens = new[]
        {
            "CanDoItAll.Modules",
            "CanDoItAll.Infrastructure",
            "CanDoItAll.AgentFramework",
            "DbContext",
            "StoragePlacement",
            "IStorage",
            "WorkspacePathResolver",
            "WorkspaceScopeDescriptor",
            "File.",
            "Directory.",
            "ProcessRunAutomationDispatchService",
            "IProcessDriverPack",
            "DriverPack",
            "DriverRegistry",
            "ILogger<"
        };

        Assert.Contains("public sealed record ProcessArtifactProjectionEligibilityDescriptor", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessArtifactValidationRequirementDescriptor", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public static class ProcessArtifactProjectionEligibilityRules", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public static class ProcessArtifactValidationRequirementDescriptorRules", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public static class ProcessArtifactValidationPolicyRules", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactValidationDescriptorAdapter.ResolveArtifactExpectationMode", finalizerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactValidationDescriptorAdapter.ResolveArtifactProducerKind", finalizerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactValidationDescriptorAdapter.IsProducerAllowedForMode", evidenceValidationSource, StringComparison.Ordinal);
        Assert.Contains("global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityRules", adapterSource, StringComparison.Ordinal);
        Assert.Contains("global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptorRules", adapterSource, StringComparison.Ordinal);
        Assert.Contains("global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationPolicyRules", adapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", dispatchSideEffectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new ProcessArtifactProjectionWriteCoordinator", coreDescriptorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", coreDescriptorSource + adapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", coreDescriptorSource + adapterSource, StringComparison.Ordinal);
        Assert.All(forbiddenCoreTokens, token =>
            Assert.DoesNotContain(token, coreDescriptorSource, StringComparison.Ordinal));
    }

    [Fact]
    public void Process_core_evidence_descriptors_SB004_SB006_INV_001_keeps_execution_descriptors_pure_and_adapter_owned()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var coreDescriptorSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Processes.Core",
            "Execution",
            "ProcessExecutionEvidenceDescriptors.cs"));
        var adapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessExecutionEvidenceDescriptorAdapter.cs"));
        var executionSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Execution.cs"));
        var observedOutcomeSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessObservedExecutionOutcomeBuilder.cs"));
        var dispatchSideEffectSource = ReadDispatchSourceExcludingCorePureRuleAdapters(dispatchDirectory);
        var forbiddenCoreTokens = new[]
        {
            "CanDoItAll.Modules",
            "CanDoItAll.Infrastructure",
            "CanDoItAll.AgentFramework",
            "DbContext",
            "StoragePlacement",
            "IStorage",
            "WorkspacePathResolver",
            "File.",
            "Directory.",
            "ProcessRunAutomationDispatchService",
            "IProcessDriverPack",
            "DriverPack",
            "DriverRegistry",
            "ILogger<"
        };

        Assert.Contains("public sealed record ProcessExecutionRunEvidenceDescriptor", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessExecutionAttemptEvidenceDescriptor", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessExecutionCarriedProofDescriptor", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public static class ProcessExecutionEvidenceDescriptorRules", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("global::CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptorRules", adapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessExecutionEvidenceDescriptorAdapter.Describe(", executionSource, StringComparison.Ordinal);
        Assert.Contains("detail.Run", executionSource, StringComparison.Ordinal);
        Assert.Contains("postAttemptFacts", executionSource, StringComparison.Ordinal);
        Assert.Contains("attemptNumber", executionSource, StringComparison.Ordinal);
        Assert.Contains("ProcessExecutionEvidenceDescriptorAdapter.DescribeRun(run)", observedOutcomeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", dispatchSideEffectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteUntilSettledAsync", coreDescriptorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessAutomationExecutionClient", coreDescriptorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new DispatchExecutionOutcome", coreDescriptorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", coreDescriptorSource + adapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", coreDescriptorSource + adapterSource, StringComparison.Ordinal);
        Assert.All(forbiddenCoreTokens, token =>
            Assert.DoesNotContain(token, coreDescriptorSource, StringComparison.Ordinal));
    }

    [Fact]
    public void Process_core_finalizer_evidence_descriptors_SB007_SB009_INV_001_keeps_finalizer_descriptors_pure_and_adapter_owned()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var coreDescriptorSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Processes.Core",
            "Finalization",
            "ProcessFinalizerEvidenceDescriptors.cs"));
        var adapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessFinalizerEvidenceDescriptorAdapter.cs"));
        var finalizerAdapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchFinalizerAdapter.cs"));
        var dispatchSideEffectSource = ReadDispatchSourceExcludingCorePureRuleAdapters(dispatchDirectory);
        var forbiddenCoreTokens = new[]
        {
            "CanDoItAll.Modules",
            "CanDoItAll.Infrastructure",
            "CanDoItAll.AgentFramework",
            "DbContext",
            "StoragePlacement",
            "IStorage",
            "WorkspacePathResolver",
            "File.",
            "Directory.",
            "ProcessDispatchFinalizerAdapter",
            "ProcessDispatchFinalizerApplicationService",
            "ProcessStepCompletionFinalizerContext",
            "ProcessStepCompletionFinalizerResult",
            "finalizeStepCompletionAsync",
            "applyFinalizedStepTransitionAsync",
            "IProcessDriverPack",
            "DriverPack",
            "DriverRegistry",
            "ILogger<"
        };

        Assert.Contains("public enum ProcessCoreFinalizerKind", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public enum ProcessCoreFinalizerBlockCauseKind", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessFinalizerIntentEvidenceDescriptor", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessFinalizerResultEvidenceDescriptor", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public static class ProcessFinalizerEvidenceDescriptorRules", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("global::CanDoItAll.Processes.Core.Finalization.ProcessFinalizerEvidenceDescriptorRules", adapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessFinalizerEvidenceDescriptorAdapter.Describe(context, finalizedCompletion)", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("!finalizerEvidence.Result.ShouldApplyTransition", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.Contains("applyFinalizedStepTransitionAsync(candidate, finalizedCompletion, dispatchClaim, cancellationToken)", finalizerAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", dispatchSideEffectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessStepBlockCause", coreDescriptorSource, StringComparison.Ordinal);
        Assert.All(forbiddenCoreTokens, token =>
            Assert.DoesNotContain(token, coreDescriptorSource, StringComparison.Ordinal));
    }

    [Fact]
    public void Process_core_diagnostic_descriptors_SB010_SB012_INV_001_keeps_retry_provider_diagnostics_pure_and_adapter_owned()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var coreDescriptorSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Processes.Core",
            "Diagnostics",
            "ProcessRetryDiagnosticDescriptors.cs"));
        var adapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRetryDiagnosticDescriptorAdapter.cs"));
        var executionSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Execution.cs"));
        var dispatchSideEffectSource = ReadDispatchSourceExcludingCorePureRuleAdapters(dispatchDirectory);
        var forbiddenCoreTokens = new[]
        {
            "CanDoItAll.Modules",
            "CanDoItAll.Infrastructure",
            "CanDoItAll.AgentFramework",
            "AgentRecoveryDecision",
            "AgentFailureCategory",
            "ProcessAutomationExecutionRunDetail",
            "ProcessAutomationToolExecutionReceipt",
            "ProcessRecoveryRetryFacts",
            "NoProgressRetrySignal",
            "ProviderRepairOutcome",
            "ProcessProviderRepairCoordinator",
            "IProcessAutomationExecutionClient",
            "DbContext",
            "StoragePlacement",
            "IStorage",
            "WorkspacePathResolver",
            "File.",
            "Directory.",
            "IProcessDriverPack",
            "DriverPack",
            "DriverRegistry",
            "ILogger<"
        };

        Assert.Contains("public enum ProcessRetryDiagnosticFailureKind", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessRetryDiagnosticDescriptor", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessNoProgressRetryDiagnosticDescriptor", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessProviderRepairDiagnosticDescriptor", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public static class ProcessRetryDiagnosticDescriptorRules", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("global::CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptorRules", adapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRetryDiagnosticDescriptorAdapter.DescribeProviderRepair(providerRepair)", executionSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRetryDiagnosticDescriptorAdapter.DescribeRetry(", executionSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRetryDiagnosticDescriptorAdapter.DescribeNoProgressSignal(noProgressSignal)", executionSource, StringComparison.Ordinal);
        Assert.Contains("retryDiagnostics.ShouldRetry != shouldRetry", executionSource, StringComparison.Ordinal);
        Assert.Contains("noProgressSignal is not null && !noProgressDiagnostics.HasSignal", executionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", dispatchSideEffectSource, StringComparison.Ordinal);
        Assert.All(forbiddenCoreTokens, token =>
            Assert.DoesNotContain(token, coreDescriptorSource, StringComparison.Ordinal));
    }

    [Fact]
    public void Process_core_projection_evidence_descriptors_SB013_SB015_INV_001_keeps_projection_descriptors_pure_and_adapter_owned()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var coreDescriptorSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Processes.Core",
            "Artifacts",
            "ProcessArtifactProjectionEvidenceDescriptors.cs"));
        var adapterSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessArtifactProjectionEvidenceDescriptorAdapter.cs"));
        var lineageBuilderSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessArtifactProjectionLineageBuilder.cs"));
        var orchestratorSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessArtifactProjectionOrchestrator.cs"));
        var browserFactsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessProviderNativeBrowserOutputFacts.cs"));
        var dispatchSideEffectSource = ReadDispatchSourceExcludingCorePureRuleAdapters(dispatchDirectory);
        var forbiddenCoreTokens = new[]
        {
            "CanDoItAll.Modules",
            "CanDoItAll.Infrastructure",
            "CanDoItAll.AgentFramework",
            "ProcessArtifactProjectionLineageJson",
            "ProcessProviderNativeBrowserOutputFacts",
            "ProcessArtifactProjectionOrchestrator",
            "ProcessRunAutomationDispatchService",
            "DbContext",
            "StoragePlacement",
            "IStorage",
            "WorkspacePathResolver",
            "WorkspaceScopeDescriptor",
            "File.",
            "Directory.",
            "IProcessDriverPack",
            "DriverPack",
            "DriverRegistry",
            "ILogger<"
        };

        Assert.Contains("public enum ProcessCoreProviderNativeBrowserEvidenceKind", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessArtifactProjectionLineageDescriptor", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessArtifactProjectionSourceOrderDescriptor", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessProviderNativeBrowserEvidenceDescriptor", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("public static class ProcessArtifactProjectionEvidenceDescriptorRules", coreDescriptorSource, StringComparison.Ordinal);
        Assert.Contains("global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEvidenceDescriptorRules", adapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionEvidenceDescriptorAdapter.DescribeLineage(lineage)", lineageBuilderSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionEvidenceDescriptorAdapter.VerifyProjectionSourceOrder(projectionSourceOrder)", orchestratorSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionEvidenceDescriptorAdapter.DescribeProviderNativeBrowserEvidence", browserFactsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", dispatchSideEffectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryResolveArtifactFullPath", coreDescriptorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectAsync", coreDescriptorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new ProcessArtifactProjectionWriteRequest", coreDescriptorSource, StringComparison.Ordinal);
        Assert.All(forbiddenCoreTokens, token =>
            Assert.DoesNotContain(token, coreDescriptorSource, StringComparison.Ordinal));
    }

    [Fact]
    public void Process_core_stabilization_SB022_INV_001_limits_process_core_consumers_to_explicit_call_site_map()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var globalUsings = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "GlobalUsings.cs"));
        var allowedMap = File.ReadAllText(Path.Combine(
            root,
            "codex",
            "bundles",
            "process-core-stabilization-diagnostics-driver-roadmap-v1",
            "architecture",
            "05-core-consumer-allowed-call-site-map.md"));
        var currentBundleAllowedMap = File.ReadAllText(Path.Combine(
            root,
            "codex",
            "bundles",
            "process-core-evidence-descriptors-driver-contract-roadmap-v1",
            "architecture",
            "06-core-adapter-ownership-map.md"));
        var expectedFiles = CreateAllowedDispatchProcessCoreConsumerFiles()
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var actualFiles = Directory
            .EnumerateFiles(dispatchDirectory, "*.cs")
            .Where(path => File.ReadAllText(path).Contains("CanDoItAll.Processes.Core", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Where(static fileName => fileName is not null)
            .Select(static fileName => fileName!)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(expectedFiles, actualFiles);
        Assert.DoesNotContain("global using CanDoItAll.Processes.Core", globalUsings, StringComparison.Ordinal);
        Assert.DoesNotContain("*", allowedMap, StringComparison.Ordinal);
        Assert.DoesNotContain("*", currentBundleAllowedMap, StringComparison.Ordinal);
        foreach (var expectedFile in expectedFiles)
        {
            Assert.Contains($"| `{expectedFile}` |", allowedMap, StringComparison.Ordinal);
            Assert.Contains($"| `{expectedFile}` |", currentBundleAllowedMap, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Process_core_stabilization_SB023_INV_001_hardens_core_dependency_guard_against_runtime_and_driver_dependencies()
    {
        var root = FindRepositoryRoot();
        var coreRoot = Path.Combine(root, "src", "CanDoItAll.Processes.Core");
        var coreProject = File.ReadAllText(Path.Combine(coreRoot, "CanDoItAll.Processes.Core.csproj"));
        var coreSource = ReadNarrowProcessCoreSource(root);
        var forbiddenCoreSourcePatterns = new[]
        {
            @"CanDoItAll\.Modules",
            @"CanDoItAll\.Infrastructure",
            @"CanDoItAll\.AgentFramework",
            @"Microsoft\.EntityFrameworkCore",
            @"\bDbContext\b",
            @"\bIDbContextFactory\b",
            @"\bIStorage\b",
            @"StoragePlacement",
            @"WorkspacePathResolver",
            @"WorkspaceScopeDescriptor",
            @"\bFile\.",
            @"\bDirectory\.",
            @"ProcessRunAutomationDispatchService",
            @"IProcessDriver",
            @"DriverPack",
            @"DriverRegistry",
            @"ILogger<",
            @"IServiceProvider",
            @"IServiceScopeFactory"
        };

        Assert.Contains(
            @"<ProjectReference Include=""..\CanDoItAll.Processes.Contracts\CanDoItAll.Processes.Contracts.csproj"" />",
            coreProject,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", coreProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"..\CanDoItAll.Modules.", coreProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"..\CanDoItAll.Infrastructure\", coreProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"..\CanDoItAll.AgentFramework", coreProject, StringComparison.OrdinalIgnoreCase);

        foreach (var forbiddenPattern in forbiddenCoreSourcePatterns)
        {
            Assert.DoesNotMatch(forbiddenPattern, coreSource);
        }
    }

    [Fact]
    public void Process_core_stabilization_SB026_SB027_INV_001_keeps_driver_contract_proposal_non_production()
    {
        var root = FindRepositoryRoot();
        var srcRoot = Path.Combine(root, "src");
        var bundleRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-core-stabilization-diagnostics-driver-roadmap-v1");
        var sourceText = ReadProductionSourceText(root);
        var contractProposal = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "06-driver-contract-proposal.md"));
        var negativeScenarios = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "07-driver-permission-negative-scenarios.md"));
        var executionReport = File.ReadAllText(Path.Combine(bundleRoot, "reviews", "01-execution-report.md"));
        var docsText = string.Join(Environment.NewLine, contractProposal, negativeScenarios);
        var forbiddenProductionTokens = new[]
        {
            "IProcessDriver",
            "ProcessDriverPack",
            "DriverPack",
            "ProcessDriverRegistry",
            "IProcessHelperDriver",
            "IProcessSwDevHelperDriver",
            "IProcessDotNetSwDevHelperDriver",
            "MapProcessDriver",
            "ProcessDriverSelector",
            "ProcessHelperDriverSelector",
            "ProcessDriverRuntimeSelector"
        };

        AssertNarrowProcessCoreSeed(root);
        Assert.False(Directory.Exists(Path.Combine(srcRoot, "CanDoItAll.Modules.Processes.Core")));
        Assert.All(forbiddenProductionTokens, token =>
            Assert.DoesNotContain(token, sourceText, StringComparison.Ordinal));
        Assert.Contains("docs/tests-only readiness material", contractProposal, StringComparison.Ordinal);
        Assert.Contains("Verification-only", contractProposal, StringComparison.Ordinal);
        Assert.Contains("Manager-readonly", contractProposal, StringComparison.Ordinal);
        Assert.Contains("Execution-capable future gate", contractProposal, StringComparison.Ordinal);
        Assert.Contains("Absence of production driver APIs is the intended state.", negativeScenarios, StringComparison.Ordinal);
        Assert.Contains("Absence of a mode is denied", negativeScenarios, StringComparison.Ordinal);
        Assert.DoesNotContain("public interface", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("MapProcessDriver", docsText, StringComparison.Ordinal);
        Assert.Contains("| SB025 | Passed | Passed |", executionReport, StringComparison.Ordinal);
        Assert.Contains("| SB026 | Passed | Passed |", executionReport, StringComparison.Ordinal);
        Assert.Contains("| SB027 | Passed | Passed |", executionReport, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_stabilization_SB028_SB030_INV_001_keeps_domain_lane_maps_read_only_and_side_effect_denied()
    {
        var root = FindRepositoryRoot();
        var srcRoot = Path.Combine(root, "src");
        var bundleRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-core-stabilization-diagnostics-driver-roadmap-v1");
        var sourceText = ReadProductionSourceText(root);
        var dotNetRustLaneMap = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "08-driver-lane-map-dotnet-rust.md"));
        var officeBusinessLaneMap = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "09-driver-lane-map-office-business-analysis.md"));
        var laneClosure = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "10-driver-domain-lane-closure.md"));
        var executionReport = File.ReadAllText(Path.Combine(bundleRoot, "reviews", "01-execution-report.md"));
        var docsText = string.Join(Environment.NewLine, dotNetRustLaneMap, officeBusinessLaneMap, laneClosure);
        var forbiddenProductionTokens = new[]
        {
            "IProcessDriver",
            "ProcessDriverPack",
            "DriverPack",
            "ProcessDriverRegistry",
            "IProcessHelperDriver",
            "IProcessSwDevHelperDriver",
            "IProcessDotNetSwDevHelperDriver",
            "MapProcessDriver",
            "ProcessDriverSelector",
            "ProcessHelperDriverSelector",
            "ProcessDriverRuntimeSelector"
        };

        AssertNarrowProcessCoreSeed(root);
        Assert.False(Directory.Exists(Path.Combine(srcRoot, "CanDoItAll.Modules.Processes.Core")));
        Assert.All(forbiddenProductionTokens, token =>
            Assert.DoesNotContain(token, sourceText, StringComparison.Ordinal));
        Assert.Contains("read-only evidence schemas", dotNetRustLaneMap, StringComparison.Ordinal);
        Assert.Contains("No shell execution driver is approved.", dotNetRustLaneMap, StringComparison.Ordinal);
        Assert.Contains(".NET Verification Lane", dotNetRustLaneMap, StringComparison.Ordinal);
        Assert.Contains("Rust Verification Lane", dotNetRustLaneMap, StringComparison.Ordinal);
        Assert.Contains("No Office API integration is approved.", officeBusinessLaneMap, StringComparison.Ordinal);
        Assert.Contains("No connector or Graph runtime work is approved.", officeBusinessLaneMap, StringComparison.Ordinal);
        Assert.Contains("Office Evidence Lane", officeBusinessLaneMap, StringComparison.Ordinal);
        Assert.Contains("Business-Analysis Evidence Lane", officeBusinessLaneMap, StringComparison.Ordinal);
        Assert.Contains("Side-Effect Denial Matrix", laneClosure, StringComparison.Ordinal);
        Assert.DoesNotContain("public interface", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("MapProcessDriver", docsText, StringComparison.Ordinal);
        Assert.Contains("| SB028 | Passed | Passed |", executionReport, StringComparison.Ordinal);
        Assert.Contains("| SB029 | Passed | Passed |", executionReport, StringComparison.Ordinal);
        Assert.Contains("| SB030 | Passed | Passed |", executionReport, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_stabilization_SB034_SB036_INV_001_closes_final_handoff_with_scorecard_and_driver_denial()
    {
        var root = FindRepositoryRoot();
        var srcRoot = Path.Combine(root, "src");
        var bundleRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-core-stabilization-diagnostics-driver-roadmap-v1");
        var rootReadme = File.ReadAllText(Path.Combine(bundleRoot, "README.md"));
        var executionReport = File.ReadAllText(Path.Combine(bundleRoot, "reviews", "01-execution-report.md"));
        var redTeamReview = File.ReadAllText(Path.Combine(bundleRoot, "reviews", "02-red-team-review.md"));
        var scorecard = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "11-core-readiness-scorecard-vnext.md"));
        var driverDecision = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "12-driver-contract-implementation-decision.md"));
        var rawNoteClosure = File.ReadAllText(Path.Combine(bundleRoot, "traceability", "02-final-raw-note-closure.md"));
        var proofIndex = File.ReadAllText(Path.Combine(bundleRoot, "proof", "INDEX.md"));
        var gateSection = ExtractMarkdownSection(executionReport, "## Subbundle Gate Results");
        var rawNoteSection = ExtractMarkdownSection(executionReport, "## Raw Note Closure");
        var sourceText = ReadProductionSourceText(root);
        var forbiddenProductionTokens = new[]
        {
            "IProcessDriver",
            "ProcessDriverPack",
            "DriverPack",
            "ProcessDriverRegistry",
            "IProcessHelperDriver",
            "IProcessSwDevHelperDriver",
            "IProcessDotNetSwDevHelperDriver",
            "MapProcessDriver",
            "ProcessDriverSelector",
            "ProcessHelperDriverSelector",
            "ProcessDriverRuntimeSelector"
        };

        AssertNarrowProcessCoreSeed(root);
        Assert.False(Directory.Exists(Path.Combine(srcRoot, "CanDoItAll.Modules.Processes.Core")));
        Assert.All(forbiddenProductionTokens, token =>
            Assert.DoesNotContain(token, sourceText, StringComparison.Ordinal));

        foreach (var subbundleId in Enumerable.Range(1, 36).Select(static index => $"SB{index:000}"))
        {
            Assert.Contains($"| {subbundleId} | Passed | Passed |", gateSection, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Pending", gateSection, StringComparison.Ordinal);
        Assert.DoesNotContain("Pending", rawNoteSection, StringComparison.Ordinal);
        Assert.Contains("Execution status: `Completed`", rootReadme, StringComparison.Ordinal);
        Assert.Contains("Final closure gate: `Passed completed-stage validator`", rootReadme, StringComparison.Ordinal);
        Assert.Contains("Recommendation: proceed to next narrow Core expansion", scorecard, StringComparison.Ordinal);
        Assert.Contains("Production driver-contract implementation: not ready.", driverDecision, StringComparison.Ordinal);
        Assert.Contains("Runtime dispatch remains denied", driverDecision, StringComparison.Ordinal);
        Assert.Contains("Proceed to next narrow Core expansion.", redTeamReview, StringComparison.Ordinal);
        Assert.Contains("Production driver-contract implementation remains denied", redTeamReview, StringComparison.Ordinal);
        Assert.Contains("No process-specific warning, Core boundary failure, production driver API, or UI/media drift remains open", rawNoteClosure, StringComparison.Ordinal);
        Assert.Contains("SB036", proofIndex, StringComparison.Ordinal);
        Assert.Contains("Completed-stage validator", proofIndex, StringComparison.Ordinal);
    }

    [Fact]
    public void Step_completion_finalizer_gate_a_SB04_INV_001_records_live_inventory_and_blocks_core_driver_or_viewport_drift()
    {
        var root = FindRepositoryRoot();
        var inventory = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-dispatch-step-completion-finalizer-boundary-v1",
            "inventories",
            "02-finalizer-method-classification-template.md");
        var targetSolution = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-dispatch-step-completion-finalizer-boundary-v1",
            "architecture",
            "01-target-solution.md");
        var mafProject = ReadRepositoryFile(
            "src",
            "CanDoItAll.AgentFramework.Maf",
            "CanDoItAll.AgentFramework.Maf.csproj");
        var sourceText = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
                .Where(path => Path.GetExtension(path) is ".cs" or ".csproj")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var proofRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-dispatch-step-completion-finalizer-boundary-v1",
            "proof");
        var forbiddenPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet",
            "android",
            "iphone",
            "responsive"
        };
        IEnumerable<string> proofArtifactPaths = Directory.Exists(proofRoot)
            ? Directory.EnumerateFiles(proofRoot, "*", SearchOption.AllDirectories)
            : [];

        Assert.Contains("2091 lines", ReadRepositoryFile(
            "codex",
            "bundles",
            "process-dispatch-step-completion-finalizer-boundary-v1",
            "inventories",
            "01-source-impact-inventory.md"), StringComparison.Ordinal);
        Assert.Contains("StorageBackedProcessArtifactContentReader", inventory, StringComparison.Ordinal);
        Assert.Contains("PersistRuntimeInvariantAuditAsync", inventory, StringComparison.Ordinal);
        Assert.Contains("ProcessStepTransitionRequest", inventory, StringComparison.Ordinal);
        Assert.Contains("ProcessRunAutomationDispatchService.StepCompletionFinalizer.Types.cs", targetSolution, StringComparison.Ordinal);
        Assert.Contains("No Process Core extraction", targetSolution, StringComparison.Ordinal);
        Assert.Contains("No public type promotion", targetSolution, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.Processes", mafProject, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.Projects", mafProject, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.Workbench", mafProject, StringComparison.Ordinal);
        Assert.DoesNotContain("interface IProcessDriverPack", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("class ProcessDriverPack", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain(proofArtifactPaths, path =>
        {
            var relativePath = Path.GetRelativePath(proofRoot, path);
            return forbiddenPathTokens.Any(token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Step_completion_finalizer_gate_a_SB04_INV_002_preserves_nested_type_surface_before_movement()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var source = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(dispatchDirectory, "ProcessRunAutomationDispatchService.StepCompletionFinalizer*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.Contains("internal enum ProcessStepCompletionExecutorKind", source, StringComparison.Ordinal);
        Assert.Contains("internal enum ProcessArtifactExpectationMode", source, StringComparison.Ordinal);
        Assert.Contains("internal enum ProcessArtifactValidationStatus", source, StringComparison.Ordinal);
        Assert.Contains("internal enum ProcessArtifactFailureOwnership", source, StringComparison.Ordinal);
        Assert.Contains("internal enum ProcessArtifactProducerKind", source, StringComparison.Ordinal);
        Assert.Contains("internal interface IProcessArtifactContentReader", source, StringComparison.Ordinal);
        Assert.Contains("internal sealed class WorkspaceProcessArtifactContentReader", source, StringComparison.Ordinal);
        Assert.Contains("internal sealed class StorageBackedProcessArtifactContentReader", source, StringComparison.Ordinal);
        Assert.Contains("ProcessStepCompletionFinalizerContext", source, StringComparison.Ordinal);
        Assert.Contains("ProcessStepCompletionFinalizerResult", source, StringComparison.Ordinal);
        Assert.Contains("ProcessStepTransitionArtifactValidationContext", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Step_completion_finalizer_gate_b_SB08_INV_001_extracts_types_and_readers_without_surface_or_stub_drift()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var mainSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs"));
        var typeSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessRunAutomationDispatchService.StepCompletionFinalizer.Types.cs"));
        var readerSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessRunAutomationDispatchService.StepCompletionFinalizer.ArtifactContentReaders.cs"));
        var extractedSource = string.Join(Environment.NewLine, typeSource, readerSource);

        Assert.True(mainSource.Split(Environment.NewLine).Length < 2091);
        Assert.DoesNotContain("internal enum ProcessStepCompletionExecutorKind", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("internal interface IProcessArtifactContentReader", mainSource, StringComparison.Ordinal);
        Assert.Contains("internal enum ProcessStepCompletionExecutorKind", typeSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessArtifactExpectationValidationResult", typeSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessStepCompletionFinalizerContext", typeSource, StringComparison.Ordinal);
        Assert.Contains("internal interface IProcessArtifactContentReader", readerSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class WorkspaceProcessArtifactContentReader", readerSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class StorageBackedProcessArtifactContentReader", readerSource, StringComparison.Ordinal);
        Assert.Contains("WorkspaceScopeDescriptor.NormalizeRelativePath", readerSource, StringComparison.Ordinal);
        Assert.Contains("StorageJson.TryParseReference", readerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public enum ProcessStepCompletionExecutorKind", extractedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", extractedSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NotImplementedException", extractedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", extractedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", extractedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Step_completion_finalizer_gate_c_SB12_INV_001_extracts_validation_invariant_and_transition_helpers_with_field_parity()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var mainSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs"));
        var validationSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessRunAutomationDispatchService.StepCompletionFinalizer.ValidationOrchestration.cs"));
        var invariantSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessRunAutomationDispatchService.StepCompletionFinalizer.RuntimeInvariantAudit.cs"));
        var transitionSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessRunAutomationDispatchService.StepCompletionFinalizer.TransitionRequestBuilder.cs"));
        var helperSource = string.Join(Environment.NewLine, validationSource, invariantSource, transitionSource);

        Assert.Contains("ValidateRequiredCompletionArtifactsAsync", validationSource, StringComparison.Ordinal);
        Assert.Contains("StorageBackedProcessArtifactContentReader", validationSource, StringComparison.Ordinal);
        Assert.Contains("ResolveWorkflowRunIdForStep", validationSource, StringComparison.Ordinal);
        Assert.Contains("ResolveSubprocessRunIdForStep", validationSource, StringComparison.Ordinal);
        Assert.Contains("PersistRuntimeInvariantAuditAsync", invariantSource, StringComparison.Ordinal);
        Assert.Contains("RuntimeInvariantViolationRecorded", invariantSource, StringComparison.Ordinal);
        Assert.Contains("ProcessConformanceSeverity.High or ProcessConformanceSeverity.Critical", mainSource, StringComparison.Ordinal);
        Assert.Contains("BuildFinalizedStepTransitionRequest", transitionSource, StringComparison.Ordinal);
        Assert.Contains("BuildStepTransitionArtifactValidationContext", transitionSource, StringComparison.Ordinal);
        Assert.Contains("BuildFinalizedStepTransitionRequest(candidate, finalizerResult)", mainSource, StringComparison.Ordinal);

        var requiredTransitionFields = new[]
        {
            "ArtifactValidationExecutorKind",
            "ArtifactValidationExecutionRunId",
            "ArtifactValidationWorkflowRunId",
            "ArtifactValidationSubprocessRunId",
            "ArtifactValidationRecoveryExecutionRunId",
            "ArtifactValidationRecoveredForExecutionRunId"
        };

        foreach (var field in requiredTransitionFields)
        {
            Assert.Contains(field, transitionSource, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("private async Task<IReadOnlyList<ProcessArtifactExpectationValidationResult>> ValidateRequiredCompletionArtifactsAsync", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private async Task<IReadOnlyList<RuntimeInvariantViolation>> PersistRuntimeInvariantAuditAsync", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void ApplyArtifactValidationContext", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", helperSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NotImplementedException", helperSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_validation_matcher_core_uses_validation_snapshot_expectations()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactValidation.cs");
        var snapshotSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactValidationSnapshot.cs");

        Assert.Contains("ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectations", source, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts", source, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactExpectationSnapshot expectedArtifact", source, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessArtifactExpectationSnapshot", snapshotSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ToProjectionExpectation", snapshotSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FromProjectionExpectation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static ProcessArtifactExpectationSnapshot ToProjectionExpectation", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB023_INV_001_converges_validation_projection_and_satisfaction_expectation_snapshots()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var validationSnapshotSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactValidationSnapshot.cs");
        var validationBuilderSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactValidationSnapshotBuilder.cs");
        var projectionModelsSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessProjectionModels.cs");
        var satisfactionSnapshotSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactSatisfactionSnapshot.cs");
        var matcherSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactExpectationMatcher.cs");
        var resolverSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactExpectationResolver.cs");

        Assert.False(File.Exists(Path.Combine(dispatchDirectory, "ProcessProjectionArtifactExpectation.cs")));
        Assert.Contains("internal sealed record ProcessArtifactExpectationSnapshot", validationSnapshotSource, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<ProcessArtifactExpectationSnapshot> ExpectedArtifacts", validationSnapshotSource, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<ProcessArtifactExpectationSnapshot> ExpectedArtifacts", projectionModelsSource, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<ProcessArtifactExpectationSnapshot> ExpectedArtifacts", satisfactionSnapshotSource, StringComparison.Ordinal);
        Assert.Contains(".Select(ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation)", projectionModelsSource, StringComparison.Ordinal);
        Assert.Contains(".Select(ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation)", satisfactionSnapshotSource, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts", matcherSource, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts", resolverSource, StringComparison.Ordinal);

        var combinedSource = string.Join(
            Environment.NewLine,
            validationSnapshotSource,
            validationBuilderSource,
            projectionModelsSource,
            satisfactionSnapshotSource,
            matcherSource,
            resolverSource);
        Assert.DoesNotContain("ProcessProjectionArtifactExpectation", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessArtifactValidationExpectation", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ToProjectionExpectation", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FromProjectionExpectation", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.DispatchArtifactExpectation> ExpectedArtifacts", satisfactionSnapshotSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB024_INV_001_preserves_projection_validation_dto_parity_paths()
    {
        var orchestratorSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProjectionOrchestrator.cs");
        var sourceAdaptersSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProjectionSourceAdapters.cs");
        var lineageSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProjectionLineageBuilder.cs");
        var providerNativeSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessProviderNativeBrowserArtifactProjectionCoordinator.cs");
        var satisfactionSnapshotSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactSatisfactionSnapshot.cs");
        var validationSnapshotSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactValidationSnapshot.cs");
        var projectionModelsSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessProjectionModels.cs");

        var sourceFamilies = new[]
        {
            "new ProcessExecutionArtifactProjectionCoordinator(",
            "new ProcessMockArtifactProjectionCoordinator(",
            "new ProcessWorkspaceWrittenArtifactProjectionCoordinator(",
            "existingManagedCoordinator,",
            "new ProcessResponseTextArtifactProjectionCoordinator(",
            "new ProcessProviderNativeBrowserArtifactProjectionCoordinator(",
            "new ProcessCompletedDecisionArtifactCoordinator("
        };
        var lastIndex = -1;
        foreach (var sourceFamily in sourceFamilies)
        {
            var currentIndex = orchestratorSource.IndexOf(sourceFamily, StringComparison.Ordinal);

            Assert.True(currentIndex > lastIndex, sourceFamily);
            lastIndex = currentIndex;
        }

        Assert.Contains("internal sealed record ProcessArtifactExpectationSnapshot", validationSnapshotSource, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<ProcessArtifactExpectationSnapshot> ExpectedArtifacts", projectionModelsSource, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<ProcessArtifactExpectationSnapshot> ExpectedArtifacts", satisfactionSnapshotSource, StringComparison.Ordinal);
        Assert.Contains(".Select(ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation)", satisfactionSnapshotSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessProjectionArtifactExpectation", validationSnapshotSource + projectionModelsSource + satisfactionSnapshotSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessArtifactValidationExpectation", validationSnapshotSource + projectionModelsSource + satisfactionSnapshotSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ToProjectionExpectation", validationSnapshotSource + projectionModelsSource + satisfactionSnapshotSource, StringComparison.Ordinal);

        Assert.Contains("ProcessArtifactProjectionLineageBuilder.ApplyRecoveryLineage", sourceAdaptersSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionLineageBuilder.BuildLineage", sourceAdaptersSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionSourceKind.ProcessMock", sourceAdaptersSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionSourceKind.WorkspaceWrite", sourceAdaptersSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionSourceKind.ExistingManagedFile", sourceAdaptersSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionSourceKind.AssistantResponse", sourceAdaptersSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionSourceKind.ProviderNativeBrowser", sourceAdaptersSource, StringComparison.Ordinal);
        Assert.Contains("manager-recovery-artifact|sha256:", lineageSource, StringComparison.Ordinal);
        Assert.Contains("SourceExternalReferenceKey = sourceExternalReferenceKey", lineageSource, StringComparison.Ordinal);

        Assert.Contains("context.Observations.SuccessfulBrowserToolOutputFiles", providerNativeSource, StringComparison.Ordinal);
        Assert.Contains("context.Observations.ProviderNativeBrowserWorkingDirectory", providerNativeSource, StringComparison.Ordinal);
        Assert.Contains("ProviderNativeBrowserArtifactProjectionSourceAdapter.PlanExpectedOutput", providerNativeSource, StringComparison.Ordinal);
        Assert.Contains("ProviderNativeBrowserArtifactProjectionSourceAdapter.PlanDiscoveredOutput", providerNativeSource, StringComparison.Ordinal);
        Assert.Contains("candidateState.TryApplyExpectedWriteOutcome", providerNativeSource, StringComparison.Ordinal);
        Assert.Contains("candidateState.TryApplyWriteOutcome", providerNativeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessProjectionSessionObservationSource sessionObservationSource", providerNativeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.DispatchArtifactExpectation> ExpectedArtifacts", satisfactionSnapshotSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_validation_path_rules_are_local_and_do_not_own_file_or_storage_effects()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactPathValidationRules.cs");

        Assert.Contains("internal static class ProcessArtifactPathValidationRules", source, StringComparison.Ordinal);
        Assert.Contains("NormalizeManagedPathReference", source, StringComparison.Ordinal);
        Assert.Contains("IsShallowSharedManagedArtifactPath", source, StringComparison.Ordinal);
        Assert.Contains("TryExtractExpectedArtifactRelativePath", source, StringComparison.Ordinal);
        Assert.Contains("ExpectedArtifactExplicitlyTargetsPath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchArtifactExpectation", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_validation_text_match_rules_are_local_and_do_not_own_orchestration_effects()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactTextMatchRules.cs");

        Assert.Contains("internal static class ProcessArtifactTextMatchRules", source, StringComparison.Ordinal);
        Assert.Contains("HasExpectedArtifactContentSignals", source, StringComparison.Ordinal);
        Assert.Contains("MatchesExpectedArtifactByTitleTokens", source, StringComparison.Ordinal);
        Assert.Contains("TokenizeArtifactComparisonText", source, StringComparison.Ordinal);
        Assert.Contains("SharesNarrativeArtifactPurpose", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchArtifactExpectation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_validation_provider_native_visual_rules_are_local_and_do_not_own_orchestration_effects()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProviderNativeVisualValidationRules.cs");
        var dispatcherSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactValidation.cs");

        Assert.Contains("internal static class ProcessArtifactProviderNativeVisualValidationRules", source, StringComparison.Ordinal);
        Assert.Contains("ScoreProviderNativeVisualArtifactExpectation", source, StringComparison.Ordinal);
        Assert.Contains("ResolveProviderNativeBrowserToolName", source, StringComparison.Ordinal);
        Assert.Contains("IsProviderNativeBrowserArtifactPath", source, StringComparison.Ordinal);
        Assert.Contains("ContainsScreenshotArtifactSignal", source, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProviderNativeVisualValidationRules.ScoreProviderNativeVisualArtifactExpectation", dispatcherSource, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchArtifactExpectation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_validation_quality_rules_are_local_and_do_not_own_orchestration_effects()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactQualityValidationRules.cs");
        var dispatcherSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactValidation.cs");

        Assert.Contains("internal static class ProcessArtifactQualityValidationRules", source, StringComparison.Ordinal);
        Assert.Contains("ResolveInvalidQualityValidationProofSummary", source, StringComparison.Ordinal);
        Assert.Contains("ContainsZeroTestRunEvidence", source, StringComparison.Ordinal);
        Assert.Contains("ContainsBuildWarningEvidence", source, StringComparison.Ordinal);
        Assert.Contains("IsPlaceholderCriticalToolRequestSummary", source, StringComparison.Ordinal);
        Assert.Contains("ContainsConcreteBrowserProofSignal", source, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactQualityValidationRules.ResolveInvalidQualityValidationProofSummary", dispatcherSource, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchArtifactExpectation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_satisfaction_boundary_helpers_are_module_local_and_side_effect_free()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var artifactValidationSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessRunAutomationDispatchService.ArtifactValidation.cs"));
        var helperFiles = new[]
        {
            "ProcessArtifactSatisfactionSnapshot.cs",
            "ProcessArtifactRecordedSatisfactionRules.cs",
            "ProcessFreshImplementationArtifactSatisfactionRules.cs",
            "ProcessRequiredArtifactAutoSatisfactionRules.cs",
            "ProcessResponseTextArtifactSatisfactionRules.cs",
            "ProcessManagedArtifactPathClassificationRules.cs",
            "ProcessQualityValidationEvidenceAggregator.cs",
            "ProcessIncompleteImplementationSignalRules.cs",
            "ProcessExternalTargetReferenceGuard.cs",
            "ProcessShallowManagedArtifactReferenceGuard.cs",
            "ProcessArtifactSatisfactionBlockerSummaryBuilder.cs"
        };
        var helperSource = string.Join(
            Environment.NewLine,
            helperFiles.Select(fileName =>
            {
                var path = Path.Combine(dispatchDirectory, fileName);
                Assert.True(File.Exists(path), path);
                return File.ReadAllText(path);
            }));
        var recordedSatisfactionRulesSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessArtifactRecordedSatisfactionRules.cs"));
        var sideEffectHelperSource = helperSource.Replace(recordedSatisfactionRulesSource, string.Empty);
        var forbiddenTokens = new[]
        {
            "CanDoItAll.Processes.Core",
            "IProcessDriverPack",
            "DriverPack",
            "ProcessDriver",
            "DbContext",
            "CreateDbContextAsync",
            "SaveChangesAsync",
            "RecordArtifactAsync",
            "storagePlacementService",
            "File.",
            "Directory."
        };

        Assert.Contains("internal sealed record ProcessArtifactSatisfactionSnapshot", helperSource, StringComparison.Ordinal);
        Assert.Contains("global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordedSatisfactionRules", recordedSatisfactionRulesSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessRequiredArtifactAutoSatisfactionRules", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessQualityValidationEvidenceAggregator", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessIncompleteImplementationSignalRules", helperSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactSatisfactionSnapshotBuilder.From", artifactValidationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRequiredArtifactAutoSatisfactionRules.CanAutoSatisfyRequiredArtifact", artifactValidationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessQualityValidationEvidenceAggregator.ResolveEvidenceTexts", artifactValidationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessIncompleteImplementationSignalRules.ResolveIncompleteImplementationSummary", artifactValidationSource, StringComparison.Ordinal);

        foreach (var forbiddenToken in forbiddenTokens)
        {
            Assert.DoesNotContain(forbiddenToken, sideEffectHelperSource, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Artifact_validation_project_structure_requirement_rules_are_local_and_preserve_mandatory_source_lines()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProjectStructureRequirementValidationRules.cs");
        var dispatcherSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactValidation.cs");
        var rootSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.cs");

        Assert.Contains("internal static class ProcessArtifactProjectStructureRequirementValidationRules", source, StringComparison.Ordinal);
        Assert.Contains("ResolveDowngradedProjectStructureRequirementSummary", source, StringComparison.Ordinal);
        Assert.Contains("ResolveGroundedProjectStructureRequirementLines", source, StringComparison.Ordinal);
        Assert.Contains("IsNonMandatoryProjectStructureSourceLine", source, StringComparison.Ordinal);
        Assert.Contains("ContainsRequirementWeakeningPhrase", source, StringComparison.Ordinal);
        Assert.Contains("TokenizeProjectStructureRequirementText", source, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectStructureRequirementValidationRules.ResolveDowngradedProjectStructureRequirementSummary", dispatcherSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectStructureRequirementNoiseTokens", rootSource, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchArtifactExpectation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Execution_artifact_projection_path_uses_projection_planner_before_recording_artifact()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessExecutionArtifactProjectionCoordinator.cs");

        var coordinatorIndex = source.IndexOf("internal sealed class ProcessExecutionArtifactProjectionCoordinator", StringComparison.Ordinal);
        var plannerIndex = source.IndexOf("ProcessArtifactProjectionPlanner.PlanExecutionArtifact", coordinatorIndex, StringComparison.Ordinal);
        var writeIndex = source.IndexOf("context.WriteCoordinator.WriteAsync", plannerIndex, StringComparison.Ordinal);
        var stateIndex = source.IndexOf("candidateState.TryApplyWriteOutcome", writeIndex, StringComparison.Ordinal);

        Assert.True(coordinatorIndex >= 0);
        Assert.True(plannerIndex > coordinatorIndex);
        Assert.True(writeIndex > plannerIndex);
        Assert.True(stateIndex > writeIndex);
        Assert.Contains("IProcessProjectionClaimGuard claimGuard", source, StringComparison.Ordinal);
        Assert.Contains("IProcessProjectionPathResolver pathResolver", source, StringComparison.Ordinal);
        Assert.Contains("IProcessProjectionCandidateStateUpdater candidateState", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService dispatchService", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_projection_helpers_do_not_reference_dispatcher_nested_expectations()
    {
        var helperFiles = new[]
        {
            "ProcessArtifactExpectationMatcher.cs",
            "ProcessArtifactProjectionPlanner.cs",
            "ProcessArtifactProjectionSourceAdapters.cs"
        };

        foreach (var helperFile in helperFiles)
        {
            var source = ReadRepositoryFile(
                "src",
                "CanDoItAll.Modules.Processes",
                "Automation",
                "Dispatch",
                helperFile);

            Assert.DoesNotContain("ProcessRunAutomationDispatchService.DispatchArtifactExpectation", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DispatchArtifactExpectation", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Artifact_projection_source_adapters_are_local_and_used_by_migrated_source_paths()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var adapterPath = Path.Combine(dispatchDirectory, "ProcessArtifactProjectionSourceAdapters.cs");
        var shellSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactProjection.cs");
        var orchestratorSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProjectionOrchestrator.cs");
        var coordinatorSource = string.Join(
            Environment.NewLine,
            new[]
            {
                "ProcessExecutionArtifactProjectionCoordinator.cs",
                "ProcessMockArtifactProjectionCoordinator.cs",
                "ProcessWorkspaceWrittenArtifactProjectionCoordinator.cs",
                "ProcessExistingManagedArtifactProjectionCoordinator.cs",
                "ProcessResponseTextArtifactProjectionCoordinator.cs",
                "ProcessProviderNativeBrowserArtifactProjectionCoordinator.cs",
                "ProcessCompletedDecisionArtifactCoordinator.cs"
            }.Select(file => ReadRepositoryFile(
                "src",
                "CanDoItAll.Modules.Processes",
                "Automation",
                "Dispatch",
                file)));

        Assert.True(File.Exists(adapterPath));
        Assert.Contains("ProcessArtifactProjectionOrchestrator.CreateDefault", shellSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionFacetSet facets", orchestratorSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessExecutionArtifactProjectionCoordinator(", orchestratorSource, StringComparison.Ordinal);
        Assert.Contains("facets.ClaimGuard", orchestratorSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessMockArtifactProjectionCoordinator(", orchestratorSource, StringComparison.Ordinal);
        Assert.Contains("facets.ProcessMockRules", orchestratorSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessWorkspaceWrittenArtifactProjectionCoordinator(", orchestratorSource, StringComparison.Ordinal);
        Assert.Contains("facets.ProjectStructureMatcher", orchestratorSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessExistingManagedArtifactProjectionCoordinator(", orchestratorSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessResponseTextArtifactProjectionCoordinator(", orchestratorSource, StringComparison.Ordinal);
        Assert.Contains("facets.ResponseTextRules", orchestratorSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessProviderNativeBrowserArtifactProjectionCoordinator(", orchestratorSource, StringComparison.Ordinal);
        Assert.Contains("facets.BrowserOutputRules", orchestratorSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessCompletedDecisionArtifactCoordinator(", orchestratorSource, StringComparison.Ordinal);
        Assert.Contains("facets.DecisionArtifactRules", orchestratorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new ProcessExecutionArtifactProjectionCoordinator(this)", shellSource + coordinatorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService dispatchService", coordinatorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessArtifactProjectionHost", shellSource + orchestratorSource + coordinatorSource, StringComparison.Ordinal);
        Assert.Contains("ProcessMockArtifactProjectionSourceAdapter.Plan", coordinatorSource, StringComparison.Ordinal);
        Assert.Contains("WorkspaceWrittenArtifactProjectionSourceAdapter.Plan", coordinatorSource, StringComparison.Ordinal);
        Assert.Contains("ExistingManagedArtifactProjectionSourceAdapter.Plan", coordinatorSource, StringComparison.Ordinal);
        Assert.Contains("ResponseTextArtifactProjectionSourceAdapter.Plan", coordinatorSource, StringComparison.Ordinal);
        Assert.Contains("ProviderNativeBrowserArtifactProjectionSourceAdapter.PlanExpectedOutput", coordinatorSource, StringComparison.Ordinal);
        Assert.Contains("ProviderNativeBrowserArtifactProjectionSourceAdapter.PlanDiscoveredOutput", coordinatorSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_projection_source_family_order_stays_execution_mock_workspace_existing_response_browser_decision()
    {
        var orchestratorSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProjectionOrchestrator.cs");
        var sourceFamilies = new[]
        {
            "new ProcessExecutionArtifactProjectionCoordinator(",
            "new ProcessMockArtifactProjectionCoordinator(",
            "new ProcessWorkspaceWrittenArtifactProjectionCoordinator(",
            "existingManagedCoordinator,",
            "new ProcessResponseTextArtifactProjectionCoordinator(",
            "new ProcessProviderNativeBrowserArtifactProjectionCoordinator(",
            "new ProcessCompletedDecisionArtifactCoordinator("
        };

        var lastIndex = -1;
        foreach (var sourceFamily in sourceFamilies)
        {
            var currentIndex = orchestratorSource.IndexOf(sourceFamily, StringComparison.Ordinal);

            Assert.True(currentIndex > lastIndex, sourceFamily);
            lastIndex = currentIndex;
        }
    }

    [Fact]
    public void Artifact_projection_facets_use_focused_implementations_without_all_facet_service()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var dispatchSource = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(dispatchDirectory, "*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var facetSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProjectionFacetImplementations.cs");
        var shellSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactProjection.cs");
        var facetImplementationMatches = Regex.Matches(
            facetSource,
            @"internal sealed class\s+\w+[^{:]*:\s*(?<interfaces>[^{]+)",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        Assert.DoesNotContain("ProcessArtifactProjectionServices", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionFacetFactory.Create((claim, token) =>", shellSource, StringComparison.Ordinal);
        Assert.Contains("ProcessProjectionSnapshotBuilderAdapter.ToDispatchClaim(claim)", shellSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessArtifactProjectionFacetFactory.Create(EnsureStepDispatchClaimHeldAsync)", shellSource, StringComparison.Ordinal);
        Assert.Contains("internal delegate Task ProcessProjectionClaimGuardHandler", facetSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessProjectionClaimGuard", facetSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessProjectionPathResolver", facetSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessProjectionFileIo", facetSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessProjectionArtifactClassifier", facetSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessProjectionExpectationMatcher", facetSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessProjectionProcessMockRules", facetSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessProjectionProjectStructureMatcher", facetSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessProjectionSessionObservationSource", facetSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessProjectionResponseTextRules", facetSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessProjectionBrowserOutputRules", facetSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessProjectionDecisionArtifactRules", facetSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessProjectionLineageFactory", facetSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ProcessProjectionCandidateStateUpdater", facetSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService dispatchService", facetSource, StringComparison.Ordinal);

        foreach (Match match in facetImplementationMatches)
        {
            var implementedFacetCount = Regex.Matches(
                match.Groups["interfaces"].Value,
                @"\bIProcessProjection[A-Za-z]+\b",
                RegexOptions.CultureInvariant).Count;

            Assert.True(implementedFacetCount <= 1, match.Value);
        }
    }

    [Fact]
    public void Artifact_projection_boundary_keeps_dispatcher_nested_models_inside_snapshot_adapter()
    {
        var coordinatorFiles = new[]
        {
            "ProcessExecutionArtifactProjectionCoordinator.cs",
            "ProcessMockArtifactProjectionCoordinator.cs",
            "ProcessWorkspaceWrittenArtifactProjectionCoordinator.cs",
            "ProcessExistingManagedArtifactProjectionCoordinator.cs",
            "ProcessResponseTextArtifactProjectionCoordinator.cs",
            "ProcessProviderNativeBrowserArtifactProjectionCoordinator.cs",
            "ProcessCompletedDecisionArtifactCoordinator.cs"
        };
        var projectionBoundaryFiles = coordinatorFiles
            .Concat(new[]
            {
                "ProcessArtifactProjectionContext.cs",
                "ProcessArtifactProjectionFacets.cs",
                "ProcessArtifactProjectionFacetImplementations.cs",
                "ProcessArtifactProjectionCandidateState.cs"
            })
            .ToList();
        var forbiddenBoundaryTokens = new[]
        {
            "using DispatchCandidate =",
            "using DispatchArtifactExpectation =",
            "ProcessRunAutomationDispatchService.DispatchCandidate",
            "ProcessRunAutomationDispatchService.DispatchArtifactExpectation",
            "ProcessRunAutomationDispatchService.ProcessStepDispatchClaim",
            "ProcessRunAutomationDispatchService.ProcessMockArtifactProjection",
            "ProcessRunAutomationDispatchService.SessionFileContent",
            "DispatchArtifactExpectation ",
            "DispatchCandidate ",
            "ProcessStepDispatchClaim ",
            "ProcessMockArtifactProjection ",
            "new SessionFileContent(",
            "context.Detail"
        };

        foreach (var file in projectionBoundaryFiles)
        {
            var source = ReadRepositoryFile(
                "src",
                "CanDoItAll.Modules.Processes",
                "Automation",
                "Dispatch",
                file);

            foreach (var forbiddenToken in forbiddenBoundaryTokens)
            {
                Assert.DoesNotContain(forbiddenToken, source, StringComparison.Ordinal);
            }
        }

        var adapterSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessProjectionModels.cs");
        var shellSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactProjection.cs");

        Assert.Contains("ProcessProjectionSnapshotBuilderAdapter", adapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRunAutomationDispatchService.DispatchCandidate", adapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRunAutomationDispatchService.ProcessStepDispatchClaim", adapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRunAutomationDispatchService.ArtifactProjectionLineage", adapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessProjectionSnapshotBuilderAdapter.FromDispatchCandidate(candidate)", shellSource, StringComparison.Ordinal);
        Assert.Contains("ProcessProjectionSnapshotBuilderAdapter.FromExecutionDetail(detail)", shellSource, StringComparison.Ordinal);
        Assert.Contains("ProcessProjectionSnapshotBuilderAdapter.FromExecutionDetailObservations(detail)", shellSource, StringComparison.Ordinal);
        Assert.Contains("ProcessProjectionSnapshotBuilderAdapter.FromDispatchClaim(dispatchClaim)", shellSource, StringComparison.Ordinal);
        Assert.Contains("ProcessProjectionSnapshotBuilderAdapter.FromDispatchLineage(lineage)", shellSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB022_INV_001_splits_projection_run_snapshot_from_execution_detail_observations()
    {
        var projectionModelsSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessProjectionModels.cs");
        var projectionContextSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProjectionContext.cs");
        var facetSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProjectionFacets.cs");
        var facetImplementationSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProjectionFacetImplementations.cs");
        var workspaceProjectionSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessWorkspaceWrittenArtifactProjectionCoordinator.cs");
        var browserProjectionSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessProviderNativeBrowserArtifactProjectionCoordinator.cs");
        var shellSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactProjection.cs");

        var runSnapshotStart = projectionModelsSource.IndexOf("internal sealed record ProcessProjectionRunSnapshot", StringComparison.Ordinal);
        var observationSnapshotStart = projectionModelsSource.IndexOf("internal sealed record ProcessProjectionObservationSnapshot", StringComparison.Ordinal);
        Assert.True(runSnapshotStart >= 0);
        Assert.True(observationSnapshotStart > runSnapshotStart);

        var runSnapshotSource = projectionModelsSource[runSnapshotStart..observationSnapshotStart];
        Assert.DoesNotContain("ProcessAutomationExecutionRunDetail", runSnapshotSource, StringComparison.Ordinal);
        Assert.DoesNotContain(" Detail", runSnapshotSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessProjectionObservationSnapshot", projectionModelsSource, StringComparison.Ordinal);
        Assert.Contains("SuccessfulWorkspaceFileMutationReceiptPaths", projectionModelsSource, StringComparison.Ordinal);
        Assert.Contains("SuccessfulBrowserToolOutputFiles", projectionModelsSource, StringComparison.Ordinal);
        Assert.Contains("ProviderNativeBrowserWorkingDirectory", projectionModelsSource, StringComparison.Ordinal);
        Assert.Contains("ProcessProjectionObservationSnapshot Observations", projectionContextSource, StringComparison.Ordinal);
        Assert.Contains("ProcessProjectionSnapshotBuilderAdapter.FromExecutionDetailObservations(detail)", shellSource, StringComparison.Ordinal);

        foreach (var source in new[] { projectionContextSource, facetSource, facetImplementationSource, workspaceProjectionSource, browserProjectionSource })
        {
            Assert.DoesNotContain("ProcessAutomationExecutionRunDetail", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".Detail", source, StringComparison.Ordinal);
            Assert.DoesNotContain("run.Detail", source, StringComparison.Ordinal);
            Assert.DoesNotContain("context.Run.Detail", source, StringComparison.Ordinal);
        }

        Assert.Contains("context.Observations.SuccessfulWorkspaceFileMutationReceiptPaths", workspaceProjectionSource, StringComparison.Ordinal);
        Assert.Contains("context.Observations.SuccessfulBrowserToolOutputFiles", browserProjectionSource, StringComparison.Ordinal);
        Assert.Contains("context.Observations.ProviderNativeBrowserWorkingDirectory", browserProjectionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessProjectionSessionObservationSource sessionObservationSource", browserProjectionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_projection_direct_file_io_stays_inside_file_io_facet()
    {
        var facetSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProjectionFacetImplementations.cs");
        var fileIoStart = facetSource.IndexOf("internal sealed class ProcessProjectionFileIo", StringComparison.Ordinal);
        var nextFacetStart = facetSource.IndexOf("internal sealed class ProcessProjectionArtifactClassifier", fileIoStart, StringComparison.Ordinal);

        Assert.True(fileIoStart >= 0);
        Assert.True(nextFacetStart > fileIoStart);

        var nonFileIoSource = facetSource.Remove(fileIoStart, nextFacetStart - fileIoStart);
        var directIoTokens = new[]
        {
            "File.Exists(",
            "new FileInfo(",
            "File.ReadAllBytes(",
            "File.ReadAllBytesAsync(",
            "File.WriteAllTextAsync(",
            "File.Copy(",
            "Directory.CreateDirectory("
        };

        foreach (var directIoToken in directIoTokens)
        {
            Assert.DoesNotContain(directIoToken, nonFileIoSource, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Artifact_projection_write_coordinator_is_created_once_by_artifact_projection_flow()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactProjection.cs");
        var methodIndex = source.IndexOf("private async Task ProjectExecutionArtifactsAsync", StringComparison.Ordinal);
        var coordinatorIndex = source.IndexOf("new ProcessArtifactProjectionWriteCoordinator", StringComparison.Ordinal);
        var totalCoordinatorCreations = Regex.Matches(source, "new ProcessArtifactProjectionWriteCoordinator").Count;

        Assert.True(methodIndex >= 0);
        Assert.True(coordinatorIndex > methodIndex);
        Assert.Equal(1, totalCoordinatorCreations);
    }

    [Fact]
    public void Process_mock_projection_SB05_INV_001_uses_write_coordinator_without_direct_storage_record_block()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessMockArtifactProjectionCoordinator.cs");
        var processMockSection = source;

        Assert.Contains("context.WriteCoordinator.WriteAsync", processMockSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteRequest", processMockSection, StringComparison.Ordinal);
        Assert.Contains("candidateState.TryApplyExpectedWriteOutcome", processMockSection, StringComparison.Ordinal);
        Assert.Contains("IProcessProjectionProcessMockRules processMockRules", processMockSection, StringComparison.Ordinal);
        Assert.Contains("IProcessProjectionFileIo fileIo", processMockSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", processMockSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", processMockSection, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService dispatchService", processMockSection, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessArtifactProjectionHost", processMockSection, StringComparison.Ordinal);
        Assert.Contains("throw new InvalidOperationException", processMockSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Workspace_written_projection_SB06_INV_001_uses_write_coordinator_without_direct_storage_record_block()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessWorkspaceWrittenArtifactProjectionCoordinator.cs");
        var workspaceWrittenSection = source;

        Assert.Contains("WorkspaceWrittenArtifactProjectionSourceAdapter.Plan", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.Contains("context.WriteCoordinator.WriteAsync", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteRequest", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.Contains("candidateState.TryApplyExpectedWriteOutcome", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.Contains("IProcessProjectionSessionObservationSource sessionObservationSource", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.Contains("IProcessProjectionProjectStructureMatcher projectStructureMatcher", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService dispatchService", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessArtifactProjectionHost", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.Contains("context.Logger.LogWarning", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.DoesNotContain("throw new InvalidOperationException", workspaceWrittenSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_managed_projection_SB07_INV_001_uses_write_coordinator_without_direct_storage_record_block()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessExistingManagedArtifactProjectionCoordinator.cs");
        var existingManagedSection = source;

        Assert.Contains("ExistingManagedArtifactFileMatches", existingManagedSection, StringComparison.Ordinal);
        Assert.Contains("ExistingManagedArtifactProjectionSourceAdapter.Plan", existingManagedSection, StringComparison.Ordinal);
        Assert.Contains("context.WriteCoordinator.WriteAsync", existingManagedSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteRequest", existingManagedSection, StringComparison.Ordinal);
        Assert.Contains("candidateState.TryApplyExpectedWriteOutcome", existingManagedSection, StringComparison.Ordinal);
        Assert.Contains("IProcessProjectionExpectationMatcher expectationMatcher", existingManagedSection, StringComparison.Ordinal);
        Assert.Contains("IProcessProjectionPathResolver pathResolver", existingManagedSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", existingManagedSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", existingManagedSection, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService dispatchService", existingManagedSection, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessArtifactProjectionHost", existingManagedSection, StringComparison.Ordinal);
        Assert.Contains("context.Logger.LogWarning", existingManagedSection, StringComparison.Ordinal);
        Assert.DoesNotContain("throw new InvalidOperationException", existingManagedSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Response_text_projection_SB09_INV_001_uses_write_coordinator_without_moving_file_creation_or_short_circuit()
    {
        var responseSection = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessResponseTextArtifactProjectionCoordinator.cs");
        var existingManagedHelperSection = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessExistingManagedArtifactProjectionCoordinator.cs");

        Assert.Contains("IsWithinWorkspace", responseSection, StringComparison.Ordinal);
        Assert.Contains("fileIo.WriteAllTextAsync", responseSection, StringComparison.Ordinal);
        Assert.Contains("persistedResponseText", responseSection, StringComparison.Ordinal);
        Assert.Contains("Environment.NewLine", responseSection, StringComparison.Ordinal);
        Assert.Contains("Encoding.UTF8.GetBytes(persistedResponseText)", responseSection, StringComparison.Ordinal);
        Assert.Contains("existingManagedCoordinator.TryRecordForResponseProjectionAsync", responseSection, StringComparison.Ordinal);
        Assert.Contains("context.WriteCoordinator.WriteAsync", responseSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteRequest", responseSection, StringComparison.Ordinal);
        Assert.Contains("candidateState.TryApplyExpectedWriteOutcome", responseSection, StringComparison.Ordinal);
        Assert.Contains("IProcessProjectionResponseTextRules responseTextRules", responseSection, StringComparison.Ordinal);
        Assert.Contains("IProcessProjectionFileIo fileIo", responseSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", responseSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", responseSection, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService dispatchService", responseSection, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessArtifactProjectionHost", responseSection, StringComparison.Ordinal);

        Assert.Contains("TryRecordForResponseProjectionAsync", existingManagedHelperSection, StringComparison.Ordinal);
        Assert.Contains("ExistingManagedArtifactFileMatches", existingManagedHelperSection, StringComparison.Ordinal);
        Assert.Contains("context.WriteCoordinator.WriteAsync", existingManagedHelperSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteRequest", existingManagedHelperSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", existingManagedHelperSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", existingManagedHelperSection, StringComparison.Ordinal);
        Assert.Contains("context.Logger.LogWarning", existingManagedHelperSection, StringComparison.Ordinal);
        Assert.Contains("return false", existingManagedHelperSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_native_browser_projection_SB10_INV_001_uses_write_coordinator_for_expected_and_discovered_modes()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessProviderNativeBrowserArtifactProjectionCoordinator.cs");
        var coordinatorStart = source.IndexOf("internal sealed class ProcessProviderNativeBrowserArtifactProjectionCoordinator", StringComparison.Ordinal);
        var expectedMethodStart = source.IndexOf("private async Task ProjectExpectedOutputsAsync", coordinatorStart, StringComparison.Ordinal);
        var discoveredMethodStart = source.IndexOf("private async Task ProjectDiscoveredOutputsAsync", expectedMethodStart, StringComparison.Ordinal);

        Assert.True(coordinatorStart >= 0);
        Assert.True(expectedMethodStart >= 0);
        Assert.True(discoveredMethodStart > expectedMethodStart);

        var expectedSection = source[expectedMethodStart..discoveredMethodStart];
        var discoveredSection = source[discoveredMethodStart..];

        Assert.Contains("ProjectDiscoveredOutputsAsync", source[coordinatorStart..expectedMethodStart], StringComparison.Ordinal);
        Assert.Contains("ResolveProviderNativeBrowserToolName", expectedSection, StringComparison.Ordinal);
        Assert.Contains("PlanExpectedOutput", expectedSection, StringComparison.Ordinal);
        Assert.Contains("IsWithinWorkspace", expectedSection, StringComparison.Ordinal);
        Assert.Contains("fileIo.CopyFile", expectedSection, StringComparison.Ordinal);
        Assert.Contains("context.WriteCoordinator.WriteAsync", expectedSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteRequest", expectedSection, StringComparison.Ordinal);
        Assert.Contains("IProcessProjectionBrowserOutputRules browserOutputRules", source, StringComparison.Ordinal);
        Assert.Contains("context.Observations.SuccessfulBrowserToolOutputFiles", source, StringComparison.Ordinal);
        Assert.Contains("context.Observations.ProviderNativeBrowserWorkingDirectory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessProjectionSessionObservationSource sessionObservationSource", source, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", expectedSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", expectedSection, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService dispatchService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessArtifactProjectionHost", source, StringComparison.Ordinal);

        Assert.Contains("IsProviderNativeBrowserArtifactPath", discoveredSection, StringComparison.Ordinal);
        Assert.Contains("ResolveArtifactExpectation", discoveredSection, StringComparison.Ordinal);
        Assert.Contains("recordExpectation", discoveredSection, StringComparison.Ordinal);
        Assert.Contains("PlanDiscoveredOutput", discoveredSection, StringComparison.Ordinal);
        Assert.Contains("fileIo.CopyFile", discoveredSection, StringComparison.Ordinal);
        Assert.Contains("context.WriteCoordinator.WriteAsync", discoveredSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteRequest", discoveredSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", discoveredSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", discoveredSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Completed_decision_projection_SB11_INV_001_uses_record_only_coordinator_without_storage_placement()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessCompletedDecisionArtifactCoordinator.cs");
        var coordinatorSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProjectionWriteCoordinator.cs");
        var methodStart = source.IndexOf("internal sealed class ProcessCompletedDecisionArtifactCoordinator", StringComparison.Ordinal);
        var recordOnlyCoordinatorStart = coordinatorSource.IndexOf("internal sealed class ProcessArtifactProjectionRecordOnlyCoordinator", StringComparison.Ordinal);

        Assert.True(methodStart >= 0);
        Assert.True(recordOnlyCoordinatorStart >= 0);

        var decisionSection = source[methodStart..];
        var recordOnlyCoordinatorSection = coordinatorSource[recordOnlyCoordinatorStart..];

        Assert.Contains("ProcessArtifactProjectionRecordOnlyRequest", decisionSection, StringComparison.Ordinal);
        Assert.Contains("context.RecordOnlyCoordinator.RecordAsync", decisionSection, StringComparison.Ordinal);
        Assert.Contains("BuildCompletedDecisionArtifactExternalReferenceKey", decisionSection, StringComparison.Ordinal);
        Assert.Contains("ResolveCompletedDecisionArtifactTrustStatus", decisionSection, StringComparison.Ordinal);
        Assert.Contains("candidateState.TryApplyExpectedRecordOnlyOutcome", decisionSection, StringComparison.Ordinal);
        Assert.Contains("IProcessProjectionDecisionArtifactRules decisionArtifactRules", decisionSection, StringComparison.Ordinal);
        Assert.Contains("IProcessProjectionLineageFactory lineageFactory", decisionSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", decisionSection, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessArtifactProjectionWriteCoordinator", decisionSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", decisionSection, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService dispatchService", decisionSection, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessArtifactProjectionHost", decisionSection, StringComparison.Ordinal);

        Assert.Contains("ProcessArtifactProjectionRecordOnlyRequest", recordOnlyCoordinatorSection, StringComparison.Ordinal);
        Assert.Contains("recordArtifactAsync", recordOnlyCoordinatorSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", recordOnlyCoordinatorSection, StringComparison.Ordinal);
        Assert.DoesNotContain("StoragePlacementRequest", recordOnlyCoordinatorSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_dispatch_candidate_hydration_gate_b_SB08_INV_001_uses_selector_and_snapshot_loader_without_side_effect_drift()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var dispatchSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Dispatch.cs"));
        var hydrationServiceSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateHydrationService.cs"));
        var hydrationAssemblerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchHydratedCandidateAssembler.cs"));
        var selectorSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateHeaderSelector.cs"));
        var loaderSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateHydrationLoader.cs"));

        Assert.Contains("internal static class ProcessDispatchCandidateHeaderSelector", selectorSource, StringComparison.Ordinal);
        Assert.Contains("SelectAsync", selectorSource, StringComparison.Ordinal);
        Assert.Contains("AutomationDispatchLeaseExpiresAtUtc", selectorSource, StringComparison.Ordinal);
        Assert.Contains("OrderBy(item => item.Sequence)", selectorSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteEligibility.IsRunEligibleForDispatchCandidate", selectorSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteEligibility.IsStepStatusDispatchableForRun", selectorSource, StringComparison.Ordinal);

        Assert.Contains("internal sealed record ProcessDispatchCandidateHydrationSnapshot", loaderSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessDispatchCandidateHydrationLoader", loaderSource, StringComparison.Ordinal);
        Assert.Contains("LoadAsync", loaderSource, StringComparison.Ordinal);
        Assert.Contains("WorkBriefsByStepRunId", loaderSource, StringComparison.Ordinal);
        Assert.Contains("StepRoleRequirementsByStepDefinitionId", loaderSource, StringComparison.Ordinal);
        Assert.Contains("ArtifactInputsByStepDefinitionId", loaderSource, StringComparison.Ordinal);
        Assert.Contains("ConditionalDependencyOutcomeIdsByStepDefinitionId", loaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChangesAsync", loaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveAgentAsync", loaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteRunAsync", loaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("workflowRunCoordinator", loaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", selectorSource + loaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", selectorSource + loaderSource, StringComparison.Ordinal);

        Assert.Contains("ProcessDispatchCandidateHeaderSelector.SelectAsync", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchCandidateHydrationLoader.LoadAsync", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCandidateHydrationLoader.LoadAsync", hydrationServiceSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchHydratedCandidateAssembler", hydrationServiceSource, StringComparison.Ordinal);
        Assert.Contains("snapshot.DispatchableSteps", hydrationAssemblerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB010_INV_001_splits_hydration_query_artifact_preparation_and_assembly()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var hydrationServiceSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateHydrationService.cs"));
        var loaderSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateHydrationLoader.cs"));
        var artifactPreparationSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateArtifactInputPreparationService.cs"));
        var hydratedCandidateAssemblerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchHydratedCandidateAssembler.cs"));
        var directAgentCandidateAssemblerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchDirectAgentCandidateAssembler.cs"));

        Assert.Contains("ProcessDispatchCandidateHydrationLoader.LoadAsync", hydrationServiceSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessDispatchCandidateArtifactInputPreparationService(", hydrationServiceSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessDispatchHydratedCandidateAssembler(", hydrationServiceSource, StringComparison.Ordinal);
        Assert.Contains("candidateAssembler.TryAssembleAsync(", hydrationServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("snapshot.DispatchableSteps", hydrationServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchBranchDependencyContext.Create", hydrationServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync", hydrationServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchArtifactInputAssembler.BuildResolvedArtifactInputs", hydrationServiceSource, StringComparison.Ordinal);

        Assert.Contains("internal static class ProcessDispatchCandidateHydrationLoader", loaderSource, StringComparison.Ordinal);
        Assert.Contains("AppDbContext dbContext", loaderSource, StringComparison.Ordinal);
        Assert.Contains("AsNoTracking", loaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChangesAsync", loaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveAgentAsync", loaderSource, StringComparison.Ordinal);

        Assert.Contains("internal sealed class ProcessDispatchCandidateArtifactInputPreparationService", artifactPreparationSource, StringComparison.Ordinal);
        Assert.Contains("IWorkspacePathResolver workspacePathResolver", artifactPreparationSource, StringComparison.Ordinal);
        Assert.Contains("IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor", artifactPreparationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchArtifactInputAssembler.BuildResolvedArtifactInputs", artifactPreparationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchManagedArtifactPromptPathPreparer.PrepareArtifactInputsForPrompt", artifactPreparationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AppDbContext", artifactPreparationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveAgentAsync", artifactPreparationSource, StringComparison.Ordinal);

        Assert.Contains("internal sealed class ProcessDispatchHydratedCandidateAssembler", hydratedCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("snapshot.DispatchableSteps", hydratedCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchBranchDependencyContext.Create", hydratedCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchExpectedArtifactLoader.LoadAsync", hydratedCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("artifactInputPreparationService.Prepare", hydratedCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCandidateFactory.CreateSubprocessCandidate", hydratedCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCandidateFactory.CreateWorkflowCandidate", hydratedCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("directAgentCandidateAssembler.TryCreateAsync", hydratedCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchCandidateFactory.CreateDirectAgentCandidate", hydratedCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync", hydratedCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRecoveryQueryHelper.ResolveRecoverableExecutionRunId", hydratedCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateDbContextAsync", hydratedCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters", hydratedCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCandidateFactory.CreateDirectAgentCandidate", directAgentCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync", directAgentCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRecoveryQueryHelper.ResolveRecoverableExecutionRunId", directAgentCandidateAssemblerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", hydratedCandidateAssemblerSource + artifactPreparationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", hydratedCandidateAssemblerSource + artifactPreparationSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_dispatch_candidate_hydration_gate_c_SB12_INV_001_uses_assembly_helpers_without_core_or_driver_drift()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var dispatchSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Dispatch.cs"));
        var hydrationServiceSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateHydrationService.cs"));
        var hydrationAssemblerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchHydratedCandidateAssembler.cs"));
        var artifactPreparationSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateArtifactInputPreparationService.cs"));
        var artifactSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchArtifactInputAssembler.cs"));
        var branchSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchBranchDependencyContext.cs"));
        var assignmentSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchAssignmentRouteHelper.cs"));
        var serviceArtifactSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.ArtifactValidation.cs"));
        var serviceCooperationSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Cooperation.cs"));
        var combinedSource = string.Join(Environment.NewLine, artifactSource, branchSource, assignmentSource);

        Assert.Contains("internal static class ProcessDispatchArtifactInputAssembler", artifactSource, StringComparison.Ordinal);
        Assert.Contains("BuildResolvedArtifactInputs", artifactSource, StringComparison.Ordinal);
        Assert.Contains("PrepareArtifactInputsForPrompt", artifactSource, StringComparison.Ordinal);
        Assert.Contains("IsCurrentRunUpstreamArtifactInput", artifactSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchArtifactInputAssembler.BuildResolvedArtifactInputs", serviceArtifactSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchArtifactInputAssembler.PrepareArtifactInputsForPrompt", serviceArtifactSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchArtifactInputAssembler.BuildResolvedArtifactInputs", artifactPreparationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchManagedArtifactPromptPathPreparer.PrepareArtifactInputsForPrompt", artifactPreparationSource, StringComparison.Ordinal);

        Assert.Contains("internal sealed record ProcessDispatchBranchDependencyContext", branchSource, StringComparison.Ordinal);
        Assert.Contains("RequiresExplicitBranchOutcomeSelection", branchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchBranchDependencyContext.Create", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchBranchDependencyContext.Create", hydrationServiceSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchBranchDependencyContext.Create", hydrationAssemblerSource, StringComparison.Ordinal);

        Assert.Contains("internal static class ProcessDispatchAssignmentRouteHelper", assignmentSource, StringComparison.Ordinal);
        Assert.Contains("ResolveCurrentAssignment", assignmentSource, StringComparison.Ordinal);
        Assert.Contains("IsWorkflowDispatchAssignment", assignmentSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchAssignmentRouteHelper.ResolveCurrentAssignment", serviceCooperationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchAssignmentRouteHelper.IsWorkflowDispatchAssignment", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchAssignmentRouteHelper.IsWorkflowDispatchAssignment", hydrationServiceSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchAssignmentRouteHelper.IsWorkflowDispatchAssignment", hydrationAssemblerSource, StringComparison.Ordinal);

        Assert.DoesNotContain("CanDoItAll.Processes.Core", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChangesAsync", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveAgentAsync", combinedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_dispatch_candidate_hydration_gate_d_SB16_INV_001_keeps_binding_side_effects_explicit_and_recovery_queries_local()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var dispatchSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Dispatch.cs"));
        var hydrationServiceSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateHydrationService.cs"));
        var hydrationAssemblerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchHydratedCandidateAssembler.cs"));
        var directAgentAssemblerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchDirectAgentCandidateAssembler.cs"));
        var bindingSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchTechnicalAgentBindingCoordinator.cs"));
        var recoverySource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRecoveryQueryHelper.cs"));
        var loaderSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateHydrationLoader.cs"));

        Assert.Contains("internal static class ProcessDispatchTechnicalAgentBindingCoordinator", bindingSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchTechnicalAgentBindingOutcome", bindingSource, StringComparison.Ordinal);
        Assert.Contains("ProjectStructureAccessGrantedAndSaved", bindingSource, StringComparison.Ordinal);
        Assert.Contains("technicalAgentBridge.GetDirectorySummariesAsync", bindingSource, StringComparison.Ordinal);
        Assert.Contains("executionClient.GetAgentEditorAsync", bindingSource, StringComparison.Ordinal);
        Assert.Contains("executionClient.SaveAgentAsync", bindingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync", hydrationServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync", hydrationAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("BuildMissingTechnicalAgentBindingDiagnostic", directAgentAssemblerSource, StringComparison.Ordinal);

        Assert.Contains("internal static class ProcessDispatchRecoveryQueryHelper", recoverySource, StringComparison.Ordinal);
        Assert.Contains("ResolveRecoverableExecutionRunId", recoverySource, StringComparison.Ordinal);
        Assert.Contains("LoadLatestManualRecoveryDirectiveAsync", recoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRecoveryQueryHelper.ResolveRecoverableExecutionRunId", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRecoveryQueryHelper.ResolveRecoverableExecutionRunId", hydrationServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRecoveryQueryHelper.ResolveRecoverableExecutionRunId", hydrationAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRecoveryQueryHelper.ResolveRecoverableExecutionRunId", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRecoveryQueryHelper.LoadLatestManualRecoveryDirectiveAsync", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRecoveryQueryHelper.LoadLatestManualRecoveryDirectiveAsync", hydrationServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRecoveryQueryHelper.LoadLatestManualRecoveryDirectiveAsync", hydrationAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRecoveryQueryHelper.LoadLatestManualRecoveryDirectiveAsync", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChangesAsync", recoverySource, StringComparison.Ordinal);

        Assert.DoesNotContain("SaveAgentAsync", loaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetDirectorySummariesAsync", loaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", bindingSource + recoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", bindingSource + recoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", bindingSource + recoverySource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB011_INV_001_moves_direct_agent_binding_recovery_and_cooperation_to_explicit_assembler()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var hydrationServiceSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateHydrationService.cs"));
        var hydratedAssemblerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchHydratedCandidateAssembler.cs"));
        var directAgentAssemblerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchDirectAgentCandidateAssembler.cs"));
        var bindingSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchTechnicalAgentBindingCoordinator.cs"));
        var recoverySource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRecoveryQueryHelper.cs"));
        var cooperationSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCooperationMetadataResolver.cs"));

        Assert.Contains("new ProcessDispatchDirectAgentCandidateAssembler(", hydrationServiceSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchDirectAgentCandidateAssembler directAgentCandidateAssembler", hydratedAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("directAgentCandidateAssembler.TryCreateAsync(", hydratedAssemblerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync", hydrationServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync", hydratedAssemblerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRecoveryQueryHelper.ResolveRecoverableExecutionRunId", hydratedAssemblerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchCooperationMetadataResolver.ResolveProcessCooperationMetadata", hydratedAssemblerSource, StringComparison.Ordinal);

        Assert.Contains("internal sealed class ProcessDispatchDirectAgentCandidateAssembler", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessAutomationExecutionRunSelection.HasBlockingAutomationExecutionRun", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRecoveryQueryHelper.ResolveRecoverableExecutionRunId", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRecoveryQueryHelper.LoadLatestManualRecoveryDirectiveAsync", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchTechnicalAgentBindingCoordinator.TryResolveProjectStructureAccessProjectId", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCooperationMetadataResolver.ResolveProcessCooperationMetadata", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCandidateAssemblyContextFactory.WithDirectAgentFacts", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCandidateFactory.CreateDirectAgentCandidate", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessDispatchTechnicalAgentBindingCoordinator", bindingSource, StringComparison.Ordinal);
        Assert.Contains("executionClient.SaveAgentAsync", bindingSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessDispatchRecoveryQueryHelper", recoverySource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessDispatchCooperationMetadataResolver", cooperationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", directAgentAssemblerSource + bindingSource + recoverySource + cooperationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", directAgentAssemblerSource + bindingSource + recoverySource + cooperationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", directAgentAssemblerSource + bindingSource + recoverySource + cooperationSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB012_INV_001_preserves_hydration_parity_and_side_effect_ownership()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var hydrationServiceSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateHydrationService.cs"));
        var loaderSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateHydrationLoader.cs"));
        var artifactPreparationSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateArtifactInputPreparationService.cs"));
        var hydratedAssemblerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchHydratedCandidateAssembler.cs"));
        var directAgentAssemblerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchDirectAgentCandidateAssembler.cs"));
        var bindingSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchTechnicalAgentBindingCoordinator.cs"));
        var recoverySource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRecoveryQueryHelper.cs"));
        var candidateFactorySource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateFactory.cs"));

        Assert.Contains("ProcessDispatchCandidateHydrationLoader.LoadAsync", hydrationServiceSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessDispatchCandidateArtifactInputPreparationService(", hydrationServiceSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessDispatchDirectAgentCandidateAssembler(", hydrationServiceSource, StringComparison.Ordinal);
        Assert.Contains("new ProcessDispatchHydratedCandidateAssembler(", hydrationServiceSource, StringComparison.Ordinal);
        Assert.Contains("candidateAssembler.TryAssembleAsync(", hydrationServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("snapshot.DispatchableSteps", hydrationServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchExpectedArtifactLoader.LoadAsync", hydrationServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync", hydrationServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRecoveryQueryHelper.ResolveRecoverableExecutionRunId", hydrationServiceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchCooperationMetadataResolver.ResolveProcessCooperationMetadata", hydrationServiceSource, StringComparison.Ordinal);

        Assert.Contains("internal static class ProcessDispatchCandidateHydrationLoader", loaderSource, StringComparison.Ordinal);
        Assert.Contains("AsNoTracking", loaderSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteEligibility.IsRunEligibleForDispatchCandidate", loaderSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteEligibility.IsStepStatusDispatchableForRun", loaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChangesAsync", loaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveAgentAsync", loaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ListExecutionRunsAsync", loaderSource, StringComparison.Ordinal);

        Assert.Contains("IWorkspacePathResolver workspacePathResolver", artifactPreparationSource, StringComparison.Ordinal);
        Assert.Contains("IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor", artifactPreparationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchArtifactInputAssembler.BuildResolvedArtifactInputs", artifactPreparationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchManagedArtifactPromptPathPreparer.PrepareArtifactInputsForPrompt", artifactPreparationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AppDbContext", artifactPreparationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveAgentAsync", artifactPreparationSource, StringComparison.Ordinal);

        Assert.Contains("snapshot.DispatchableSteps", hydratedAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchBranchDependencyContext.Create", hydratedAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchExpectedArtifactLoader.LoadAsync", hydratedAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCandidateAssemblyContextFactory.Create", hydratedAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCandidateFactory.CreateSubprocessCandidate", hydratedAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCandidateFactory.CreateWorkflowCandidate", hydratedAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("directAgentCandidateAssembler.TryCreateAsync(", hydratedAssemblerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync", hydratedAssemblerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRecoveryQueryHelper.LoadLatestManualRecoveryDirectiveAsync", hydratedAssemblerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchCooperationMetadataResolver.ResolveProcessCooperationMetadata", hydratedAssemblerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveAgentAsync", hydratedAssemblerSource, StringComparison.Ordinal);

        Assert.Contains("executionClient.ListExecutionRunsAsync", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessAutomationExecutionRunSelection.HasBlockingAutomationExecutionRun", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRecoveryQueryHelper.ResolveRecoverableExecutionRunId", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRecoveryQueryHelper.LoadLatestManualRecoveryDirectiveAsync", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("BuildMissingTechnicalAgentBindingDiagnostic", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCandidateAssemblyContextFactory.WithDirectAgentFacts", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCandidateFactory.CreateDirectAgentCandidate", directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("executionClient.SaveAgentAsync", bindingSource, StringComparison.Ordinal);
        Assert.Contains("LoadLatestManualRecoveryDirectiveAsync", recoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChangesAsync", recoverySource, StringComparison.Ordinal);

        Assert.Contains("CreateSubprocessCandidate", candidateFactorySource, StringComparison.Ordinal);
        Assert.Contains("CreateWorkflowCandidate", candidateFactorySource, StringComparison.Ordinal);
        Assert.Contains("CreateDirectAgentCandidate", candidateFactorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", ReadDispatchSourceExcludingCorePureRuleAdapters(dispatchDirectory), StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", hydrationServiceSource + loaderSource + artifactPreparationSource + hydratedAssemblerSource + directAgentAssemblerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", hydrationServiceSource + loaderSource + artifactPreparationSource + hydratedAssemblerSource + directAgentAssemblerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_dispatch_candidate_factory_gate_a_SB04_INV_001_uses_module_local_side_effect_free_factory()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var contextPath = Path.Combine(dispatchDirectory, "ProcessDispatchCandidateAssemblyContext.cs");
        var factoryPath = Path.Combine(dispatchDirectory, "ProcessDispatchCandidateFactory.cs");

        Assert.True(File.Exists(contextPath), contextPath);
        Assert.True(File.Exists(factoryPath), factoryPath);

        var dispatchSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Dispatch.cs"));
        var hydrationServiceSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateHydrationService.cs"));
        var hydrationAssemblerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchHydratedCandidateAssembler.cs"));
        var directAgentAssemblerSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchDirectAgentCandidateAssembler.cs"));
        var contextSource = File.ReadAllText(contextPath);
        var factorySource = File.ReadAllText(factoryPath);
        var helperSource = contextSource + Environment.NewLine + factorySource;
        var forbiddenTokens = new[]
        {
            "CanDoItAll.Processes.Core",
            "IProcessDriverPack",
            "DriverPack",
            "ProcessDriver",
            "DbContext",
            "CreateDbContextAsync",
            "SaveChangesAsync",
            "SaveAgentAsync",
            "executionClient",
            "technicalAgentBridge",
            "workflowRunCoordinator",
            "TransitionStepWithClaimAsync",
            "HandleSubprocessDispatchAsync",
            "TryRunOrObserveAsync"
        };

        Assert.Contains("internal sealed record ProcessDispatchCandidateAssemblyContext", contextSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessDispatchCandidateAssemblyContextFactory", contextSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessDispatchCandidateFactory", factorySource, StringComparison.Ordinal);
        Assert.Contains("CreateSubprocessCandidate", factorySource, StringComparison.Ordinal);
        Assert.Contains("CreateWorkflowCandidate", factorySource, StringComparison.Ordinal);
        Assert.Contains("CreateDirectAgentCandidate", factorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchCandidateAssemblyContextFactory.Create", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchCandidateAssemblyContextFactory.Create", hydrationServiceSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCandidateAssemblyContextFactory.Create", hydrationAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCandidateFactory.CreateSubprocessCandidate", hydrationAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCandidateFactory.CreateWorkflowCandidate", hydrationAssemblerSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCandidateFactory.CreateDirectAgentCandidate", directAgentAssemblerSource, StringComparison.Ordinal);

        foreach (var forbiddenToken in forbiddenTokens)
        {
            Assert.DoesNotContain(forbiddenToken, helperSource, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Process_dispatch_candidate_factory_gate_b_SB08_INV_001_owns_all_dispatch_candidate_construction()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var dispatchSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Dispatch.cs"));
        var factorySource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateFactory.cs"));
        var factoryConstructorCount = Regex.Matches(
            factorySource,
            @"new\s+DispatchCandidate\s*\(",
            RegexOptions.CultureInvariant).Count;

        Assert.DoesNotContain("new DispatchCandidate", dispatchSource, StringComparison.Ordinal);
        Assert.Equal(1, factoryConstructorCount);
        Assert.Contains("CreateSubprocessCandidate", factorySource, StringComparison.Ordinal);
        Assert.Contains("CreateWorkflowCandidate", factorySource, StringComparison.Ordinal);
        Assert.Contains("CreateDirectAgentCandidate", factorySource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_dispatch_cooperation_resolver_SB13_INV_001_moves_profile_resolution_without_driver_api()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var resolverPath = Path.Combine(dispatchDirectory, "ProcessDispatchCooperationMetadataResolver.cs");

        Assert.True(File.Exists(resolverPath), resolverPath);

        var resolverSource = File.ReadAllText(resolverPath);
        var serviceCooperationSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Cooperation.cs"));

        Assert.Contains("internal static class ProcessDispatchCooperationMetadataResolver", resolverSource, StringComparison.Ordinal);
        Assert.Contains("ResolveProcessCooperationMetadata", resolverSource, StringComparison.Ordinal);
        Assert.Contains("ResolveWorkspaceToolProfile", resolverSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCooperationMetadataResolver.ResolveProcessCooperationMetadata", serviceCooperationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchCooperationMetadataResolver.ResolveWorkspaceToolProfile", serviceCooperationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", resolverSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", resolverSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", resolverSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", resolverSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_dispatch_execution_retry_provider_gate_a_SB04_INV_001_keeps_refactor_module_local_without_driver_or_ui_proof()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var bundleRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-dispatch-execution-retry-provider-boundary-v1");
        var productionProjectNames = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(static name => !string.IsNullOrEmpty(name))
            .Select(static name => name!)
            .ToArray();
        var dispatchSource = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(dispatchDirectory, "*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var proofPaths = Directory.Exists(Path.Combine(bundleRoot, "proof"))
            ? Directory.EnumerateFiles(Path.Combine(bundleRoot, "proof"), "*", SearchOption.AllDirectories)
            : [];
        var forbiddenProofPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet"
        };

        Assert.DoesNotContain(productionProjectNames, name =>
            string.Equals(name, "CanDoItAll.Modules.Processes.Core", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("ProcessDriver", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("DriverPack", StringComparison.OrdinalIgnoreCase));
        AssertNarrowProcessCoreSeed(root);
        Assert.False(Directory.Exists(Path.Combine(root, "src", "CanDoItAll.Modules.Processes.Core")));
        Assert.DoesNotContain("IProcessDriverPack", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverRegistry", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessHelperDriver", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverRegistry", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", ReadDispatchSourceExcludingCorePureRuleAdapters(dispatchDirectory), StringComparison.Ordinal);
        Assert.All(proofPaths, path =>
        {
            var relativePath = Path.GetRelativePath(bundleRoot, path);
            Assert.DoesNotContain(forbiddenProofPathTokens, token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Process_core_contract_candidate_gate_a_SB003_INV_001_keeps_bundle_rows_and_production_guardrails()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var bundleRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-core-contract-candidate-driver-readiness-prep-v1");
        var productionProjectNames = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .ToArray();
        var dispatchSource = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(dispatchDirectory, "*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var executionReport = File.ReadAllText(Path.Combine(bundleRoot, "reviews", "01-execution-report.md"));
        var gateSection = ExtractMarkdownSection(executionReport, "## Subbundle Gate Results");
        var proofPaths = Directory.Exists(Path.Combine(bundleRoot, "proof"))
            ? Directory.EnumerateFiles(Path.Combine(bundleRoot, "proof"), "*", SearchOption.AllDirectories)
            : [];
        var forbiddenProofPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet"
        };

        Assert.DoesNotContain(productionProjectNames, name =>
            string.Equals(name, "CanDoItAll.Modules.Processes.Core", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("ProcessDriver", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("DriverPack", StringComparison.OrdinalIgnoreCase));
        AssertNarrowProcessCoreSeed(root);
        Assert.False(Directory.Exists(Path.Combine(root, "src", "CanDoItAll.Modules.Processes.Core")));
        Assert.DoesNotContain("IProcessDriverPack", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverRegistry", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessHelperDriver", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverRegistry", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", ReadDispatchSourceExcludingCorePureRuleAdapters(dispatchDirectory), StringComparison.Ordinal);
        Assert.DoesNotContain("| SB001-SB033 |", gateSection, StringComparison.Ordinal);

        foreach (var subbundleId in Enumerable.Range(1, 33).Select(static index => $"SB{index:000}"))
        {
            Assert.Single(Regex.Matches(
                gateSection,
                $@"^\| {Regex.Escape(subbundleId)} \|",
                RegexOptions.Multiline));
        }

        Assert.All(proofPaths, path =>
        {
            var relativePath = Path.GetRelativePath(bundleRoot, path);
            Assert.DoesNotContain(forbiddenProofPathTokens, token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
            });
    }

    [Fact]
    public void Process_core_pre_extraction_consolidation_SB002_INV_001_guards_core_driver_ui_drift_and_collapsed_rows()
    {
        var root = FindRepositoryRoot();
        var srcRoot = Path.Combine(root, "src");
        var dispatchDirectory = Path.Combine(
            srcRoot,
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var bundleRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-core-pre-extraction-consolidation-driver-readiness-v1");
        var productionProjectNames = Directory
            .EnumerateFiles(srcRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .ToArray();
        var dispatchSource = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(dispatchDirectory, "*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var executionReport = File.ReadAllText(Path.Combine(bundleRoot, "reviews", "01-execution-report.md"));
        var gateSection = ExtractMarkdownSection(executionReport, "## Subbundle Gate Results");
        var changedFilesOutsideBundle = ReadGitChangedFilesForCurrentProcessCoreStabilizationBundle(root);
        var forbiddenProductionTokens = new[]
        {
            "IProcessDriverPack",
            "IProcessDriverRegistry",
            "IProcessHelperDriver",
            "ProcessDriverRegistry",
            "CanDoItAll.Modules.Processes.Core"
        };

        Assert.DoesNotContain(productionProjectNames, name =>
            string.Equals(name, "CanDoItAll.Modules.Processes.Core", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("ProcessDriver", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("DriverPack", StringComparison.OrdinalIgnoreCase));
        AssertNarrowProcessCoreSeed(root);
        Assert.False(Directory.Exists(Path.Combine(srcRoot, "CanDoItAll.Modules.Processes.Core")));
        Assert.All(forbiddenProductionTokens, token =>
            Assert.DoesNotContain(token, dispatchSource, StringComparison.Ordinal));
        Assert.DoesNotContain("CanDoItAll.Processes.Core", ReadDispatchSourceExcludingCorePureRuleAdapters(dispatchDirectory), StringComparison.Ordinal);
        Assert.DoesNotContain(changedFilesOutsideBundle, IsUiOrMobileProofPath);
        Assert.DoesNotContain("| SB001-SB036 |", gateSection, StringComparison.Ordinal);

        foreach (var subbundleId in Enumerable.Range(1, 36).Select(static index => $"SB{index:000}"))
        {
            Assert.Single(Regex.Matches(
                gateSection,
                $@"^\| {Regex.Escape(subbundleId)} \|",
                RegexOptions.Multiline));
        }
    }

    [Fact]
    public void Process_core_pre_extraction_consolidation_SB005_INV_001_confines_route_adapters_to_application_edges()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var routeModelsSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteModels.cs"));
        var routeAdaptersSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteModelAdapters.cs"));
        var routeHandlersSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteHandlers.cs"));
        var routeServicesSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteServices.cs"));
        var routeFacingSource = string.Join(Environment.NewLine, routeHandlersSource, routeServicesSource);
        var allowedApplicationEdgeSource = string.Join(
            Environment.NewLine,
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteModelAdapters.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.RouteExecution.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCandidateHydrationService.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRecoveryRuntimeService.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchDirectAgentExecutionAdapter.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchFinalizerAdapter.cs")),
            File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchCompetingExecutionGuardService.cs")));

        Assert.DoesNotContain("IProcessRouteCandidateSource", routeModelsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessRouteDispatchClaimSource", routeModelsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessRouteExecutionOutcomeSource", routeModelsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters", routeFacingSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".Source", routeFacingSource, StringComparison.Ordinal);
        Assert.Contains("ConditionalWeakTable<ProcessRouteCandidate, DispatcherCandidateSource>", routeAdaptersSource, StringComparison.Ordinal);
        Assert.Contains("ConditionalWeakTable<ProcessRouteDispatchClaim, DispatcherDispatchClaimSource>", routeAdaptersSource, StringComparison.Ordinal);
        Assert.Contains("ConditionalWeakTable<ProcessRouteExecutionOutcome, DispatcherExecutionOutcomeSource>", routeAdaptersSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.FromDispatcherClaim", allowedApplicationEdgeSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.FromDispatcherCandidate", allowedApplicationEdgeSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherCandidate", allowedApplicationEdgeSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome", allowedApplicationEdgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", ReadDispatchSourceExcludingCorePureRuleAdapters(dispatchDirectory), StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", routeFacingSource + allowedApplicationEdgeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB027_INV_001_preserves_pure_rule_parity_and_core_candidate_boundaries()
    {
        var root = FindRepositoryRoot();
        var dispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var bundleRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-core-contract-candidate-driver-readiness-prep-v1");
        var dispatchSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.Dispatch.cs"));
        var routeSnapshotSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Processes.Core",
            "Routing",
            "ProcessDispatchRouteSnapshot.cs"));
        var subprocessResolverSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessSubprocessArtifactSourceResolver.cs"));
        var projectionUtilitiesSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessRunAutomationDispatchService.ArtifactProjectionUtilities.cs"));
        var routeServicesSource = File.ReadAllText(Path.Combine(dispatchDirectory, "ProcessDispatchRouteServices.cs"));
        var integrationTestSource = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "CanDoItAll.Tests.Integration",
            "ProcessRunAutomationDispatchServiceTests.cs"));
        var coreDecisionMatrix = File.ReadAllText(Path.Combine(
            bundleRoot,
            "architecture",
            "04-core-readiness-decision-matrix-template.md"));
        var productionProjectNames = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .ToArray();
        var allowedCoreAdapterFiles = CreateAllowedDispatchProcessCoreConsumerFiles();
        var dispatchModuleSource = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(dispatchDirectory, "*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var sideEffectDispatchModuleSource = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(dispatchDirectory, "*.cs")
                .Where(path => !allowedCoreAdapterFiles.Contains(Path.GetFileName(path)))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("internal static bool IsRunClosedToAutomation", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("internal static bool IsRunEligibleForDispatchCandidate", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("internal static bool IsStepStatusDispatchableForRun", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("internal static ProcessArtifactRecord? ResolveSubprocessSourceArtifact", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("internal static IReadOnlyList<ProcessSubprocessOutputArtifactMapping> ResolveSubprocessOutputArtifactMappings", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.IsRunClosedToAutomation", integrationTestSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRunAutomationDispatchService.ResolveSubprocessSourceArtifact", integrationTestSource, StringComparison.Ordinal);

        Assert.Contains("public static class ProcessDispatchRouteEligibility", routeSnapshotSource, StringComparison.Ordinal);
        Assert.Contains("public static bool IsRunClosedToAutomation", routeSnapshotSource, StringComparison.Ordinal);
        Assert.Contains("public static bool IsRunEligibleForDispatchCandidate", routeSnapshotSource, StringComparison.Ordinal);
        Assert.Contains("public static bool IsStepStatusDispatchableForRun", routeSnapshotSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ProcessSubprocessArtifactSourceResolver", subprocessResolverSource, StringComparison.Ordinal);
        Assert.Contains("public static ProcessArtifactRecord? ResolveSourceArtifact", subprocessResolverSource, StringComparison.Ordinal);
        Assert.Contains("public static IReadOnlyList<ProcessSubprocessOutputArtifactMapping> ResolveOutputArtifactMappings", subprocessResolverSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteEligibility.IsRunClosedToAutomation", integrationTestSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteEligibility.IsRunEligibleForDispatchCandidate", integrationTestSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteEligibility.IsStepStatusDispatchableForRun", integrationTestSource, StringComparison.Ordinal);
        Assert.Contains("ProcessSubprocessArtifactSourceResolver.ResolveSourceArtifact", integrationTestSource, StringComparison.Ordinal);

        Assert.Contains("ApplyProjectStructureReadAccess", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchTechnicalAgentBindingCoordinator.ApplyProjectStructureReadAccess", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("LoadLatestManualRecoveryDirectiveAsync", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRecoveryQueryHelper.LoadLatestManualRecoveryDirectiveAsync", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("EnsureProviderNativeBrowserOutputDirectories", projectionUtilitiesSource, StringComparison.Ordinal);
        Assert.Contains("Directory.CreateDirectory", projectionUtilitiesSource, StringComparison.Ordinal);
        Assert.Contains("TransitionStepWithClaimAsync", projectionUtilitiesSource, StringComparison.Ordinal);

        Assert.DoesNotContain("ProcessDispatchRouteModelAdapters", routeServicesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TBD", coreDecisionMatrix, StringComparison.Ordinal);
        Assert.Contains("Candidate later, not extracted in this bundle.", coreDecisionMatrix, StringComparison.Ordinal);
        Assert.Contains("Must remain application-local.", coreDecisionMatrix, StringComparison.Ordinal);
        Assert.Contains("No production driver API", coreDecisionMatrix, StringComparison.Ordinal);
        Assert.DoesNotContain(productionProjectNames, name =>
            string.Equals(name, "CanDoItAll.Modules.Processes.Core", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("ProcessDriver", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("DriverPack", StringComparison.OrdinalIgnoreCase));
        AssertNarrowProcessCoreSeed(root);
        Assert.False(Directory.Exists(Path.Combine(root, "src", "CanDoItAll.Modules.Processes.Core")));
        Assert.DoesNotContain("IProcessDriverPack", dispatchModuleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverRegistry", dispatchModuleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverRegistry", dispatchModuleSource, StringComparison.Ordinal);
        Assert.Contains("global::CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceResolver", subprocessResolverSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", sideEffectDispatchModuleSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_pre_extraction_consolidation_SB030_INV_001_keeps_core_rehearsal_docs_tests_only()
    {
        var root = FindRepositoryRoot();
        var srcRoot = Path.Combine(root, "src");
        var bundleRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-core-pre-extraction-consolidation-driver-readiness-v1");
        var sourceText = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(srcRoot, "*.*", SearchOption.AllDirectories)
                .Where(static path =>
                    path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var contractMap = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "04-core-candidate-contract-map.md"));
        var allowDeny = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "05-future-core-allow-deny-list.md"));
        var inventory = File.ReadAllText(Path.Combine(bundleRoot, "analysis", "04-static-wrapper-inventory.md"));
        var executionReport = File.ReadAllText(Path.Combine(bundleRoot, "reviews", "01-execution-report.md"));
        var docsText = string.Join(Environment.NewLine, contractMap, allowDeny, inventory);
        var forbiddenProductionTokens = new[]
        {
            "IProcessDriverPack",
            "IProcessDriverRegistry",
            "ProcessDriverRegistry",
            "IProcessHelperDriver",
            "IProcessSwDevHelperDriver",
            "IProcessDotNetSwDevHelperDriver"
        };

        AssertNarrowProcessCoreSeed(root);
        Assert.False(Directory.Exists(Path.Combine(srcRoot, "CanDoItAll.Modules.Processes.Core")));
        Assert.All(forbiddenProductionTokens, token =>
            Assert.DoesNotContain(token, sourceText, StringComparison.Ordinal));
        Assert.Contains("Docs/tests only", contractMap, StringComparison.Ordinal);
        Assert.Contains("Allowed Future Core Candidates", allowDeny, StringComparison.Ordinal);
        Assert.Contains("Denied From Future Core", allowDeny, StringComparison.Ordinal);
        Assert.Contains("ProcessDispatchRouteEligibility", contractMap, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactExpectationSnapshot", contractMap, StringComparison.Ordinal);
        Assert.Contains("Route eligibility", inventory, StringComparison.Ordinal);
        Assert.Contains("Subprocess artifact source mapping", inventory, StringComparison.Ordinal);
        Assert.Contains("EF contexts and queries", allowDeny, StringComparison.Ordinal);
        Assert.Contains("TransitionStepWithClaimAsync", allowDeny, StringComparison.Ordinal);
        Assert.Contains("Route model adapters and dispatcher nested aliases", allowDeny, StringComparison.Ordinal);
        Assert.DoesNotContain("public interface", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("MapProcessDriver", docsText, StringComparison.Ordinal);
        Assert.Contains("| SB028 | Passed | Passed | Passed | Passed |", executionReport, StringComparison.Ordinal);
        Assert.Contains("| SB029 | Passed | Passed | Passed | Passed |", executionReport, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_pre_extraction_consolidation_SB033_INV_001_keeps_driver_readiness_docs_tests_only()
    {
        var root = FindRepositoryRoot();
        var srcRoot = Path.Combine(root, "src");
        var bundleRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-core-pre-extraction-consolidation-driver-readiness-v1");
        var sourceText = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(srcRoot, "*.*", SearchOption.AllDirectories)
                .Where(static path =>
                    path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var evidenceVocabulary = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "06-driver-evidence-vocabulary.md"));
        var negativeScenarios = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "07-driver-permission-negative-scenarios.md"));
        var executionReport = File.ReadAllText(Path.Combine(bundleRoot, "reviews", "01-execution-report.md"));
        var docsText = string.Join(Environment.NewLine, evidenceVocabulary, negativeScenarios);
        var forbiddenProductionTokens = new[]
        {
            "IProcessDriverPack",
            "IProcessDriverRegistry",
            "ProcessDriverRegistry",
            "IProcessHelperDriver",
            "IProcessSwDevHelperDriver",
            "IProcessDotNetSwDevHelperDriver",
            "MapProcessDriver"
        };

        AssertNarrowProcessCoreSeed(root);
        Assert.False(Directory.Exists(Path.Combine(srcRoot, "CanDoItAll.Modules.Processes.Core")));
        Assert.All(forbiddenProductionTokens, token =>
            Assert.DoesNotContain(token, sourceText, StringComparison.Ordinal));
        Assert.Contains("verification-only", evidenceVocabulary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Route helper evidence", evidenceVocabulary, StringComparison.Ordinal);
        Assert.Contains("Artifact helper evidence", evidenceVocabulary, StringComparison.Ordinal);
        Assert.Contains("Runtime helper evidence", evidenceVocabulary, StringComparison.Ordinal);
        Assert.Contains("Domain helper evidence", evidenceVocabulary, StringComparison.Ordinal);
        Assert.Contains("Permission negative evidence", evidenceVocabulary, StringComparison.Ordinal);
        Assert.Contains("not a production permission system", negativeScenarios, StringComparison.Ordinal);
        Assert.Contains("Absence of a production driver API is the intended state.", negativeScenarios, StringComparison.Ordinal);
        Assert.Contains("Absence of DI registration is the intended state.", negativeScenarios, StringComparison.Ordinal);
        Assert.DoesNotContain("public interface", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("MapProcessDriver", docsText, StringComparison.Ordinal);
        Assert.Contains("| SB031 | Passed | Passed | Passed | Passed |", executionReport, StringComparison.Ordinal);
        Assert.Contains("| SB032 | Passed | Passed | Passed | Passed |", executionReport, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB030_INV_001_keeps_driver_readiness_docs_traceability_only()
    {
        var root = FindRepositoryRoot();
        var srcRoot = Path.Combine(root, "src");
        var bundleRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-core-contract-candidate-driver-readiness-prep-v1");
        var sourceText = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(srcRoot, "*.*", SearchOption.AllDirectories)
                .Where(static path =>
                    path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var driverStrategy = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "02-driver-readiness-strategy.md"));
        var laneMap = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "05-driver-readiness-lane-map.md"));
        var safetyModel = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "06-driver-safety-permission-model.md"));
        var executionReport = File.ReadAllText(Path.Combine(bundleRoot, "reviews", "01-execution-report.md"));
        var docsText = string.Join(Environment.NewLine, driverStrategy, laneMap, safetyModel);
        var forbiddenProductionTokens = new[]
        {
            "IProcessDriverPack",
            "IProcessDriverRegistry",
            "ProcessDriverRegistry",
            "IProcessHelperDriver",
            "IProcessSwDevHelperDriver",
            "IProcessDotNetSwDevHelperDriver"
        };

        AssertNarrowProcessCoreSeed(root);
        Assert.False(Directory.Exists(Path.Combine(srcRoot, "CanDoItAll.Modules.Processes.Core")));
        Assert.All(forbiddenProductionTokens, token =>
            Assert.DoesNotContain(token, sourceText, StringComparison.Ordinal));
        Assert.Contains("documentation-only readiness work", laneMap, StringComparison.Ordinal);
        Assert.Contains("No lane may introduce a production interface", laneMap, StringComparison.Ordinal);
        Assert.Contains("not a production permission system", safetyModel, StringComparison.Ordinal);
        Assert.Contains("absence of a mode is a denial", safetyModel, StringComparison.Ordinal);
        Assert.Contains("not production type names or runtime contracts", driverStrategy, StringComparison.Ordinal);
        Assert.DoesNotContain("public interface", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("MapProcessDriver", docsText, StringComparison.Ordinal);
        Assert.Contains("| SB028 | Passed | Passed | Passed | Passed |", executionReport, StringComparison.Ordinal);
        Assert.Contains("| SB029 | Passed | Passed | Passed | Passed |", executionReport, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_contract_candidate_driver_readiness_SB033_INV_001_closes_final_red_team_cutline_without_core_or_driver_api()
    {
        var root = FindRepositoryRoot();
        var srcRoot = Path.Combine(root, "src");
        var bundleRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-core-contract-candidate-driver-readiness-prep-v1");
        var sourceText = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(srcRoot, "*.*", SearchOption.AllDirectories)
                .Where(static path =>
                    path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var rootReadme = File.ReadAllText(Path.Combine(bundleRoot, "README.md"));
        var executionReport = File.ReadAllText(Path.Combine(bundleRoot, "reviews", "01-execution-report.md"));
        var finalRedTeam = File.ReadAllText(Path.Combine(bundleRoot, "reviews", "02-final-red-team-review.md"));
        var scorecard = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "07-core-extraction-readiness-scorecard.md"));
        var inputCoverage = File.ReadAllText(Path.Combine(bundleRoot, "traceability", "01-input-coverage.md"));
        var gateSection = ExtractMarkdownSection(executionReport, "## Subbundle Gate Results");
        var rawNoteSection = ExtractMarkdownSection(executionReport, "## Raw Note Closure");
        var forbiddenProductionTokens = new[]
        {
            "IProcessDriverPack",
            "IProcessDriverRegistry",
            "ProcessDriverRegistry",
            "IProcessHelperDriver",
            "IProcessSwDevHelperDriver",
            "IProcessDotNetSwDevHelperDriver"
        };

        AssertNarrowProcessCoreSeed(root);
        Assert.False(Directory.Exists(Path.Combine(srcRoot, "CanDoItAll.Modules.Processes.Core")));
        Assert.All(forbiddenProductionTokens, token =>
            Assert.DoesNotContain(token, sourceText, StringComparison.Ordinal));

        foreach (var subbundleId in Enumerable.Range(1, 33).Select(static index => $"SB{index:000}"))
        {
            Assert.Contains($"| {subbundleId} | Passed | Passed | Passed | Passed |", gateSection, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Pending", rawNoteSection, StringComparison.Ordinal);
        Assert.Contains("## Status", rootReadme, StringComparison.Ordinal);
        Assert.Contains("Completed.", rootReadme, StringComparison.Ordinal);
        Assert.Contains("Execution status: `Completed`", rootReadme, StringComparison.Ordinal);
        Assert.Contains("Final closure gate: `Passed`", rootReadme, StringComparison.Ordinal);
        Assert.Contains("Recommendation", finalRedTeam, StringComparison.Ordinal);
        Assert.Contains("The next bundle may start a narrow Process Core proposal only for pure read models and deterministic rule families.", finalRedTeam, StringComparison.Ordinal);
        Assert.Contains("No production process driver API was added.", finalRedTeam, StringComparison.Ordinal);
        Assert.Contains("Driver APIs should remain out of scope", finalRedTeam, StringComparison.Ordinal);
        Assert.Contains("The next bundle may propose a narrow `CanDoItAll.Processes.Core` project only for pure read models and deterministic rules", scorecard, StringComparison.Ordinal);
        Assert.Contains("final red-team closure", inputCoverage, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_evidence_driver_contract_SB024_INV_001_keeps_driver_permission_model_non_production()
    {
        var root = FindRepositoryRoot();
        var bundleRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-core-evidence-descriptors-driver-contract-roadmap-v1");
        var sourceText = ReadProductionSourceText(root);
        var permissionModel = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "08-driver-permission-audit-requirements.md"));
        var negativeScenarios = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "09-driver-negative-scenarios.md"));
        var docsText = string.Join(Environment.NewLine, permissionModel, negativeScenarios);
        var forbiddenProductionTokens = new[]
        {
            "IProcessDriverPack",
            "IProcessDriverRegistry",
            "ProcessDriverRegistry",
            "ProcessDriverPack",
            "ProcessDriverRuntimeSelector",
            "ProcessDriverManagerCommand",
            "ProcessDriverRuntime",
            "ProcessDriverProvider"
        };

        AssertNarrowProcessCoreSeed(root);
        Assert.All(forbiddenProductionTokens, token =>
            Assert.DoesNotContain(token, sourceText, StringComparison.Ordinal));
        Assert.Contains("VerificationOnly", permissionModel, StringComparison.Ordinal);
        Assert.Contains("ManagerReadonly", permissionModel, StringComparison.Ordinal);
        Assert.Contains("ExecutionCapableFuture", permissionModel, StringComparison.Ordinal);
        Assert.Contains("Absence of an approved mode is a denial.", permissionModel, StringComparison.Ordinal);
        Assert.Contains("Required Audit Facts", permissionModel, StringComparison.Ordinal);
        Assert.Contains("Mutation denied", negativeScenarios, StringComparison.Ordinal);
        Assert.Contains("Shell denied", negativeScenarios, StringComparison.Ordinal);
        Assert.Contains("Graph denied", negativeScenarios, StringComparison.Ordinal);
        Assert.DoesNotContain("public interface", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("MapProcessDriver", docsText, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_evidence_driver_contract_SB027_INV_001_keeps_domain_schemas_readonly()
    {
        var root = FindRepositoryRoot();
        var bundleRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-core-evidence-descriptors-driver-contract-roadmap-v1");
        var sourceText = ReadProductionSourceText(root);
        var laneMap = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "05-domain-driver-lanes.md"));
        var schemas = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "10-domain-driver-readonly-evidence-schemas.md"));
        var docsText = string.Join(Environment.NewLine, laneMap, schemas);
        var forbiddenProductionTokens = new[]
        {
            "IProcessDriverPack",
            "IProcessDriverRegistry",
            "ProcessDriverRegistry",
            "ProcessDriverPack",
            "ProcessDriverRuntimeSelector",
            "ProcessDriverManagerCommand",
            "ProcessDriverRuntime",
            "ProcessDriverProvider"
        };

        AssertNarrowProcessCoreSeed(root);
        Assert.All(forbiddenProductionTokens, token =>
            Assert.DoesNotContain(token, sourceText, StringComparison.Ordinal));
        Assert.Contains(".NET / Rust verification lane", laneMap, StringComparison.Ordinal);
        Assert.Contains("Office lane", laneMap, StringComparison.Ordinal);
        Assert.Contains("Business-analysis lane", laneMap, StringComparison.Ordinal);
        Assert.Contains("Readonly build/test/proof evidence schema", schemas, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Calling Office or Graph APIs", schemas, StringComparison.Ordinal);
        Assert.Contains("Mutating CRM, project, workflow, or business records", schemas, StringComparison.Ordinal);
        Assert.DoesNotContain("State mutation allowed", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("Run shell commands", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("Workspace write allowed", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", docsText, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", docsText, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_core_evidence_driver_contract_SB033_INV_001_defers_driver_runtime_and_blocks_broad_core_extraction()
    {
        var root = FindRepositoryRoot();
        var bundleRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-core-evidence-descriptors-driver-contract-roadmap-v1");
        var sourceText = ReadProductionSourceText(root);
        var implementationDecision = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "11-production-driver-implementation-decision.md"));
        var scorecard = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "12-core-extraction-scorecard.md"));
        var nextPureFamilyDecision = File.ReadAllText(Path.Combine(bundleRoot, "architecture", "13-next-pure-family-decision.md"));
        var forbiddenProductionTokens = new[]
        {
            "IProcessDriverPack",
            "IProcessDriverRegistry",
            "ProcessDriverRegistry",
            "ProcessDriverPack",
            "ProcessDriverRuntimeSelector",
            "ProcessDriverManagerCommand",
            "ProcessDriverRuntime",
            "ProcessDriverProvider"
        };

        AssertNarrowProcessCoreSeed(root);
        Assert.All(forbiddenProductionTokens, token =>
            Assert.DoesNotContain(token, sourceText, StringComparison.Ordinal));
        Assert.Contains("Decision: No production driver implementation in this bundle.", implementationDecision, StringComparison.Ordinal);
        Assert.Contains("Alpha candidate: Defer.", implementationDecision, StringComparison.Ordinal);
        Assert.Contains("permission enforcement, audit persistence, sandbox policy, runtime ownership, and executable negative tests are not yet implemented", implementationDecision, StringComparison.Ordinal);
        Assert.Contains("No broad runtime extraction is approved.", scorecard, StringComparison.Ordinal);
        Assert.Contains("Core is stable enough for driver-contract proposal work.", scorecard, StringComparison.Ordinal);
        Assert.Contains("declare Core stable enough for driver-contract proposal work", nextPureFamilyDecision, StringComparison.Ordinal);
        Assert.Contains("No additional pure family is required", nextPureFamilyDecision, StringComparison.Ordinal);
        Assert.DoesNotContain("Implement production driver", implementationDecision + scorecard + nextPureFamilyDecision, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. pathParts]));
    }

    private static string ReadProductionSourceText(string repositoryRoot)
    {
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.*", SearchOption.AllDirectories)
                .Where(static path =>
                    path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static IReadOnlyList<string> ReadProcessCorePublicApiSurface()
    {
        var assembly = typeof(CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePipeline).Assembly;
        var lines = new List<string>();
        foreach (var type in assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            lines.Add($"Type:{type.FullName}");
            if (type.IsEnum)
            {
                foreach (var name in Enum.GetNames(type))
                {
                    lines.Add($"Enum:{type.FullName}.{name}");
                }

                continue;
            }

            const BindingFlags constructorFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            const BindingFlags memberFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var constructor in type.GetConstructors(constructorFlags))
            {
                lines.Add($"Constructor:{type.FullName}({FormatApiParameters(constructor)})");
            }

            foreach (var property in type.GetProperties(memberFlags).OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                lines.Add($"Property:{type.FullName}.{property.Name}:{FormatApiType(property.PropertyType)}");
            }

            foreach (var method in type.GetMethods(memberFlags)
                .Where(method => !method.IsSpecialName)
                .Where(method => method.Name is not "ToString" and not "GetHashCode" and not "Equals" and not "Deconstruct" and not "<Clone>$")
                .OrderBy(method => method.Name, StringComparer.Ordinal)
                .ThenBy(FormatApiParameters, StringComparer.Ordinal))
            {
                lines.Add($"Method:{type.FullName}.{method.Name}({FormatApiParameters(method)}):{FormatApiType(method.ReturnType)}");
            }
        }

        return lines;
    }

    private static string FormatApiParameters(MethodBase method)
    {
        return string.Join(",", method.GetParameters().Select(parameter => FormatApiType(parameter.ParameterType)));
    }

    private static string FormatApiType(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var name = type.GetGenericTypeDefinition().FullName ?? type.GetGenericTypeDefinition().Name;
        var tickIndex = name.IndexOf('`', StringComparison.Ordinal);
        if (tickIndex >= 0)
        {
            name = name[..tickIndex];
        }

        return $"{name}<{string.Join(",", type.GetGenericArguments().Select(FormatApiType))}>";
    }

    private static void AssertNarrowProcessCoreSeed(string repositoryRoot)
    {
        var coreRoot = Path.Combine(repositoryRoot, "src", "CanDoItAll.Processes.Core");
        var coreProjectPath = Path.Combine(coreRoot, "CanDoItAll.Processes.Core.csproj");
        var solution = File.ReadAllText(Path.Combine(repositoryRoot, "CanDoItAll.slnx"));
        var coreProject = File.ReadAllText(coreProjectPath);
        var coreSource = ReadNarrowProcessCoreSource(repositoryRoot);

        Assert.True(Directory.Exists(coreRoot));
        Assert.False(Directory.Exists(Path.Combine(repositoryRoot, "src", "CanDoItAll.Modules.Processes.Core")));
        Assert.Contains("src/CanDoItAll.Processes.Core/CanDoItAll.Processes.Core.csproj", solution, StringComparison.Ordinal);
        Assert.Contains(
            @"<ProjectReference Include=""..\CanDoItAll.Processes.Contracts\CanDoItAll.Processes.Contracts.csproj"" />",
            coreProject,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", coreProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"..\CanDoItAll.Modules.Processes\", coreProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"..\CanDoItAll.Infrastructure\", coreProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"..\CanDoItAll.AgentFramework", coreProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"..\CanDoItAll.Modules.Workspace\", coreProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"..\CanDoItAll.Modules.Plugins\", coreProject, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("namespace CanDoItAll.Processes.Core.Routing", coreSource, StringComparison.Ordinal);
        Assert.Contains("namespace CanDoItAll.Processes.Core.Subprocess", coreSource, StringComparison.Ordinal);
        Assert.Contains("namespace CanDoItAll.Processes.Core.Artifacts", coreSource, StringComparison.Ordinal);
        Assert.Contains("public enum ProcessDispatchRouteStage", coreSource, StringComparison.Ordinal);
        Assert.Contains("public static class ProcessDispatchRoutePipeline", coreSource, StringComparison.Ordinal);
        Assert.Contains("public static class ProcessDispatchRoutePlanner", coreSource, StringComparison.Ordinal);
        Assert.Contains("public static class ProcessDispatchRouteEligibility", coreSource, StringComparison.Ordinal);

        var forbiddenCoreTokens = new[]
        {
            "CanDoItAll.Modules.",
            "CanDoItAll.Infrastructure",
            "CanDoItAll.AgentFramework",
            "Microsoft.EntityFrameworkCore",
            "DbContext",
            "IDbContextFactory",
            "IWorkspace",
            "WorkspacePathResolver",
            "IStorage",
            "StoragePlacement",
            "File.",
            "Directory.",
            "AgentFramework",
            "Maf",
            "ProcessRunAutomationDispatchService",
            "IProcessDriver",
            "DriverPack",
            "DriverRegistry",
            "IServiceProvider",
            "IServiceScopeFactory",
            "ILogger<"
        };

        foreach (var forbiddenCoreToken in forbiddenCoreTokens)
        {
            Assert.DoesNotContain(forbiddenCoreToken, coreSource, StringComparison.Ordinal);
        }
    }

    private static string ReadNarrowProcessCoreSource(string repositoryRoot)
    {
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(Path.Combine(repositoryRoot, "src", "CanDoItAll.Processes.Core"), "*.cs", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string ReadDispatchSourceExcludingCorePureRuleAdapters(string dispatchDirectory)
    {
        var allowedCoreAdapterFiles = CreateAllowedDispatchProcessCoreConsumerFiles();

        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(dispatchDirectory, "*.cs")
                .Where(path => !allowedCoreAdapterFiles.Contains(Path.GetFileName(path)))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static HashSet<string> CreateAllowedDispatchProcessCoreConsumerFiles()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ProcessArtifactExpectationMatcher.cs",
            "ProcessArtifactExpectationSatisfactionAdapter.cs",
            "ProcessArtifactProjectionEvidenceDescriptorAdapter.cs",
            "ProcessArtifactRecordedSatisfactionRules.cs",
            "ProcessArtifactValidationDescriptorAdapter.cs",
            "ProcessCoreArtifactModelAdapters.cs",
            "ProcessDispatchCandidateHeaderSelector.cs",
            "ProcessDispatchCandidateHydrationLoader.cs",
            "ProcessDispatchRouteExecutionModels.cs",
            "ProcessDispatchRouteFacets.cs",
            "ProcessDispatchRouteHandlerPipeline.cs",
            "ProcessDispatchRouteHandlers.cs",
            "ProcessDispatchRouteModelAdapters.cs",
            "ProcessDispatchRunClosureGuardService.cs",
            "ProcessDispatchStartTransitionPlanner.cs",
            "ProcessExecutionEvidenceDescriptorAdapter.cs",
            "ProcessFinalizerEvidenceDescriptorAdapter.cs",
            "ProcessRuntimeEvidenceVerificationReadOnlyAdapter.cs",
            "ProcessRetryDiagnosticDescriptorAdapter.cs",
            "ProcessRunAutomationDispatchService.Concurrency.cs",
            "ProcessSubprocessArtifactSourceResolver.cs",
            "ProcessSubprocessLifecycleRules.cs",
            "ProcessTransitionIntentAdapters.cs"
        };
    }

    private static IReadOnlyList<string> ReadGitChangedFilesOutsideBundle(string repositoryRoot)
    {
        var tracked = RunGit(
            repositoryRoot,
            "diff",
            "--name-only",
            "--",
            ".",
            ":(exclude)codex/bundles");
        var untracked = RunGit(
            repositoryRoot,
            "ls-files",
            "--others",
            "--exclude-standard",
            "--",
            ".",
            ":(exclude)codex/bundles");

        return tracked
            .Concat(untracked)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadGitChangedFilesForCurrentProcessCoreStabilizationBundle(string repositoryRoot)
    {
        var tracked = RunGit(
            repositoryRoot,
            "diff",
            "--name-only",
            "--",
            "src",
            "tests",
            "codex/bundles/process-core-stabilization-diagnostics-driver-roadmap-v1");
        var untracked = RunGit(
            repositoryRoot,
            "ls-files",
            "--others",
            "--exclude-standard",
            "--",
            "src",
            "tests",
            "codex/bundles/process-core-stabilization-diagnostics-driver-roadmap-v1");

        return tracked
            .Concat(untracked)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> RunGit(string repositoryRoot, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), $"Failed to start git {string.Join(" ", arguments)}.");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        process.WaitForExit();
        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();

        Assert.True(
            process.ExitCode == 0,
            $"git {string.Join(" ", arguments)} failed with exit code {process.ExitCode}: {error}");

        return output
            .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    private static bool IsUiOrMobileProofPath(string path)
    {
        var normalizedPath = path.Replace('\\', '/');
        var extension = Path.GetExtension(normalizedPath);
        var forbiddenExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".razor",
            ".css",
            ".scss",
            ".js",
            ".ts",
            ".tsx",
            ".jsx",
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".webp",
            ".svg"
        };
        var forbiddenTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet"
        };

        return forbiddenExtensions.Contains(extension) ||
            forbiddenTokens.Any(token => normalizedPath.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractMarkdownSection(string content, string heading)
    {
        var startIndex = content.IndexOf(heading, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            throw new InvalidOperationException($"Could not find markdown heading '{heading}'.");
        }

        var sectionStart = startIndex + heading.Length;
        var remainingContent = content[sectionStart..];
        var nextHeading = Regex.Match(remainingContent, @"\r?\n## ");

        return nextHeading.Success
            ? remainingContent[..nextHeading.Index]
            : remainingContent;
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(sourceFilePath) ?? string.Empty })
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                continue;
            }

            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
