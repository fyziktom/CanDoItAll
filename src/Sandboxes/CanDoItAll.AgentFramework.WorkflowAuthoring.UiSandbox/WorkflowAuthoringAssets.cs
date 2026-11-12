namespace CanDoItAll.AgentFramework.WorkflowAuthoring.UiSandbox;

public enum WorkflowAuthoringAssetMode { Parity, Fast }

public static class WorkflowAuthoringAssets {
#if WORKFLOW_AUTHORING_FAST_ASSETS
    public const WorkflowAuthoringAssetMode Mode = WorkflowAuthoringAssetMode.Fast;
    public const string ThemePath = "css/workflow-authoring-fast.css";
#else
    public const WorkflowAuthoringAssetMode Mode = WorkflowAuthoringAssetMode.Parity;
    public const string ThemePath = "css/output.css";
#endif
}
