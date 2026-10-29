namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     What a MySQL or MariaDB server of a version has that this dialect writes, as Pomelo's ServerVersion support flags say;
///     a dialect for an older server turns off what it lacks. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Infrastructure/MySqlServerVersion.cs
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Infrastructure/MariaDbServerVersion.cs
/// </summary>
internal sealed record MySqlServerFeatures(
    bool LateralJoins,
    bool OuterReferencesInDerivedTables,
    bool WindowFunctions,
    bool JsonTable,
    bool JsonEquals,
    bool TableValueConstructor,
    bool RegexpFunctions,
    bool GeometryAxisOrder,
    bool ExpressionDefaults,
    bool ColumnSrid,
    bool DescendingIndexes,
    bool RenameColumn,
    bool GeographicDistance,
    bool DistanceSphere)
{
    // The oldest servers with the JSON functions every MySqlDialect writes: MySQL 5.7.8, and MariaDB 10.2.4 (JSON_COMPACT).
    public static readonly Version OldestMySql = new(5, 7, 8);
    public static readonly Version OldestMariaDb = new(10, 2, 4);

    public static MySqlServerFeatures For(bool mariaDb, Version? version)
        => mariaDb ? MariaDb(version) : MySql(version);

    private static MySqlServerFeatures MySql(Version? version)
    {
        bool From(int major, int minor, int build)
            => (version == null) || (version >= new Version(major, minor, build));

        return new MySqlServerFeatures(
            LateralJoins: From(8, 0, 14),
            OuterReferencesInDerivedTables: From(8, 0, 14),
            WindowFunctions: From(8, 0, 0),
            JsonTable: From(8, 0, 4),
            JsonEquals: true,
            TableValueConstructor: From(8, 0, 19),
            RegexpFunctions: From(8, 0, 4),
            GeometryAxisOrder: From(8, 0, 1),
            ExpressionDefaults: From(8, 0, 13),
            ColumnSrid: From(8, 0, 3),
            DescendingIndexes: From(8, 0, 1),
            RenameColumn: From(8, 0, 0),
            GeographicDistance: From(8, 0, 0),
            DistanceSphere: From(8, 0, 0));
    }

    // MariaDB has no LATERAL (MDEV-19078), reads no outer row from a derived table, has no axis-order option and measures no
    // geographic distance; it writes a column's SRID (REF_SYSTEM_ID) and DESC indexes it accepts at every version this
    // dialect takes. ST_DISTANCE_SPHERE came in 10.5.10 and was backported to 10.2.38, 10.3.29 and 10.4.19
    // (MDEV-13467: https://mariadb.com/docs/server/ref/mdb/functions/ST_DISTANCE_SPHERE).
    private static MySqlServerFeatures MariaDb(Version? version)
    {
        bool From(int major, int minor, int build)
            => (version == null) || (version >= new Version(major, minor, build));

        var distanceSphere = (version == null)
            || (version >= new Version(10, 5, 10))
            || ((version.Major == 10) && (version.Minor == 4) && (version.Build >= 19))
            || ((version.Major == 10) && (version.Minor == 3) && (version.Build >= 29))
            || ((version.Major == 10) && (version.Minor == 2) && (version.Build >= 38));
        return new MySqlServerFeatures(
            LateralJoins: false,
            OuterReferencesInDerivedTables: false,
            WindowFunctions: From(10, 2, 0),
            JsonTable: From(10, 6, 0),
            JsonEquals: From(10, 7, 0),
            TableValueConstructor: From(10, 3, 3),
            RegexpFunctions: true,
            GeometryAxisOrder: false,
            ExpressionDefaults: From(10, 2, 7),
            ColumnSrid: true,
            DescendingIndexes: true,
            RenameColumn: From(10, 5, 2),
            GeographicDistance: false,
            DistanceSphere: distanceSphere);
    }
}
