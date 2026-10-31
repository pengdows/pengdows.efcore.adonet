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

    /// <summary>How the database stores its data: on a server, in a file, or in a directory.</summary>
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

    /// <summary>
    ///     Whether the database is single-writer: one writer at a time, any number of readers. Its migrations take the lock file beside it,
    ///     and a single-writer database with no lock file to name refuses to migrate. Runtime writer control is not this library's concern.
    /// </summary>
    public bool SingleWriter { get; private init; }

    /// <summary>Whether a connection string names a database held in memory, which exists while it is open.</summary>
    public Func<DbConnectionStringBuilder, bool> IsInMemory { get; private init; } = _ => false;

    /// <summary>
    ///     Whether the database's file (or directory) can have a lock file beside it. A path this code can't name as a plain file (a URI,
    ///     say) can't, and a single-writer database at such a path is refused for migrations.
    /// </summary>
    public Func<DbConnectionStringBuilder, bool> IsLockable { get; private init; } = _ => true;

    /// <summary>
    ///     Releases pooled connections to the database before it is deleted, so none still holds it. When null, the driver's
    ///     static <c>ClearPool(TheirConnection)</c> is found by convention.
    /// </summary>
    public Action<DbConnection>? ClearPool { get; private init; }

    /// <summary>A database on a server, created and deleted by statements the dialect writes.</summary>
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

    /// <summary>A database held in a file.</summary>
    public static DatabaseLifecycle File(
        string pathKeyword,
        IReadOnlyList<string>? companionSuffixes = null,
        string? initializeSql = null,
        Func<DbConnectionStringBuilder, bool>? isInMemory = null,
        Action<DbConnection>? clearPool = null,
        Func<DbConnectionStringBuilder, bool>? isLockable = null,
        bool singleWriter = false)
        => new(DatabaseStorage.File, pathKeyword)
        {
            CompanionSuffixes = companionSuffixes ?? [],
            InitializeSql = initializeSql,
            IsInMemory = isInMemory ?? (_ => false),
            IsLockable = isLockable ?? (_ => true),
            ClearPool = clearPool,
            SingleWriter = singleWriter
        };

    /// <summary>A database held in a directory.</summary>
    public static DatabaseLifecycle Directory(string pathKeyword, bool singleWriter = false)
        => new(DatabaseStorage.Directory, pathKeyword) { SingleWriter = singleWriter };

    /// <summary>
    ///     The migrations lock file for the database a connection string names: a sidecar beside the file or directory, or null where
    ///     there is none (a server, an in-memory database, or a path that can't have a file beside it).
    /// </summary>
    public string? LockFileFor(DbConnectionStringBuilder connection)
    {
        if (Storage == DatabaseStorage.Server || IsInMemory(connection) || !IsLockable(connection))
        {
            return null;
        }

        return connection.TryGetValue(Keyword, out var value) && Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) is { Length: > 0 } path
            ? Path.TrimEndingDirectorySeparator(path) + ".migrations.lock"
            : null;
    }
}
