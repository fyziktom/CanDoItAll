using CanDoItAll.AgentFramework.Core;
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
using CanDoItAll.CrmHr.UI;
using CanDoItAll.CrmHr.UI.Recruiting;

namespace CanDoItAll.Modules.CrmHr.Components;

// The panel is the evidence surface's view: the surface binds to these members and to nothing else of the host.
public partial class AgentRecruitingEvidencePanel : IAgentRecruitingEvidenceView
{
    void ICrmHrWorkspaceView.RequestRender() => _ = InvokeAsync(StateHasChanged);

    AgentDefinition? IAgentRecruitingEvidenceView.Candidate => candidate;
    string? IAgentRecruitingEvidenceView.CandidateConfigurationVersion => candidate is null ? null : AgentConfigurationVersion.Create(candidate);
    AgentRecruitingCandidateReadiness? IAgentRecruitingEvidenceView.Readiness => readiness;
    IReadOnlyList<AgentRecruitingInterview> IAgentRecruitingEvidenceView.Interviews => interviews;
    IReadOnlyList<AgentDefinition> IAgentRecruitingEvidenceView.EvaluatorAgents => evaluatorAgents;
    IReadOnlyList<ProviderProfile> IAgentRecruitingEvidenceView.Providers => providers;
    string IAgentRecruitingEvidenceView.CreateCandidateName { get => createCandidateName; set => createCandidateName = value; }
    string IAgentRecruitingEvidenceView.CreatePurpose { get => createPurpose; set => createPurpose = value; }
    string IAgentRecruitingEvidenceView.LoadError => loadError;
    AttemptDraft IAgentRecruitingEvidenceView.AttemptDraft => attemptDraft;
    bool IAgentRecruitingEvidenceView.IsLoading => isLoading;
    bool IAgentRecruitingEvidenceView.IsSaving => isSaving;
    bool IAgentRecruitingEvidenceView.IsCreateDialogOpen => isCreateDialogOpen;
    bool IAgentRecruitingEvidenceView.IsAttachDialogOpen => isAttachDialogOpen;
    Guid? IAgentRecruitingEvidenceView.CandidateAgentId => CandidateAgentId;
    EventCallback<RecruitmentTrainingRequest> IAgentRecruitingEvidenceView.RequestTraining => RequestTraining;
    int IAgentRecruitingEvidenceView.AttemptCount => AttemptCount;
    AgentRecruitingAssessmentAnalysis? IAgentRecruitingEvidenceView.LatestAnalysis => LatestAnalysis;
    Task IAgentRecruitingEvidenceView.ReloadAsync() => ReloadAsync();
    Task IAgentRecruitingEvidenceView.OpenCreateDialog() => OpenCreateDialog();
    Task IAgentRecruitingEvidenceView.CloseCreateDialog() => CloseCreateDialog();
    Task IAgentRecruitingEvidenceView.OpenRecheckDialog(AgentRecruitingInterview interview, AgentRecruitingAttempt attempt) => OpenRecheckDialog(interview, attempt);
    Task IAgentRecruitingEvidenceView.CreateAssessmentAsync() => CreateAssessmentAsync();
    Task IAgentRecruitingEvidenceView.OpenAttachDialog() => OpenAttachDialog();
    Task IAgentRecruitingEvidenceView.CloseAttachDialog() => CloseAttachDialog();
    Task IAgentRecruitingEvidenceView.AttachResultAsync() => AttachResultAsync();
    string IAgentRecruitingEvidenceView.FormatLatestScore() => FormatLatestScore();
    string IAgentRecruitingEvidenceView.FormatLatestNextStep() => FormatLatestNextStep();
}
