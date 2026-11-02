using pengdows.efcore.adonet.Scaffolding;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>
///     ISO's catalog, with the comments PostgreSQL keeps in its own catalog: a table's comment is <c>obj_description</c> and a
///     column's is <c>col_description</c>, both read through the table's oid. Everything else is ISO's query.
/// </summary>
internal sealed class PostgreSqlSchemaReader : InformationSchemaReader
{
    public static readonly PostgreSqlSchemaReader Instance = new();

    // An extension's tables and views (PostGIS's spatial_ref_sys, geometry_columns) are not the database's: an extension member
    // is a pg_depend entry of deptype 'e'.
    private static string NotAnExtensionObject(string schema, string name)
        => "NOT EXISTS (SELECT 1 FROM pg_class k JOIN pg_namespace n ON n.oid = k.relnamespace "
            + "JOIN pg_depend d ON d.objid = k.oid AND d.classid = 'pg_class'::regclass "
            + "WHERE n.nspname = " + schema + " AND k.relname = " + name + " AND d.deptype = 'e')";

    private const string TableOid = "to_regclass(quote_ident(TABLE_SCHEMA) || '.' || quote_ident(TABLE_NAME))";

    public override string TablesSql
        => "SELECT TABLE_SCHEMA AS \"SCHEMA\", TABLE_NAME AS \"NAME\", TABLE_TYPE AS \"TYPE\", "
            + "obj_description(" + TableOid + ", 'pg_class') AS \"COMMENT\" "
            + "FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = {schema} AND TABLE_TYPE IN ('BASE TABLE', 'VIEW')"
            + " AND " + NotAnExtensionObject("TABLE_SCHEMA", "TABLE_NAME");

    public override string ColumnsSql
        => "SELECT c.TABLE_SCHEMA AS \"SCHEMA\", c.TABLE_NAME AS \"TABLE\", c.COLUMN_NAME AS \"NAME\", c.ORDINAL_POSITION AS \"POSITION\", "
            + "c.IS_NULLABLE AS \"NULLABLE\", c.COLUMN_DEFAULT AS \"DEFAULT\", c.DATA_TYPE, c.CHARACTER_MAXIMUM_LENGTH, c.NUMERIC_PRECISION, "
            + "c.NUMERIC_SCALE, c.DATETIME_PRECISION, c.COLLATION_NAME, "
            + "(SELECT col_description(a.attrelid, a.attnum) FROM pg_attribute a "
            + "WHERE a.attrelid = to_regclass(quote_ident(c.TABLE_SCHEMA) || '.' || quote_ident(c.TABLE_NAME)) AND a.attname = c.COLUMN_NAME) AS \"COMMENT\" "
            + "FROM INFORMATION_SCHEMA.COLUMNS c WHERE c.TABLE_SCHEMA = {schema} AND "
            + NotAnExtensionObject("c.TABLE_SCHEMA", "c.TABLE_NAME");
}
