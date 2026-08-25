using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CanDoItAll.Modules.Workspace;

internal sealed class SharedProviderInvocationRecordConfiguration
    : IEntityTypeConfiguration<SharedProviderInvocationRecord>
{
    public void Configure(EntityTypeBuilder<SharedProviderInvocationRecord> builder)
    {
        var accessContextConverter = new ValueConverter<AccessContextReference, string>(
            reference => reference.Value,
            value => new(value));

        builder.ToTable(
            "Workspace_SharedProviderInvocations",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_Workspace_SharedProviderInvocations_Completion",
                    "(\"Outcome\" = 'InProgress' AND \"CompletedAtUtc\" IS NULL AND \"DurationMilliseconds\" IS NULL) OR " +
                    "(\"Outcome\" <> 'InProgress' AND \"CompletedAtUtc\" IS NOT NULL AND \"DurationMilliseconds\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_Workspace_SharedProviderInvocations_Usage",
                    "(\"InputTokenCount\" IS NULL OR \"InputTokenCount\" >= 0) AND " +
                    "(\"OutputTokenCount\" IS NULL OR \"OutputTokenCount\" >= 0) AND " +
                    $"(\"ImageCount\" IS NULL OR \"ImageCount\" BETWEEN 1 AND {SharedProviderRelaySupportDescriptor.MaximumAllowedImageCount}) AND " +
                    "((\"UsageCompleteness\" = 'Unavailable' AND \"InputTokenCount\" IS NULL AND \"OutputTokenCount\" IS NULL AND \"ImageCount\" IS NULL AND \"Operation\" IN ('ChatCompletions', 'Responses', 'ImageGenerations')) OR " +
                    "(\"Operation\" IN ('ChatCompletions', 'Responses') AND \"ImageCount\" IS NULL AND ((\"UsageCompleteness\" = 'Partial' AND ((\"InputTokenCount\" IS NOT NULL AND \"OutputTokenCount\" IS NULL) OR (\"InputTokenCount\" IS NULL AND \"OutputTokenCount\" IS NOT NULL))) OR (\"UsageCompleteness\" = 'Complete' AND \"InputTokenCount\" IS NOT NULL AND \"OutputTokenCount\" IS NOT NULL))) OR " +
                    "(\"Operation\" = 'ImageGenerations' AND \"UsageCompleteness\" = 'Complete' AND \"InputTokenCount\" IS NULL AND \"OutputTokenCount\" IS NULL AND \"ImageCount\" IS NOT NULL))");
            });
        builder.HasKey(invocation => invocation.Id);
        builder.Property(invocation => invocation.RequestId).HasMaxLength(128).IsRequired();
        builder.Property(invocation => invocation.PublicationId)
            .HasConversion(
                publicationId => publicationId.Value,
                value => new(value));
        builder.Property(invocation => invocation.AuthenticatedSubject)
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(invocation => invocation.AccessContextReference)
            .HasConversion(accessContextConverter)
            .HasMaxLength(AccessContextReference.MaximumLength);
        builder.Property(invocation => invocation.TraceId).HasMaxLength(128).IsRequired();
        builder.Property(invocation => invocation.CorrelationId).HasMaxLength(128).IsRequired();
        builder.Property(invocation => invocation.Operation)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(invocation => invocation.PublicModelId)
            .HasConversion(
                modelId => modelId.Value,
                value => SharedProviderRoutingModelIdCodec.Parse(value))
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(invocation => invocation.UpstreamModelId)
            .HasMaxLength(SharedProviderRoutingModelIdCodec.MaximumUpstreamModelIdLength)
            .IsRequired();
        builder.Property(invocation => invocation.Outcome)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(invocation => invocation.FailureCategory)
            .HasConversion<string>()
            .HasMaxLength(64);
        builder.Property(invocation => invocation.ImageCount);
        builder.Property(invocation => invocation.UsageCompleteness)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(invocation => invocation.Price).HasPrecision(28, 12);
        builder.Property(invocation => invocation.PricingCompleteness)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(invocation => invocation.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(invocation => invocation.RequestId).IsUnique();
        builder.HasIndex(invocation => new
        {
            invocation.PublicationId,
            invocation.StartedAtUtc
        });
        builder.HasIndex(invocation => new
        {
            invocation.DeleteAfterUtc,
            invocation.CompletedAtUtc
        });
        builder.HasOne<ProviderSharePublication>()
            .WithMany()
            .HasForeignKey(invocation => new
            {
                invocation.PublicationId,
                invocation.ProviderProfileId
            })
            .HasPrincipalKey(publication => new
            {
                publication.PublicId,
                publication.ProviderProfileId
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
