using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Collaboration;

public enum CollaborationInboxItemKind
{
    Notification = 0,
    Escalation = 1
}

public enum CollaborationContextKind
{
    Manual = 0,
    ProcessRun = 1,
    ProcessLaunch = 2,
    AutomationSignal = 3
}

public enum CollaborationMessageAuthorKind
{
    User = 0,
    Agent = 1,
    Role = 2,
    System = 3
}

public enum CollaborationMessageKind
{
    Standard = 0,
    Escalation = 1,
    System = 2
}

public enum CollaborationParticipantKind
{
    User = 0,
    Agent = 1,
    Role = 2,
    System = 3
}

public enum CollaborationThreadState
{
    Open = 0,
    Closed = 1
}

public sealed class CollaborationThreadRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Subject { get; set; } = string.Empty;

    public CollaborationContextKind ContextKind { get; set; } = CollaborationContextKind.Manual;

    public Guid? ContextId { get; set; }

    public Guid? ProjectId { get; set; }

    public string ContextLabel { get; set; } = string.Empty;

    public string? ContextRoute { get; set; }

    public CollaborationInboxItemKind PrimaryItemKind { get; set; } = CollaborationInboxItemKind.Notification;

    public CollaborationThreadState State { get; set; } = CollaborationThreadState.Open;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset LastActivityAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CollaborationParticipantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ThreadId { get; set; }

    public CollaborationParticipantKind ParticipantKind { get; set; }

    public string ParticipantKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string RoleLabel { get; set; } = string.Empty;

    public DateTimeOffset AddedAtUtc { get; set; }
}

public sealed class CollaborationMessageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ThreadId { get; set; }

    public CollaborationMessageKind Kind { get; set; } = CollaborationMessageKind.Standard;

    public CollaborationMessageAuthorKind AuthorKind { get; set; } = CollaborationMessageAuthorKind.User;

    public string AuthorKey { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public bool RaisesEscalation { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CollaborationInboxItemRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ThreadId { get; set; }

    public CollaborationInboxItemKind ItemKind { get; set; } = CollaborationInboxItemKind.Notification;

    public string Title { get; set; } = string.Empty;

    public string PreviewText { get; set; } = string.Empty;

    public string Route { get; set; } = string.Empty;

    public bool IsUnread { get; set; } = true;

    public int UnreadCount { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

internal sealed class CollaborationThreadRecordConfiguration : IEntityTypeConfiguration<CollaborationThreadRecord>
{
    public void Configure(EntityTypeBuilder<CollaborationThreadRecord> builder)
    {
        builder.ToTable("Collaboration_Threads");
        builder.HasKey(thread => thread.Id);
        builder.Property(thread => thread.Subject).HasMaxLength(240).IsRequired();
        builder.Property(thread => thread.ContextKind).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(thread => thread.ContextLabel).HasMaxLength(200).IsRequired();
        builder.Property(thread => thread.ContextRoute).HasMaxLength(500);
        builder.Property(thread => thread.PrimaryItemKind).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(thread => thread.State).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.HasIndex(thread => thread.ProjectId);
        builder.HasIndex(thread => new { thread.ContextKind, thread.ContextId });
        builder.HasIndex(thread => thread.LastActivityAtUtc);
    }
}

internal sealed class CollaborationParticipantRecordConfiguration : IEntityTypeConfiguration<CollaborationParticipantRecord>
{
    public void Configure(EntityTypeBuilder<CollaborationParticipantRecord> builder)
    {
        builder.ToTable("Collaboration_Participants");
        builder.HasKey(participant => participant.Id);
        builder.Property(participant => participant.ParticipantKind).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(participant => participant.ParticipantKey).HasMaxLength(200).IsRequired();
        builder.Property(participant => participant.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(participant => participant.RoleLabel).HasMaxLength(120);
        builder.HasIndex(participant => new { participant.ThreadId, participant.ParticipantKey }).IsUnique();
    }
}

internal sealed class CollaborationMessageRecordConfiguration : IEntityTypeConfiguration<CollaborationMessageRecord>
{
    public void Configure(EntityTypeBuilder<CollaborationMessageRecord> builder)
    {
        builder.ToTable("Collaboration_Messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Kind).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(message => message.AuthorKind).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(message => message.AuthorKey).HasMaxLength(200).IsRequired();
        builder.Property(message => message.AuthorName).HasMaxLength(160).IsRequired();
        builder.Property(message => message.Body).HasColumnType("TEXT");
        builder.HasIndex(message => new { message.ThreadId, message.CreatedAtUtc });
    }
}

internal sealed class CollaborationInboxItemRecordConfiguration : IEntityTypeConfiguration<CollaborationInboxItemRecord>
{
    public void Configure(EntityTypeBuilder<CollaborationInboxItemRecord> builder)
    {
        builder.ToTable("Collaboration_InboxItems");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ItemKind).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(item => item.Title).HasMaxLength(240).IsRequired();
        builder.Property(item => item.PreviewText).HasMaxLength(320).IsRequired();
        builder.Property(item => item.Route).HasMaxLength(500).IsRequired();
        builder.HasIndex(item => item.ThreadId).IsUnique();
        builder.HasIndex(item => new { item.ItemKind, item.IsUnread });
        builder.HasIndex(item => item.UpdatedAtUtc);
    }
}
