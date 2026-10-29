using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;

namespace pengdows.efcore.adonet.Scaffolding;

/// <summary>
///     Reads a database's schema through the dialect's <see cref="IAdoNetDialect.SchemaReader" />, opening the connection for
///     the read when it is closed.
/// </summary>
public class AdoNetDatabaseModelFactory : DatabaseModelFactory
{
    private readonly IAdoNetDialect _dialect;
    private readonly DbProviderFactory? _driverFactory;

    public AdoNetDatabaseModelFactory(IAdoNetDialect dialect)
        : this(dialect, null)
    {
    }

    /// <summary>
    ///     With the driver a plugin's design-time services name (<see cref="Design.AdoNetDesignTimeServices" />), so a
    ///     connection string can be read too, as EF's tools scaffold (dotnet ef dbcontext scaffold).
    /// </summary>
    public AdoNetDatabaseModelFactory(IAdoNetDialect dialect, DbProviderFactory? driverFactory)
    {
        _dialect = dialect;
        _driverFactory = driverFactory;
    }

    /// <summary>
    ///     The dialect that reads <paramref name="connection" />'s schema, asked once it is open: the configured one, unless a
    ///     plugin tells servers apart from the connection (a design-time context has no options to choose one).
    /// </summary>
    protected virtual IAdoNetDialect DialectFor(DbConnection connection)
        => _dialect;

    // A connection string alone doesn't say which driver to open it with: the plugin's design-time services do, or it is refused.
    public override DatabaseModel Create(string connectionString, DatabaseModelFactoryOptions options)
    {
        if (_driverFactory == null)
        {
            throw new NotSupportedException("Read a schema from a DbConnection: a connection string doesn't name its driver.");
        }

        using var connection = _driverFactory.CreateConnection()
            ?? throw new InvalidOperationException($"The driver's {_driverFactory.GetType().Name} creates no connection.");
        connection.ConnectionString = connectionString;
        return Create(connection, options);
    }

    public override DatabaseModel Create(DbConnection connection, DatabaseModelFactoryOptions options)
    {
        var opened = connection.State != ConnectionState.Open;
        if (opened)
        {
            connection.Open();
        }

        try
        {
            var dialect = DialectFor(connection);
            var reader = dialect.SchemaReader
                ?? throw new NotSupportedException($"Dialect '{dialect.Name}' can't read a database's schema.");

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
