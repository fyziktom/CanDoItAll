using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessPreparedLaunchProjectScopeTests {
    [Theory]
    [InlineData(ScopeFact.None, false)]
    [InlineData(ScopeFact.UnrelatedText, false)]
    [InlineData(ScopeFact.Authority, true)]
    [InlineData(ScopeFact.LinkTarget, true)]
    [InlineData(ScopeFact.RequestProject, true)]
    [InlineData(ScopeFact.RequestNode, true)]
    [InlineData(ScopeFact.RequestVariable, true)]
    [InlineData(ScopeFact.AssignmentVariable, true)]
    [InlineData(ScopeFact.MalformedProjectVariable, true)]
    [InlineData(ScopeFact.DeliveredLink, true)]
    public void Owner_scope_reads_actual_typed_and_legacy_project_facts_without_rewriting_evidence(ScopeFact fact, bool expected) {
        var project = new ProcessProjectAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var preparation = fact == ScopeFact.Authority
            ? ProcessPreparedLaunchFixture.Create(ProcessPreparedLaunchFixture.Local(project.DatabaseProfileId, project))
            : ProcessPreparedLaunchFixture.Create(ProcessPreparedLaunchFixture.Local(project.DatabaseProfileId));
        preparation = fact switch {
            ScopeFact.LinkTarget => preparation with { LinkTarget = new(project.ProjectId, "node", "binding") },
            ScopeFact.RequestProject => preparation with { Request = preparation.Request with { ProjectId = project.ProjectId } },
            ScopeFact.RequestNode => preparation with { Request = preparation.Request with { ProjectNodeId = "node:historical" } },
            ScopeFact.RequestVariable or ScopeFact.MalformedProjectVariable => preparation with {
                Request = preparation.Request with { Variables = new Dictionary<string, string> {
                    [ProcessRuntimeLaunchVariables.ProjectId] = fact == ScopeFact.RequestVariable ? project.ProjectId.ToString("D") : "malformed"
                } }
            },
            ScopeFact.UnrelatedText => preparation with {
                Request = preparation.Request with { Variables = new Dictionary<string, string> { ["Topic"] = "ProjectId is discussed in this text." } }
            },
            ScopeFact.AssignmentVariable => preparation with {
                InitialCommit = preparation.InitialCommit with {
                    InitialAssignments = preparation.InitialCommit.InitialAssignments!.Select(assignment => assignment with {
                        LaunchVariables = new Dictionary<string, string> { [ProcessRuntimeLaunchVariables.ProjectNodeId] = "node:historical" }
                    }).ToArray()
                }
            },
            _ => preparation
        };
        preparation = preparation with { RequestFingerprint = ProcessLaunchIntentFingerprint.Compute(preparation.Request) };
        var row = ProcessPreparedLaunchCodec.ToEntity(preparation);
        if (fact == ScopeFact.DeliveredLink) {
            row.DeliveredLinkId = Guid.NewGuid();
        }
        var payload = row.PayloadJson;
        var fingerprint = row.PreparationFingerprint;
        Assert.Equal(expected, row.ReferencesProject());
        Assert.Equal(payload, row.PayloadJson);
        Assert.Equal(fingerprint, row.PreparationFingerprint);
    }

    [Fact]
    public void Corrupt_prepared_evidence_is_rejected_instead_of_classified_as_unrelated() {
        var row = ProcessPreparedLaunchCodec.ToEntity(ProcessPreparedLaunchFixture.Create());
        row.PayloadJson += " ";
        Assert.Throws<InvalidOperationException>(() => row.ReferencesProject());
    }

    public enum ScopeFact {
        None, UnrelatedText, Authority, LinkTarget, RequestProject, RequestNode,
        RequestVariable, AssignmentVariable, MalformedProjectVariable, DeliveredLink
    }
}
