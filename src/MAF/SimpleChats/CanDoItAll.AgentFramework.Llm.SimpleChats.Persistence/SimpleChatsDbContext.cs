using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.EntityConfigurations;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;

public sealed class SimpleChatsDbContext(DbContextOptions<SimpleChatsDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new LlmChatDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new LlmChatDefinitionRevisionConfiguration());
        modelBuilder.ApplyConfiguration(new LlmChatDefinitionTagConfiguration());
        modelBuilder.ApplyConfiguration(new LlmChatConversationConfiguration());
        modelBuilder.ApplyConfiguration(new LlmChatTranscriptConfiguration());
        modelBuilder.ApplyConfiguration(new LlmChatMessageConfiguration());
        modelBuilder.ApplyConfiguration(new LlmChatOperationConfiguration());
        modelBuilder.ApplyConfiguration(new LlmChatInvocationRecordConfiguration());
        modelBuilder.ApplyConfiguration(new LlmChatOperationEventConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
