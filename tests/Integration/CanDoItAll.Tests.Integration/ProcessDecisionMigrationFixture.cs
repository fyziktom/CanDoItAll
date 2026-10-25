using CanDoItAll.Processes.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Tests.Integration.Processes;

internal static class ProcessDecisionMigrationFixture {
    internal static async Task ApplyAsync(ProcessPersistenceDbContext context, bool down, params Migration[] migrations) {
        var commands = (down ? migrations.Reverse() : migrations).SelectMany(migration =>
            context.GetService<IMigrationsSqlGenerator>().Generate(down ? migration.DownOperations : migration.UpOperations, context.Model));
        await using var transaction = await context.Database.BeginTransactionAsync();
        foreach (var command in commands) {
            await using var sql = context.Database.GetDbConnection().CreateCommand();
            sql.Transaction = transaction.GetDbTransaction();
            sql.CommandText = command.CommandText;
            await sql.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }
}
