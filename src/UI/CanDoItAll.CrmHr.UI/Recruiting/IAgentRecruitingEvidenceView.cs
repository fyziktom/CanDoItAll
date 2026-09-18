using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AppComponents;
using CanDoItAll.CrmHr.UI.Activity;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Pickers;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.CrmHr.UI.Recruiting;

// What the agent assessment evidence region renders and what it can ask its host to do. The host implements it: it
// owns the candidate, readiness and interview reads and the interview and attempt commands of the technical owner.
public interface IAgentRecruitingEvidenceView : ICrmHrWorkspaceView
{
    AgentDefinition? Candidate { get; }

    // The configuration version the technical owner computes for the candidate; assessments are pinned to it.
    string? CandidateConfigurationVersion { get; }
    AgentRecruitingCandidateReadiness? Readiness { get; }
    IReadOnlyList<AgentRecruitingInterview> Interviews { get; }
    IReadOnlyList<AgentDefinition> EvaluatorAgents { get; }
    IReadOnlyList<ProviderProfile> Providers { get; }
    string CreateCandidateName { get; set; }
    string CreatePurpose { get; set; }
    string LoadError { get; }
    AttemptDraft AttemptDraft { get; }
    bool IsLoading { get; }
    bool IsSaving { get; }
    bool IsCreateDialogOpen { get; }
    bool IsAttachDialogOpen { get; }
    Guid? CandidateAgentId { get; }
    EventCallback<RecruitmentTrainingRequest> RequestTraining { get; }
    int AttemptCount { get; }
    AgentRecruitingAssessmentAnalysis? LatestAnalysis { get; }
    Task ReloadAsync();
    Task OpenCreateDialog();
    Task CloseCreateDialog();
    Task OpenRecheckDialog(AgentRecruitingInterview interview, AgentRecruitingAttempt attempt);
    Task CreateAssessmentAsync();
    Task OpenAttachDialog();
    Task CloseAttachDialog();
    Task AttachResultAsync();
    string FormatLatestScore();
    string FormatLatestNextStep();
}
