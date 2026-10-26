using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;

namespace pengdows.efcore.adonet.Scaffolding;

/// <summary>
///     Reads a database's schema through the dialect's <see cref="IAdoNetDialect.SchemaReader" />, opening the connection for
///     the read when it is closed.
/// </summary>
public class AdoNetDatabaseModelFactory(IAdoNetDialect dialect) : DatabaseModelFactory
{
    // A connection string alone doesn't say which driver to open it with.
    public override DatabaseModel Create(string connectionString, DatabaseModelFactoryOptions options)
        => throw new NotSupportedException("Read a schema from a DbConnection: a connection string doesn't name its driver.");

    public override DatabaseModel Create(DbConnection connection, DatabaseModelFactoryOptions options)
    {
        var reader = dialect.SchemaReader
            ?? throw new NotSupportedException($"Dialect '{dialect.Name}' can't read a database's schema.");

        var opened = connection.State != ConnectionState.Open;
        if (opened)
        {
            connection.Open();
        }

        try
        {
            // The reader's SQL is written for the dialect's session mode, which a connection opened elsewhere may not be in.
            if (dialect.Sql.SessionSetup is { } setup)
            {
                using var command = connection.CreateCommand();
                command.CommandText = setup;
                command.ExecuteNonQuery();
            }

            return reader.Read(connection, options, dialect.Sql);
        }
        finally
        {
            if (opened)
            {
                connection.Close();
            }
        }
    }
}
