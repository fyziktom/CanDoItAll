using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.LlmChats.Ui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.Conversations;

public sealed class ConversationShellRegistrationTests
{
    private const string LauncherTypeName =
        "CanDoItAll.Conversations.Shell.IConversationShellLauncher";
    private const string CoordinatorTypeName =
        "CanDoItAll.Conversations.Shell.IConversationShellCoordinator";

    [Fact]
    public void Agent_module_registers_its_required_conversation_shell_services()
    {
        var services = new ServiceCollection();

        services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());

        Assert.Contains(services, item => item.ServiceType.FullName == LauncherTypeName);
        Assert.Contains(services, item => item.ServiceType.FullName == CoordinatorTypeName);
    }

    [Fact]
    public void Llm_chats_ui_registers_its_required_conversation_shell_services()
    {
        var services = new ServiceCollection();

        services.AddLlmChatsUi();

        Assert.Contains(services, item => item.ServiceType.FullName == LauncherTypeName);
        Assert.Contains(services, item => item.ServiceType.FullName == CoordinatorTypeName);
    }
}
