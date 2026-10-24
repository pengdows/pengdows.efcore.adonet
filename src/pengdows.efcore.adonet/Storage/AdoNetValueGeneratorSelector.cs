using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     Under <see cref="GeneratedValueRetrieval.RecordsAffected" />, store-generated integer keys are replaced by
///     values drawn from the dialect's key sequence. Generators are built per selection (this service is scoped)
///     because they hold the current connection.
/// </summary>
public class AdoNetValueGeneratorSelector(
    ValueGeneratorSelectorDependencies dependencies,
    AdoNetSqlDialect dialect,
    IRelationalConnection connection,
    IRawSqlCommandBuilder rawSqlCommandBuilder,
    IRelationalCommandDiagnosticsLogger commandLogger,
    ISqlGenerationHelper sqlGenerationHelper)
    : RelationalValueGeneratorSelector(dependencies)
{
    public override bool TrySelect(IProperty property, ITypeBase typeBase, out ValueGenerator? valueGenerator)
    {
        if (dialect.GeneratedValues != GeneratedValueRetrieval.RecordsAffected
            || property.ValueGenerated != ValueGenerated.OnAdd
            || !property.IsPrimaryKey()
            || property.GetValueGeneratorFactory() != null
            || property.GetDefaultValueSql() != null
            || typeBase is not IEntityType { } entityType
            || entityType.GetTableName() is not { } tableName)
        {
            return base.TrySelect(property, typeBase, out valueGenerator);
        }

        var table = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
        var sequence = sqlGenerationHelper.DelimitIdentifier(
            dialect.GetKeySequenceName(tableName, property.GetColumnName(table)!), table.Schema);
        var sql = dialect.GetNextSequenceValueSql(sequence);

        var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        valueGenerator = clrType == typeof(int) ? new AdoNetSequenceValueGenerator<int>(sql, connection, rawSqlCommandBuilder, commandLogger)
            : clrType == typeof(long) ? new AdoNetSequenceValueGenerator<long>(sql, connection, rawSqlCommandBuilder, commandLogger)
            : clrType == typeof(short) ? new AdoNetSequenceValueGenerator<short>(sql, connection, rawSqlCommandBuilder, commandLogger)
            : null;

        return valueGenerator != null || base.TrySelect(property, typeBase, out valueGenerator);
    }
}
