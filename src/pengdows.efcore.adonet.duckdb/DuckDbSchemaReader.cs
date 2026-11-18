using pengdows.efcore.adonet.Scaffolding;

namespace pengdows.efcore.adonet.duckdb;

/// <summary>DuckDB's information schema exposes comments on tables and columns.</summary>
internal sealed class DuckDbSchemaReader : InformationSchemaReader
{
    public override string TablesSql
        => "SELECT TABLE_SCHEMA AS \"SCHEMA\", TABLE_NAME AS \"NAME\", TABLE_TYPE AS \"TYPE\", COMMENT AS \"COMMENT\" "
            + "FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = {schema} AND TABLE_TYPE IN ('BASE TABLE', 'VIEW')";

    public override string ColumnsSql
        => "SELECT TABLE_SCHEMA AS \"SCHEMA\", TABLE_NAME AS \"TABLE\", COLUMN_NAME AS \"NAME\", ORDINAL_POSITION AS \"POSITION\", "
            + "IS_NULLABLE AS \"NULLABLE\", COLUMN_DEFAULT AS \"DEFAULT\", DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, "
            + "NUMERIC_SCALE, DATETIME_PRECISION, COLLATION_NAME, COMMENT AS \"COMMENT\" "
            + "FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = {schema}";
}
