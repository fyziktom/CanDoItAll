using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessManagedArtifactEvidenceTests
{
    [Fact]
    public void Product_target_receipts_keep_case_distinct_versioned_aliases()
    {
        const string rootId = "0123456789abcdef01234567";
        var upperAlias = ExternalTargetAliasCodec.BuildAlias(rootId, ["Foo"]);
        var lowerAlias = ExternalTargetAliasCodec.BuildAlias(rootId, ["foo"]);
        var launchVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ProductRootAlias"] = upperAlias,
            ["OutputRootAlias"] = lowerAlias
        };

        var receipts = ProcessManagedArtifactEvidence.ResolveProductTargetReceiptRefs(launchVariables);

        Assert.Equal(2, receipts.Count);
        Assert.Contains(upperAlias, receipts, StringComparer.Ordinal);
        Assert.Contains(lowerAlias, receipts, StringComparer.Ordinal);
    }

    [Fact]
    public void Launch_variable_string_list_keeps_case_distinct_versioned_aliases()
    {
        const string rootId = "0123456789abcdef01234567";
        var upperAlias = ExternalTargetAliasCodec.BuildAlias(rootId, ["Foo"]);
        var lowerAlias = ExternalTargetAliasCodec.BuildAlias(rootId, ["foo"]);

        var parsed = ProcessLaunchVariableStringList.TryParse(
            $"{upperAlias};{lowerAlias}",
            out var aliases);

        Assert.True(parsed);
        Assert.Equal(2, aliases.Count);
    }

    [Fact]
    public void Parent_artifact_refs_keep_case_distinct_paths()
    {
        const string rootId = "0123456789abcdef01234567";
        var upperAlias = ExternalTargetAliasCodec.BuildAlias(rootId, ["Foo"]);
        var lowerAlias = ExternalTargetAliasCodec.BuildAlias(rootId, ["foo"]);
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProcessRuntimeLaunchVariables.ParentRequiredArtifactRefs] =
                ProcessRuntimeLaunchVariables.SerializeParentRequiredArtifactRefs([upperAlias, lowerAlias])
        };

        var parsed = ProcessRuntimeLaunchVariables.TryReadParentRequiredArtifactRefs(variables, out var aliases);

        Assert.True(parsed);
        Assert.Equal(2, aliases.Count);
    }
}
