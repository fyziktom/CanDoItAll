using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectTransferTargetResidue(
    ProjectTransferTargetStateArea Area,
    string Description);

public sealed class ProjectTransferTargetStateGuard
{
    private static readonly ProjectTransferTargetStateArea[] RequiredAreas =
        Enum.GetValues<ProjectTransferTargetStateArea>();
    private readonly IReadOnlyList<IProjectTransferTargetStateParticipant>
        participants;

    public ProjectTransferTargetStateGuard(
        IEnumerable<IProjectTransferTargetStateParticipant> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);

        var supplied = participants.ToArray();
        var duplicateAreas = supplied
            .GroupBy(participant => participant.Area)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order()
            .ToArray();
        if (duplicateAreas.Length > 0)
        {
            throw new InvalidOperationException(
                $"Project transfer target-state participants are duplicated for: {string.Join(", ", duplicateAreas)}.");
        }

        var participantsByArea = supplied.ToDictionary(
            participant => participant.Area);
        var missingAreas = RequiredAreas
            .Where(area => !participantsByArea.ContainsKey(area))
            .ToArray();
        if (missingAreas.Length > 0)
        {
            throw new InvalidOperationException(
                $"Project transfer target-state participants are missing for: {string.Join(", ", missingAreas)}.");
        }

        this.participants = RequiredAreas
            .Select(area => participantsByArea[area])
            .ToArray();
    }

    internal async Task<IReadOnlyList<ProjectTransferTargetResidue>>
        FindResiduesAsync(
            AppDbContext dbContext,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var residues = new List<ProjectTransferTargetResidue>();
        foreach (var participant in participants)
        {
            var participantResidues = await participant.FindResiduesAsync(
                dbContext,
                cancellationToken);
            residues.AddRange(participantResidues.Select(residue =>
                new ProjectTransferTargetResidue(
                    participant.Area,
                    residue.Description)));
        }

        return residues;
    }

    internal async Task AcquireExclusiveImportLocksAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (!string.Equals(
                dbContext.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Project import exclusion locks require PostgreSQL; provider '{dbContext.Database.ProviderName ?? "unknown"}' is not supported.");
        }

        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Project import exclusion locks require an active database transaction.");
        }

        var tableNames = ResolveExclusiveImportTableNames(dbContext);
        var command = $"LOCK TABLE\n    {string.Join(",\n    ", tableNames)}\nIN ACCESS EXCLUSIVE MODE";
        await dbContext.Database.ExecuteSqlRawAsync(command, cancellationToken);
    }

    internal IReadOnlyList<string> ResolveExclusiveImportTableNames(
        AppDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        return participants
            .SelectMany(participant => participant.EntityTypesToLock.Select(entityType =>
                ResolveTableName(dbContext, participant.Area, entityType)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    internal static string Describe(
        IReadOnlyCollection<ProjectTransferTargetResidue> residues)
        => string.Join(
            ", ",
            residues
                .Select(residue => residue.Description)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));

    private static string ResolveTableName(
        AppDbContext dbContext,
        ProjectTransferTargetStateArea area,
        Type entityType)
    {
        var metadata = dbContext.Model.FindEntityType(entityType) ??
            throw new InvalidOperationException(
                $"Project transfer target-state participant '{area}' declared unmapped entity type '{entityType.FullName}'.");
        var tableName = metadata.GetTableName() ??
            throw new InvalidOperationException(
                $"Project transfer target-state participant '{area}' declared entity type '{entityType.FullName}' without a table mapping.");
        var schema = metadata.GetSchema();
        return string.IsNullOrWhiteSpace(schema)
            ? QuoteIdentifier(tableName)
            : $"{QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)}";
    }

    private static string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
