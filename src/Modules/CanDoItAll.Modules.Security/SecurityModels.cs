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

public sealed record SecretBindingCreateRequest(
    Guid SecretId,
    string ConsumerType,
    string ConsumerId,
    string Purpose);

public sealed record SecretBindingSummary(
    Guid Id,
    Guid SecretId,
    string NameSnapshot,
    SecretKind Kind,
    string Scope,
    string ConsumerType,
    string ConsumerId,
    string Purpose);

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
    ISecretVault vault,
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

    public async Task<Result<Guid>> BindSecretAsync(
        SecretBindingCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateBindingRequest(request);
        if (validationError is not null)
        {
            return Result<Guid>.Failure(validationError);
        }

        var consumerType = request.ConsumerType.Trim();
        var consumerId = request.ConsumerId.Trim();
        var purpose = request.Purpose.Trim();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var secretExists = await dbContext.Set<SecretRecord>()
            .AnyAsync(secret => secret.Id == request.SecretId, cancellationToken);
        if (!secretExists)
        {
            return Result<Guid>.Failure(Error.Validation("Secret record was not found.", "secret.binding.secret-not-found"));
        }

        var existing = await dbContext.Set<SecretReference>()
            .FirstOrDefaultAsync(reference =>
                reference.SecretRecordId == request.SecretId &&
                reference.ContextType == consumerType &&
                reference.ContextId == consumerId &&
                reference.Purpose == purpose,
                cancellationToken);
        if (existing is not null)
        {
            return Result<Guid>.Success(existing.Id);
        }

        var reference = new SecretReference
        {
            Id = Guid.NewGuid(),
            SecretRecordId = request.SecretId,
            ContextType = consumerType,
            ContextId = consumerId,
            Purpose = purpose
        };
        await dbContext.Set<SecretReference>().AddAsync(reference, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "security",
            "bind-secret",
            "Bound secret reference",
            $"Consumer: {consumerType}/{consumerId}. Purpose: {purpose}.",
            ArtifactKind: "secret-reference",
            ArtifactId: reference.Id,
            Route: "/settings"), cancellationToken);

        return Result<Guid>.Success(reference.Id);
    }

    public async Task<IReadOnlyList<SecretBindingSummary>> ListBindingsAsync(
        string consumerType,
        string consumerId,
        string? purpose = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedConsumerType = NormalizeBindingSegment(consumerType, nameof(consumerType));
        var normalizedConsumerId = NormalizeBindingSegment(consumerId, nameof(consumerId));
        var normalizedPurpose = string.IsNullOrWhiteSpace(purpose)
            ? null
            : NormalizeBindingSegment(purpose, nameof(purpose));
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query =
            from reference in dbContext.Set<SecretReference>()
            join secret in dbContext.Set<SecretRecord>() on reference.SecretRecordId equals secret.Id
            where reference.ContextType == normalizedConsumerType &&
                  reference.ContextId == normalizedConsumerId
            select new { reference, secret };

        if (normalizedPurpose is not null)
        {
            query = query.Where(item => item.reference.Purpose == normalizedPurpose);
        }

        return await query
            .OrderBy(item => item.secret.Name)
            .ThenBy(item => item.reference.Purpose)
            .Select(item => new SecretBindingSummary(
                item.reference.Id,
                item.secret.Id,
                item.secret.Name,
                item.secret.Kind,
                item.secret.Scope,
                item.reference.ContextType,
                item.reference.ContextId,
                item.reference.Purpose))
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
            SecretValue = await ResolveSecretValueAsync(secret, cancellationToken),
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
                Id = model.Id is { } requestedId && requestedId != Guid.Empty
                    ? requestedId
                    : Guid.NewGuid(),
                CreatedAtUtc = clock.GetUtcNow()
            };

            await dbContext.Set<SecretRecord>().AddAsync(entity, cancellationToken);
        }

        var oldVaultKey = SecretVaultRecordReference.TryParse(entity.EncryptedPayload, out var existingVaultKey)
            ? existingVaultKey
            : null;
        var newVaultKey = SecretVaultRecordReference.BuildKey(entity.Id, Guid.NewGuid());
        await vault.SetAsync(newVaultKey, model.SecretValue, cancellationToken);

        entity.Name = model.Name.Trim();
        entity.Kind = model.Kind;
        entity.Scope = string.IsNullOrWhiteSpace(model.Scope) ? "workspace" : model.Scope.Trim();
        entity.RotationNote = model.RotationNote?.Trim();
        entity.MetadataJson = string.IsNullOrWhiteSpace(model.MetadataJson) ? "{}" : model.MetadataJson;
        entity.EncryptedPayload = SecretVaultRecordReference.Create(newVaultKey);
        entity.UpdatedAtUtc = clock.GetUtcNow();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception saveException) when (saveException is not OperationCanceledException)
        {
            await DeleteStagedVaultPayloadAsync(newVaultKey, saveException, cancellationToken);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldVaultKey) &&
            !string.Equals(oldVaultKey, newVaultKey, StringComparison.Ordinal))
        {
            await vault.DeleteAsync(oldVaultKey, cancellationToken);
        }

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

        var vaultKey = SecretVaultRecordReference.TryParse(entity.EncryptedPayload, out var parsedVaultKey)
            ? parsedVaultKey
            : null;

        dbContext.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(vaultKey))
        {
            await vault.DeleteAsync(vaultKey, cancellationToken);
        }

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

    private static Error? ValidateBindingRequest(SecretBindingCreateRequest request)
    {
        if (request.SecretId == Guid.Empty)
        {
            return Error.Validation("Secret id is required.", "secret.binding.secret-required");
        }

        try
        {
            _ = NormalizeBindingSegment(request.ConsumerType, nameof(request.ConsumerType));
            _ = NormalizeBindingSegment(request.ConsumerId, nameof(request.ConsumerId));
            _ = NormalizeBindingSegment(request.Purpose, nameof(request.Purpose));
        }
        catch (ArgumentException exception)
        {
            return Error.Validation(exception.Message, "secret.binding.invalid");
        }

        return null;
    }

    private static string NormalizeBindingSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Secret binding value is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Contains('\r', StringComparison.Ordinal) ||
            normalized.Contains('\n', StringComparison.Ordinal))
        {
            throw new ArgumentException("Secret binding values cannot contain line breaks.", parameterName);
        }

        return normalized;
    }

    private async Task<string> ResolveSecretValueAsync(
        SecretRecord secret,
        CancellationToken cancellationToken)
    {
        try
        {
            if (SecretVaultRecordReference.TryParse(secret.EncryptedPayload, out var vaultKey))
            {
                return await vault.GetAsync(vaultKey, cancellationToken)
                    ?? throw new InvalidOperationException("The referenced vault payload was not found.");
            }

            return protector.Unprotect(secret.EncryptedPayload);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Secret '{secret.Name}' ({secret.Id:D}) could not be opened for editing.",
                exception);
        }
    }

    private async Task DeleteStagedVaultPayloadAsync(
        string vaultKey,
        Exception saveException,
        CancellationToken cancellationToken)
    {
        try
        {
            await vault.DeleteAsync(vaultKey, cancellationToken);
        }
        catch (Exception cleanupException) when (cleanupException is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "Secret metadata save failed and staged vault payload cleanup also failed.",
                new AggregateException(saveException, cleanupException));
        }
    }
}
