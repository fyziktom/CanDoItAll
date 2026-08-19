using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectStructureAgentAcceptanceOracleTests(ITestOutputHelper output)
{
    private const string NodeCreateToolName = "project_structure_node_create";

    private static readonly ProjectStructureCanonicalNodeSnapshot ExpectedNode = new(
        "node-created-by-agent",
        "architecture-parent",
        ProjectObjectType.ProjectBlock,
        "architecture",
        "Agent-created architecture node",
        "Persisted by the Project Structure tool",
        "active",
        "Canonical target",
        "{\"source\":\"agent\"}");

    private static readonly ProjectStructureCanonicalNodeSnapshot Sentinel = new(
        "authority-negative-sentinel",
        "architecture-parent",
        ProjectObjectType.Note,
        "authority-sentinel",
        "Authority-negative sentinel",
        "Must remain unchanged",
        "locked",
        "Baseline canonical evidence",
        "{\"immutable\":true}");

    private static readonly ProjectStructureCanonicalNodeSnapshot UnrelatedBaselineNode = new(
        "unrelated-baseline-node",
        "architecture-parent",
        ProjectObjectType.Note,
        "baseline-note",
        "Existing planning note",
        "Outside the requested change",
        "active",
        "Must be preserved exactly",
        "{\"source\":\"baseline\"}");

    private static readonly ProjectStructureCanonicalLinkSnapshot BaselineLink = new(
        Sentinel.Id,
        UnrelatedBaselineNode.Id,
        ProjectObjectLinkKind.Contains,
        true);

    private static readonly ProjectStructureCanonicalManagedAssetSnapshot BaselineAsset = new(
        UnrelatedBaselineNode.Id,
        "managed-files/project-media/files/baseline.md",
        "text/markdown",
        "baseline.md",
        18,
        "DB97B55016C47C59880E103160E4550C2A45D03D74AE07B71C321274099CD013");

    private static readonly ProjectStructureCanonicalHierarchyEdgeSnapshot BaselineHierarchyEdge = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"));

    private static readonly ProjectStructureAcceptanceContract Contract = new(
        [NodeCreateToolName],
        CreateGraph(
            [Sentinel, UnrelatedBaselineNode],
            [BaselineLink],
            [BaselineAsset],
            [BaselineHierarchyEdge]),
        new ProjectStructureCanonicalAllowedDelta([ExpectedNode], []),
        Sentinel.Id);

    [Fact]
    public void Complete_manifest_receipt_and_canonical_evidence_is_accepted()
    {
        var decision = ProjectStructureAgentAcceptanceOracle.Evaluate(
            Contract,
            CreateCompleteEvidence());

        Assert.True(decision.IsAccepted);
        Assert.Empty(decision.Rejections);
    }

    [Fact]
    public void Completed_assistant_claim_requires_successful_receipt_and_matching_canonical_node()
    {
        var completedProseOnly = CreateCompleteEvidence() with
        {
            Receipts = [],
            CanonicalGraphAfter = Contract.CanonicalGraphBefore
        };
        var failedReceipt = CreateCompleteEvidence() with
        {
            Receipts =
            [
                new(
                    NodeCreateToolName,
                    ProjectStructureAcceptanceReceiptOutcome.Failed)
            ]
        };
        var canonicalMismatch = CreateCompleteEvidence() with
        {
            CanonicalGraphAfter = ReplaceNodes(
                CreateCompleteEvidence().CanonicalGraphAfter,
                ExpectedNode with { Title = "Assistant prose claimed a different title" },
                Sentinel,
                UnrelatedBaselineNode)
        };

        var proseOnlyDecision = ProjectStructureAgentAcceptanceOracle.Evaluate(Contract, completedProseOnly);
        var failedReceiptDecision = ProjectStructureAgentAcceptanceOracle.Evaluate(Contract, failedReceipt);
        var canonicalMismatchDecision = ProjectStructureAgentAcceptanceOracle.Evaluate(Contract, canonicalMismatch);

        Assert.Equal(ExecutionState.Completed, completedProseOnly.RunState);
        Assert.Contains("successfully created", completedProseOnly.AssistantResponseText, StringComparison.Ordinal);
        Assert.False(proseOnlyDecision.IsAccepted);
        Assert.All(
            proseOnlyDecision.Rejections,
            rejection => Assert.Equal(
                Sb01ProjectStructureInvariantIds.ReceiptAndCanonicalEvidenceRequired,
                rejection.InvariantId));
        Assert.Contains(
            proseOnlyDecision.Rejections,
            rejection => rejection.Failure == ProjectStructureAcceptanceFailure.RequiredSuccessfulReceiptMissing);
        Assert.Contains(
            proseOnlyDecision.Rejections,
            rejection => rejection.Failure == ProjectStructureAcceptanceFailure.ExpectedCanonicalNodeMissing);

        var failedReceiptRejection = Assert.Single(failedReceiptDecision.Rejections);
        Assert.Equal(
            ProjectStructureAcceptanceFailure.RequiredSuccessfulReceiptMissing,
            failedReceiptRejection.Failure);
        Assert.Equal(
            Sb01ProjectStructureInvariantIds.ReceiptAndCanonicalEvidenceRequired,
            failedReceiptRejection.InvariantId);

        var canonicalMismatchRejection = Assert.Single(canonicalMismatchDecision.Rejections);
        Assert.Equal(
            ProjectStructureAcceptanceFailure.ExpectedCanonicalNodeMismatch,
            canonicalMismatchRejection.Failure);
        Assert.Equal(
            Sb01ProjectStructureInvariantIds.ReceiptAndCanonicalEvidenceRequired,
            canonicalMismatchRejection.InvariantId);
    }

    [Fact]
    public void Successful_receipt_without_required_tool_manifest_is_rejected()
    {
        var evidence = CreateCompleteEvidence() with { ToolManifest = [] };

        var decision = ProjectStructureAgentAcceptanceOracle.Evaluate(Contract, evidence);

        var rejection = Assert.Single(decision.Rejections);
        Assert.False(decision.IsAccepted);
        Assert.Equal(
            Sb01ProjectStructureInvariantIds.RequiredToolManifestMustBePresent,
            rejection.InvariantId);
        Assert.Equal(
            ProjectStructureAcceptanceFailure.RequiredToolMissingFromManifest,
            rejection.Failure);
    }

    [Fact]
    public void Canonical_sentinel_mutation_is_rejected_despite_other_complete_evidence()
    {
        var evidence = CreateCompleteEvidence() with
        {
            CanonicalGraphAfter = ReplaceNodes(
                CreateCompleteEvidence().CanonicalGraphAfter,
                ExpectedNode,
                Sentinel with { Notes = "Mutated after the agent run" },
                UnrelatedBaselineNode)
        };

        var decision = ProjectStructureAgentAcceptanceOracle.Evaluate(Contract, evidence);

        var rejection = Assert.Single(decision.Rejections);
        Assert.False(decision.IsAccepted);
        Assert.Equal(
            Sb01ProjectStructureInvariantIds.CanonicalSentinelMustRemainUnchanged,
            rejection.InvariantId);
        Assert.Equal(ProjectStructureAcceptanceFailure.CanonicalSentinelDrifted, rejection.Failure);
    }

    [Fact]
    public void Red_team_transcript_records_oracle_rejections_for_incomplete_evidence()
    {
        var attempts = new[]
        {
            EvaluateAttempt(
                "completed-prose-only",
                CreateCompleteEvidence() with
                {
                    Receipts = [],
                    CanonicalGraphAfter = Contract.CanonicalGraphBefore
                }),
            EvaluateAttempt(
                "failed-required-receipt",
                CreateCompleteEvidence() with
                {
                    Receipts =
                    [
                        new(
                            NodeCreateToolName,
                            ProjectStructureAcceptanceReceiptOutcome.Failed)
                    ]
                }),
            EvaluateAttempt(
                "canonical-node-mismatch",
                CreateCompleteEvidence() with
                {
                    CanonicalGraphAfter = ReplaceNodes(
                        CreateCompleteEvidence().CanonicalGraphAfter,
                        ExpectedNode with { ParentId = "wrong-parent" },
                        Sentinel,
                        UnrelatedBaselineNode)
                }),
            EvaluateAttempt(
                "missing-required-tool-manifest",
                CreateCompleteEvidence() with { ToolManifest = [] }),
            EvaluateAttempt(
                "canonical-sentinel-mutation",
                CreateCompleteEvidence() with
                {
                    CanonicalGraphAfter = ReplaceNodes(
                        CreateCompleteEvidence().CanonicalGraphAfter,
                        ExpectedNode,
                        Sentinel with { Title = "Unauthorized sentinel mutation" },
                        UnrelatedBaselineNode)
                })
        };
        var transcriptLines = attempts.Select(RenderTranscriptLine).ToArray();
        output.WriteLine(string.Join(Environment.NewLine, transcriptLines));

        Assert.Equal(
            [
                "completed-prose-only|REJECT|receipt-and-canonical-evidence|RequiredSuccessfulReceiptMissing,ExpectedCanonicalNodeMissing",
                "failed-required-receipt|REJECT|receipt-and-canonical-evidence|RequiredSuccessfulReceiptMissing",
                "canonical-node-mismatch|REJECT|receipt-and-canonical-evidence|ExpectedCanonicalNodeMismatch",
                "missing-required-tool-manifest|REJECT|required-tool-manifest|RequiredToolMissingFromManifest",
                "canonical-sentinel-mutation|REJECT|canonical-sentinel-unchanged|CanonicalSentinelDrifted"
            ],
            transcriptLines);
        Assert.All(attempts, attempt => Assert.False(attempt.Decision.IsAccepted));
    }

    [Fact]
    public void Unrelated_baseline_mutation_is_rejected()
    {
        var evidence = CreateCompleteEvidence() with
        {
            CanonicalGraphAfter = ReplaceNodes(
                CreateCompleteEvidence().CanonicalGraphAfter,
                ExpectedNode,
                Sentinel,
                UnrelatedBaselineNode with { Subtitle = "Unauthorized collateral edit" })
        };

        var decision = ProjectStructureAgentAcceptanceOracle.Evaluate(Contract, evidence);

        var rejection = Assert.Single(decision.Rejections);
        Assert.Equal(Sb01ProjectStructureInvariantIds.CanonicalAllowedDeltaMustBeExact, rejection.InvariantId);
        Assert.Equal(ProjectStructureAcceptanceFailure.CanonicalBaselineNodeDrifted, rejection.Failure);
        Assert.Equal(UnrelatedBaselineNode.Id, rejection.EvidenceKey);
    }

    [Fact]
    public void Unrelated_baseline_deletion_is_rejected()
    {
        var evidence = CreateCompleteEvidence() with
        {
            CanonicalGraphAfter = ReplaceNodes(
                CreateCompleteEvidence().CanonicalGraphAfter,
                ExpectedNode,
                Sentinel)
        };

        var decision = ProjectStructureAgentAcceptanceOracle.Evaluate(Contract, evidence);

        var rejection = Assert.Single(decision.Rejections);
        Assert.Equal(ProjectStructureAcceptanceFailure.CanonicalBaselineNodeMissing, rejection.Failure);
        Assert.Equal(UnrelatedBaselineNode.Id, rejection.EvidenceKey);
    }

    [Fact]
    public void Unexpected_node_creation_is_rejected()
    {
        var unexpected = ExpectedNode with
        {
            Id = "unauthorized-extra-node",
            Title = "Unexpected collateral creation"
        };
        var evidence = CreateCompleteEvidence() with
        {
            CanonicalGraphAfter = ReplaceNodes(
                CreateCompleteEvidence().CanonicalGraphAfter,
                ExpectedNode,
                Sentinel,
                UnrelatedBaselineNode,
                unexpected)
        };

        var decision = ProjectStructureAgentAcceptanceOracle.Evaluate(Contract, evidence);

        var rejection = Assert.Single(decision.Rejections);
        Assert.Equal(ProjectStructureAcceptanceFailure.UnexpectedCanonicalNode, rejection.Failure);
        Assert.Equal(unexpected.Id, rejection.EvidenceKey);
    }

    [Fact]
    public void Expected_deletion_that_remains_is_rejected()
    {
        var deleteContract = Contract with
        {
            AllowedDelta = new ProjectStructureCanonicalAllowedDelta(
                [ExpectedNode],
                [UnrelatedBaselineNode.Id])
            {
                DeletedLinks = [BaselineLink],
                DeletedManagedAssetNodeIds = [UnrelatedBaselineNode.Id]
            }
        };
        var complete = CreateCompleteEvidence();
        var evidence = complete with
        {
            CanonicalGraphAfter = complete.CanonicalGraphAfter with
            {
                Links = [],
                ManagedAssets = []
            }
        };

        var decision = ProjectStructureAgentAcceptanceOracle.Evaluate(
            deleteContract,
            evidence);

        var rejection = Assert.Single(decision.Rejections);
        Assert.Equal(ProjectStructureAcceptanceFailure.ExpectedDeletedNodeStillPresent, rejection.Failure);
        Assert.Equal(UnrelatedBaselineNode.Id, rejection.EvidenceKey);
    }

    [Fact]
    public void Duplicate_canonical_node_identifier_is_rejected()
    {
        var evidence = CreateCompleteEvidence() with
        {
            CanonicalGraphAfter = ReplaceNodes(
                CreateCompleteEvidence().CanonicalGraphAfter,
                ExpectedNode,
                ExpectedNode,
                Sentinel,
                UnrelatedBaselineNode)
        };

        var decision = ProjectStructureAgentAcceptanceOracle.Evaluate(Contract, evidence);

        var rejection = Assert.Single(decision.Rejections);
        Assert.Equal(ProjectStructureAcceptanceFailure.DuplicateCanonicalNode, rejection.Failure);
        Assert.Equal(ExpectedNode.Id, rejection.EvidenceKey);
    }

    [Fact]
    public void Allowed_deletion_must_reference_baseline_node()
    {
        var invalidContract = Contract with
        {
            AllowedDelta = new ProjectStructureCanonicalAllowedDelta(
                [ExpectedNode],
                ["not-in-baseline"])
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            ProjectStructureAgentAcceptanceOracle.Evaluate(
                invalidContract,
                CreateCompleteEvidence()));

        Assert.Contains("must exist", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Canonical_link_set_rejects_missing_unexpected_and_duplicate_entries()
    {
        var unexpectedLink = new ProjectStructureCanonicalLinkSnapshot(
            ExpectedNode.Id,
            Sentinel.Id,
            ProjectObjectLinkKind.DependsOn,
            true);
        var complete = CreateCompleteEvidence();
        var missing = ProjectStructureAgentAcceptanceOracle.Evaluate(
            Contract,
            complete with
            {
                CanonicalGraphAfter = complete.CanonicalGraphAfter with { Links = [] }
            });
        var unexpected = ProjectStructureAgentAcceptanceOracle.Evaluate(
            Contract,
            complete with
            {
                CanonicalGraphAfter = complete.CanonicalGraphAfter with
                {
                    Links = [BaselineLink, unexpectedLink]
                }
            });
        var duplicate = ProjectStructureAgentAcceptanceOracle.Evaluate(
            Contract,
            complete with
            {
                CanonicalGraphAfter = complete.CanonicalGraphAfter with
                {
                    Links = [BaselineLink, BaselineLink]
                }
            });

        Assert.Equal(
            ProjectStructureAcceptanceFailure.CanonicalBaselineLinkMissing,
            Assert.Single(missing.Rejections).Failure);
        Assert.Equal(
            ProjectStructureAcceptanceFailure.UnexpectedCanonicalLink,
            Assert.Single(unexpected.Rejections).Failure);
        Assert.Equal(
            ProjectStructureAcceptanceFailure.DuplicateCanonicalLink,
            Assert.Single(duplicate.Rejections).Failure);
    }

    [Fact]
    public void Canonical_managed_asset_set_rejects_missing_unexpected_duplicate_and_content_drift()
    {
        var unexpectedAsset = BaselineAsset with
        {
            NodeId = ExpectedNode.Id,
            MediaRelativePath = "managed-files/project-media/files/unexpected.md"
        };
        var complete = CreateCompleteEvidence();
        var missing = ProjectStructureAgentAcceptanceOracle.Evaluate(
            Contract,
            complete with
            {
                CanonicalGraphAfter = complete.CanonicalGraphAfter with { ManagedAssets = [] }
            });
        var unexpected = ProjectStructureAgentAcceptanceOracle.Evaluate(
            Contract,
            complete with
            {
                CanonicalGraphAfter = complete.CanonicalGraphAfter with
                {
                    ManagedAssets = [BaselineAsset, unexpectedAsset]
                }
            });
        var duplicate = ProjectStructureAgentAcceptanceOracle.Evaluate(
            Contract,
            complete with
            {
                CanonicalGraphAfter = complete.CanonicalGraphAfter with
                {
                    ManagedAssets = [BaselineAsset, BaselineAsset]
                }
            });
        var contentDrift = ProjectStructureAgentAcceptanceOracle.Evaluate(
            Contract,
            complete with
            {
                CanonicalGraphAfter = complete.CanonicalGraphAfter with
                {
                    ManagedAssets =
                    [
                        BaselineAsset with
                        {
                            ContentLength = BaselineAsset.ContentLength + 1,
                            Sha256 = new string('A', 64)
                        }
                    ]
                }
            });

        Assert.Equal(
            ProjectStructureAcceptanceFailure.CanonicalBaselineAssetMissing,
            Assert.Single(missing.Rejections).Failure);
        Assert.Equal(
            ProjectStructureAcceptanceFailure.UnexpectedCanonicalAsset,
            Assert.Single(unexpected.Rejections).Failure);
        Assert.Equal(
            ProjectStructureAcceptanceFailure.DuplicateCanonicalAsset,
            Assert.Single(duplicate.Rejections).Failure);
        Assert.Equal(
            ProjectStructureAcceptanceFailure.CanonicalBaselineAssetDrifted,
            Assert.Single(contentDrift.Rejections).Failure);
    }

    [Fact]
    public void Canonical_hierarchy_set_rejects_missing_unexpected_and_duplicate_edges()
    {
        var unexpectedEdge = new ProjectStructureCanonicalHierarchyEdgeSnapshot(
            BaselineHierarchyEdge.ChildProjectId,
            Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var complete = CreateCompleteEvidence();
        var missing = ProjectStructureAgentAcceptanceOracle.Evaluate(
            Contract,
            complete with
            {
                CanonicalGraphAfter = complete.CanonicalGraphAfter with { HierarchyEdges = [] }
            });
        var unexpected = ProjectStructureAgentAcceptanceOracle.Evaluate(
            Contract,
            complete with
            {
                CanonicalGraphAfter = complete.CanonicalGraphAfter with
                {
                    HierarchyEdges = [BaselineHierarchyEdge, unexpectedEdge]
                }
            });
        var duplicate = ProjectStructureAgentAcceptanceOracle.Evaluate(
            Contract,
            complete with
            {
                CanonicalGraphAfter = complete.CanonicalGraphAfter with
                {
                    HierarchyEdges = [BaselineHierarchyEdge, BaselineHierarchyEdge]
                }
            });

        Assert.Equal(
            ProjectStructureAcceptanceFailure.CanonicalBaselineHierarchyEdgeMissing,
            Assert.Single(missing.Rejections).Failure);
        Assert.Equal(
            ProjectStructureAcceptanceFailure.UnexpectedCanonicalHierarchyEdge,
            Assert.Single(unexpected.Rejections).Failure);
        Assert.Equal(
            ProjectStructureAcceptanceFailure.DuplicateCanonicalHierarchyEdge,
            Assert.Single(duplicate.Rejections).Failure);
    }

    [Fact]
    public void Exact_allowed_delta_can_add_a_node_link_asset_and_subproject_edge_together()
    {
        var expectedLink = new ProjectStructureCanonicalLinkSnapshot(
            Sentinel.Id,
            ExpectedNode.Id,
            ProjectObjectLinkKind.Contains,
            true);
        var expectedAsset = BaselineAsset with
        {
            NodeId = ExpectedNode.Id,
            MediaRelativePath = "managed-files/project-media/files/expected.md",
            MediaOriginalFileName = "expected.md"
        };
        var expectedHierarchyEdge = new ProjectStructureCanonicalHierarchyEdgeSnapshot(
            BaselineHierarchyEdge.ChildProjectId,
            Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var contract = Contract with
        {
            AllowedDelta = new ProjectStructureCanonicalAllowedDelta([ExpectedNode], [])
            {
                UpsertedLinks = [expectedLink],
                UpsertedManagedAssets = [expectedAsset],
                UpsertedHierarchyEdges = [expectedHierarchyEdge]
            }
        };
        var complete = CreateCompleteEvidence();
        var evidence = complete with
        {
            CanonicalGraphAfter = complete.CanonicalGraphAfter with
            {
                Links = [BaselineLink, expectedLink],
                ManagedAssets = [BaselineAsset, expectedAsset],
                HierarchyEdges = [BaselineHierarchyEdge, expectedHierarchyEdge]
            }
        };

        var decision = ProjectStructureAgentAcceptanceOracle.Evaluate(contract, evidence);

        Assert.True(decision.IsAccepted);
        Assert.Empty(decision.Rejections);
    }

    private static ProjectStructureAcceptanceEvidence CreateCompleteEvidence()
    {
        return new ProjectStructureAcceptanceEvidence(
            ExecutionState.Completed,
            "I successfully created the requested Project Structure node.",
            [NodeCreateToolName],
            [
                new(
                    NodeCreateToolName,
                    ProjectStructureAcceptanceReceiptOutcome.Succeeded)
            ],
            CreateGraph(
                [ExpectedNode, Sentinel, UnrelatedBaselineNode],
                [BaselineLink],
                [BaselineAsset],
                [BaselineHierarchyEdge]));
    }

    private static ProjectStructureCanonicalGraphSnapshot CreateGraph(
        IReadOnlyList<ProjectStructureCanonicalNodeSnapshot> nodes,
        IReadOnlyList<ProjectStructureCanonicalLinkSnapshot> links,
        IReadOnlyList<ProjectStructureCanonicalManagedAssetSnapshot> assets,
        IReadOnlyList<ProjectStructureCanonicalHierarchyEdgeSnapshot> hierarchyEdges)
    {
        return new ProjectStructureCanonicalGraphSnapshot(nodes, links, assets, hierarchyEdges);
    }

    private static ProjectStructureCanonicalGraphSnapshot ReplaceNodes(
        ProjectStructureCanonicalGraphSnapshot graph,
        params ProjectStructureCanonicalNodeSnapshot[] nodes)
    {
        return graph with { Nodes = nodes };
    }

    private static RedTeamAttempt EvaluateAttempt(
        string name,
        ProjectStructureAcceptanceEvidence evidence)
    {
        return new RedTeamAttempt(
            name,
            ProjectStructureAgentAcceptanceOracle.Evaluate(Contract, evidence));
    }

    private static string RenderTranscriptLine(RedTeamAttempt attempt)
    {
        var invariantIds = attempt.Decision.Rejections
            .Select(rejection => rejection.InvariantId)
            .Distinct(StringComparer.Ordinal);
        var failures = attempt.Decision.Rejections.Select(rejection => rejection.Failure);
        return string.Join(
            '|',
            attempt.Name,
            attempt.Decision.IsAccepted ? "ACCEPT" : "REJECT",
            string.Join(',', invariantIds),
            string.Join(',', failures));
    }

    private sealed record RedTeamAttempt(
        string Name,
        ProjectStructureAcceptanceDecision Decision);
}
