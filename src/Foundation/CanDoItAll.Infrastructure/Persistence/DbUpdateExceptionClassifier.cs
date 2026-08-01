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
            PostgresException postgresException => postgresException.SqlState == PostgresErrorCodes.UniqueViolation,
            _ => false
        };
    }

    public static string? GetConstraintName(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.InnerException switch
        {
            PostgresException postgresException => postgresException.ConstraintName,
            _ => null
        };
    }

    public static string GetProviderMessage(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.InnerException?.Message ?? exception.Message;
    }
}
