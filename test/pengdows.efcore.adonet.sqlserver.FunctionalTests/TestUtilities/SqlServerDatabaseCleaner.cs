namespace Microsoft.EntityFrameworkCore.TestUtilities;

/// <summary>pengdows: drops every user object, in dependency order, without reading the schema through EF.</summary>
public static class SqlServerDatabaseCleaner
{
    public const string DropAllSql = """
        DECLARE @sql nvarchar(max) = N'';
        SELECT @sql += N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) + N' SET (SYSTEM_VERSIONING = OFF);'
            FROM sys.tables t WHERE t.temporal_type = 2;
        SELECT @sql += N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) + N' DROP CONSTRAINT ' + QUOTENAME(f.name) + N';'
            FROM sys.foreign_keys f JOIN sys.tables t ON f.parent_object_id = t.object_id;
        SELECT @sql += N'DROP VIEW ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' FROM sys.views WHERE is_ms_shipped = 0;
        SELECT @sql += N'DROP TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' FROM sys.tables WHERE is_ms_shipped = 0;
        SELECT @sql += N'DROP SEQUENCE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' FROM sys.sequences;
        SELECT @sql += N'DROP ' + CASE WHEN type = 'P' THEN N'PROCEDURE ' ELSE N'FUNCTION ' END
            + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';'
            FROM sys.objects WHERE type IN ('P', 'FN', 'IF', 'TF') AND is_ms_shipped = 0;
        SELECT @sql += N'DROP TYPE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' FROM sys.types WHERE is_user_defined = 1;
        SELECT @sql += N'DROP SCHEMA ' + QUOTENAME(name) + N';' FROM sys.schemas
            WHERE schema_id > 4 AND schema_id < 16384 AND name NOT IN ('guest', 'INFORMATION_SCHEMA', 'sys');
        EXEC sp_executesql @sql;
        """;
}
