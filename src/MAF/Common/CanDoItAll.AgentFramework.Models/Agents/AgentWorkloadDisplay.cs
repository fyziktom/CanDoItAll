namespace CanDoItAll.AgentFramework.Models;

public static class AgentWorkloadDisplay
{
    public static string ResolveLabel(AgentWorkloadKind workload)
    {
        return workload switch
        {
            AgentWorkloadKind.Hr => "HR",
            AgentWorkloadKind.Qa => "QA",
            _ => workload.ToString()
        };
    }
}
