using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CanDoItAll.Infrastructure.Persistence;

public static class DbUpdateExceptionClassifier
{
    public static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.InnerException switch
        {
            SqliteException sqliteException => sqliteException.SqliteErrorCode == 19,
            PostgresException postgresException => postgresException.SqlState == PostgresErrorCodes.UniqueViolation,
            _ => false
        };
    }
}
