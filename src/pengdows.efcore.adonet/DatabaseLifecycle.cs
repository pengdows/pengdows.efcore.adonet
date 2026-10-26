using System.Data.Common;

namespace pengdows.efcore.adonet;

/// <summary>Where a database lives, which decides how it is created, found and deleted.</summary>
public enum DatabaseStorage
{
    /// <summary>On a server: created and dropped with SQL, through a connection to an administrative database.</summary>
    Server,

    /// <summary>A file (with companion files, such as a write-ahead log), created by opening a connection to it.</summary>
    File,

    /// <summary>A directory of files.</summary>
    Directory
}

/// <summary>
///     How a database is created and deleted: ADO.NET and ISO SQL have no portable form for either, so each dialect declares
///     it. The connection string says which database; <see cref="Keyword" /> is the key that names it.
/// </summary>
public sealed class DatabaseLifecycle
{
    private DatabaseLifecycle(DatabaseStorage storage, string keyword)
    {
        Storage = storage;
        Keyword = keyword;
    }

    public DatabaseStorage Storage { get; }

    /// <summary>The connection-string key naming the database (server) or its path (file, directory).</summary>
    public string Keyword { get; }

    /// <summary>The database an administrative connection uses; null to connect without one.</summary>
    public string? AdminDatabase { get; private init; }

    /// <summary>Creates a database; <c>{0}</c> is the delimited name.</summary>
    public string CreateSql { get; private init; } = "CREATE DATABASE {0}";

    /// <summary>Drops a database, statement by statement; <c>{0}</c> is the delimited name.</summary>
    public IReadOnlyList<string> DropSql { get; private init; } = ["DROP DATABASE {0}"];

    /// <summary>Files beside the database's that belong to it (such as <c>-wal</c>), deleted with it.</summary>
    public IReadOnlyList<string> CompanionSuffixes { get; private init; } = [];

    /// <summary>Run once on a database just created, such as a journal mode the database keeps.</summary>
    public string? InitializeSql { get; private init; }

    /// <summary>Whether a connection string names a database held in memory, which exists while it is open.</summary>
    public Func<DbConnectionStringBuilder, bool> IsInMemory { get; private init; } = _ => false;

    /// <summary>
    ///     Releases pooled connections to the database before it is deleted, so none still holds it. When null, the driver's
    ///     static <c>ClearPool(TheirConnection)</c> is found by convention.
    /// </summary>
    public Action<DbConnection>? ClearPool { get; private init; }

    public static DatabaseLifecycle Server(
        string databaseKeyword,
        string? adminDatabase,
        string createSql = "CREATE DATABASE {0}",
        IReadOnlyList<string>? dropSql = null,
        Action<DbConnection>? clearPool = null)
        => new(DatabaseStorage.Server, databaseKeyword)
        {
            AdminDatabase = adminDatabase,
            CreateSql = createSql,
            DropSql = dropSql ?? ["DROP DATABASE {0}"],
            ClearPool = clearPool
        };

    public static DatabaseLifecycle File(
        string pathKeyword,
        IReadOnlyList<string>? companionSuffixes = null,
        string? initializeSql = null,
        Func<DbConnectionStringBuilder, bool>? isInMemory = null,
        Action<DbConnection>? clearPool = null)
        => new(DatabaseStorage.File, pathKeyword)
        {
            CompanionSuffixes = companionSuffixes ?? [],
            InitializeSql = initializeSql,
            IsInMemory = isInMemory ?? (_ => false),
            ClearPool = clearPool
        };

    public static DatabaseLifecycle Directory(string pathKeyword)
        => new(DatabaseStorage.Directory, pathKeyword);
}
