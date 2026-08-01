using System.Data;
using System.Data.Common;
using System.Globalization;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed class PostgreSqlLegacyCognitiveMemoryDataReader(DbConnection connection) : ILegacyCognitiveMemoryDataReader
{
    private const string TableNameParameter = "@tableName";
    private const string LegacyTableNamePatternParameter = "@legacyTableNamePattern";
    private const string LegacyTableNamePattern = @"CognitiveMemory\_%";

    public async Task<IReadOnlyList<LegacyCognitiveMemoryTableSnapshot>> ReadLegacyTablesAsync(
        CancellationToken cancellationToken = default) {
        if (!IsPostgreSqlConnection(connection)) {
            throw new InvalidOperationException(
                $"Legacy Cognitive Memory export supports PostgreSQL connections only. Provider={connection.GetType().FullName}.");
        }

        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) {
            await connection.OpenAsync(cancellationToken);
        }

        try {
            var tableNames = await ReadTableNamesAsync(cancellationToken);
            var tables = new List<LegacyCognitiveMemoryTableSnapshot>(tableNames.Count);
            foreach (var tableName in tableNames) {
                if (!LegacyCognitiveMemoryExportConstants.IsLegacyTableName(tableName)) {
                    throw new InvalidOperationException(
                        $"Refusing to read invalid legacy Cognitive Memory table name '{tableName}'.");
                }

                var columnNames = await ReadColumnNamesAsync(tableName, cancellationToken);
                var rows = await ReadRowsAsync(tableName, columnNames, cancellationToken);
                tables.Add(new LegacyCognitiveMemoryTableSnapshot(tableName, columnNames, rows));
            }

            return tables;
        }
        finally {
            if (shouldClose) {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<IReadOnlyList<string>> ReadTableNamesAsync(CancellationToken cancellationToken) {
        await using var command = CreateCommand(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = current_schema()
              AND table_type = 'BASE TABLE'
              AND table_name LIKE @legacyTableNamePattern ESCAPE '\'
            ORDER BY table_name;
            """,
            (LegacyTableNamePatternParameter, LegacyTableNamePattern));

        return await ReadStringListAsync(command, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> ReadColumnNamesAsync(
        string tableName,
        CancellationToken cancellationToken) {
        await using var command = CreateCommand(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = @tableName
            ORDER BY ordinal_position;
            """,
            (TableNameParameter, tableName));

        return await ReadStringListAsync(command, cancellationToken);
    }

    private async Task<IReadOnlyList<LegacyCognitiveMemoryRow>> ReadRowsAsync(
        string tableName,
        IReadOnlyList<string> columnNames,
        CancellationToken cancellationToken) {
        if (columnNames.Count == 0) {
            return [];
        }

        var commandText = BuildSelectCommand(tableName, columnNames);
        await using var command = CreateCommand(commandText);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<LegacyCognitiveMemoryRow>();
        while (await reader.ReadAsync(cancellationToken)) {
            var values = new SortedDictionary<string, string?>(StringComparer.Ordinal);
            for (var index = 0; index < columnNames.Count; index++) {
                values[columnNames[index]] = await ReadValueAsync(reader, index, cancellationToken);
            }

            rows.Add(new LegacyCognitiveMemoryRow(values));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<string>> ReadStringListAsync(
        DbCommand command,
        CancellationToken cancellationToken) {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var values = new List<string>();
        while (await reader.ReadAsync(cancellationToken)) {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private static async Task<string?> ReadValueAsync(
        DbDataReader reader,
        int ordinal,
        CancellationToken cancellationToken) {
        if (await reader.IsDBNullAsync(ordinal, cancellationToken)) {
            return null;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            byte[] bytes => Convert.ToBase64String(bytes),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    private static string BuildSelectCommand(
        string tableName,
        IReadOnlyList<string> columnNames) {
        var columns = string.Join(", ", columnNames.Select(QuoteIdentifier));
        var orderBy = columnNames.Contains("Id", StringComparer.Ordinal)
            ? QuoteIdentifier("Id")
            : "ctid";

        return $"SELECT {columns} FROM {QuoteIdentifier(tableName)} ORDER BY {orderBy};";
    }

    private DbCommand CreateCommand(
        string commandText,
        params (string Name, object? Value)[] parameters) {
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        foreach (var (name, value) in parameters) {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static string QuoteIdentifier(string identifier) {
        if (!identifier.All(character => char.IsAsciiLetterOrDigit(character) || character == '_')) {
            throw new InvalidOperationException($"Refusing to quote unsafe PostgreSQL identifier '{identifier}'.");
        }

        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static bool IsPostgreSqlConnection(DbConnection dbConnection)
        => dbConnection.GetType().FullName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
}
