using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Security;

public enum SecretKind
{
    ApiKey,
    Password,
    Token,
    ConnectionString,
    SshKey,
    Generic
}

public sealed class SecretRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public SecretKind Kind { get; set; } = SecretKind.Generic;

    public string EncryptedPayload { get; set; } = string.Empty;

    public string Scope { get; set; } = "workspace";

    public string MetadataJson { get; set; } = "{}";

    public string? RotationNote { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class SecretReference
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SecretRecordId { get; set; }

    public string ContextType { get; set; } = string.Empty;

    public string ContextId { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;
}

internal sealed class SecretRecordConfiguration : IEntityTypeConfiguration<SecretRecord>
{
    public void Configure(EntityTypeBuilder<SecretRecord> builder)
    {
        builder.ToTable("Security_SecretRecords");
        builder.HasKey(secret => secret.Id);
        builder.Property(secret => secret.Name).HasMaxLength(200).IsRequired();
        builder.Property(secret => secret.Scope).HasMaxLength(50).IsRequired();
        builder.Property(secret => secret.MetadataJson).HasColumnType("TEXT");
        builder.Property(secret => secret.EncryptedPayload).HasColumnType("TEXT");
    }
}

internal sealed class SecretReferenceConfiguration : IEntityTypeConfiguration<SecretReference>
{
    public void Configure(EntityTypeBuilder<SecretReference> builder)
    {
        builder.ToTable("Security_SecretReferences");
        builder.HasKey(reference => reference.Id);
        builder.Property(reference => reference.ContextType).HasMaxLength(80).IsRequired();
        builder.Property(reference => reference.ContextId).HasMaxLength(120).IsRequired();
        builder.Property(reference => reference.Purpose).HasMaxLength(120).IsRequired();
        builder.HasIndex(reference => new { reference.ContextType, reference.ContextId });
    }
}

public sealed record SecretListItem(Guid Id, string Name, SecretKind Kind, string Scope, DateTimeOffset UpdatedAtUtc);

public sealed class SecretEditorModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public SecretKind Kind { get; set; } = SecretKind.Generic;

    public string SecretValue { get; set; } = string.Empty;

    public string Scope { get; set; } = "workspace";

    public string? RotationNote { get; set; }

    public string MetadataJson { get; set; } = "{}";
}

public interface ISecretProtector
{
    string Protect(string plainText);

    string Unprotect(string protectedValue);
}

public sealed class DataProtectionSecretProtector(IDataProtectionProvider dataProtectionProvider) : ISecretProtector
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("CanDoItAll.Secrets");

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}

public sealed class SecretService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ISecretProtector protector,
    IClock clock,
    IActivityStream activityStream)
{
    public async Task<IReadOnlyList<SecretListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<SecretRecord>()
            .OrderBy(secret => secret.Name)
            .Select(secret => new SecretListItem(secret.Id, secret.Name, secret.Kind, secret.Scope, secret.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<SecretEditorModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var secret = await dbContext.Set<SecretRecord>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (secret is null)
        {
            return null;
        }

        return new SecretEditorModel
        {
            Id = secret.Id,
            Name = secret.Name,
            Kind = secret.Kind,
            SecretValue = protector.Unprotect(secret.EncryptedPayload),
            Scope = secret.Scope,
            RotationNote = secret.RotationNote,
            MetadataJson = secret.MetadataJson
        };
    }

    public async Task<Result<Guid>> SaveAsync(SecretEditorModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Result<Guid>.Failure(Error.Validation("Secret name is required."));
        }

        if (string.IsNullOrWhiteSpace(model.SecretValue))
        {
            return Result<Guid>.Failure(Error.Validation("Secret value is required."));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = model.Id.HasValue
            ? await dbContext.Set<SecretRecord>().FirstOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;

        if (entity is null)
        {
            entity = new SecretRecord
            {
                CreatedAtUtc = clock.GetUtcNow()
            };

            await dbContext.Set<SecretRecord>().AddAsync(entity, cancellationToken);
        }

        entity.Name = model.Name.Trim();
        entity.Kind = model.Kind;
        entity.Scope = string.IsNullOrWhiteSpace(model.Scope) ? "workspace" : model.Scope.Trim();
        entity.RotationNote = model.RotationNote?.Trim();
        entity.MetadataJson = string.IsNullOrWhiteSpace(model.MetadataJson) ? "{}" : model.MetadataJson;
        entity.EncryptedPayload = protector.Protect(model.SecretValue);
        entity.UpdatedAtUtc = clock.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "security",
            model.Id.HasValue ? "update-secret" : "create-secret",
            $"{(model.Id.HasValue ? "Updated" : "Created")} secret record",
            entity.Name,
            ArtifactKind: "secret",
            ArtifactId: entity.Id,
            Route: "/settings"), cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<SecretRecord>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        dbContext.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "security",
            "delete-secret",
            "Deleted secret record",
            entity.Name,
            ArtifactKind: "secret",
            ArtifactId: entity.Id,
            Route: "/settings"), cancellationToken);
    }

    public async Task<IReadOnlyList<SecretListItem>> ListForPickerAsync(CancellationToken cancellationToken = default)
        => await ListAsync(cancellationToken);
}
