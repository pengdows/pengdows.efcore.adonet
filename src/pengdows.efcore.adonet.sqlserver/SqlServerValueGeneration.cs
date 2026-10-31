using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using pengdows.efcore.adonet.Storage;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>
///     A hi-lo key (<c>UseHiLo</c>) is numbered from blocks drawn from its sequence; a sequence key (<c>UseSequence</c>,
///     <c>UseKeySequences</c>) draws its value before the INSERT, since the core reads back only an identity. Everything else is
///     the core's. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/ValueGeneration/Internal/SqlServerValueGeneratorSelector.cs
/// </summary>
internal sealed class SqlServerValueGeneratorSelector : AdoNetValueGeneratorSelector
{
    private readonly IRelationalConnection _connection;
    private readonly IRawSqlCommandBuilder _rawSqlCommandBuilder;
    private readonly IRelationalCommandDiagnosticsLogger _commandLogger;
    private readonly ISqlGenerationHelper _sqlGenerationHelper;
    private readonly SqlServerHiLoStates _hiLoStates;

    public SqlServerValueGeneratorSelector(
        ValueGeneratorSelectorDependencies dependencies,
        ISqlDialect dialect,
        IRelationalConnection connection,
        IRawSqlCommandBuilder rawSqlCommandBuilder,
        IRelationalCommandDiagnosticsLogger commandLogger,
        ISqlGenerationHelper sqlGenerationHelper,
        SqlServerHiLoStates hiLoStates)
        : base(dependencies, dialect, connection, rawSqlCommandBuilder, commandLogger, sqlGenerationHelper)
    {
        _connection = connection;
        _rawSqlCommandBuilder = rawSqlCommandBuilder;
        _commandLogger = commandLogger;
        _sqlGenerationHelper = sqlGenerationHelper;
        _hiLoStates = hiLoStates;
    }

    public override bool TrySelect(IProperty property, ITypeBase typeBase, out ValueGenerator? valueGenerator)
    {
        valueGenerator = null;
        if ((property.ValueGenerated != ValueGenerated.OnAdd) || (property.GetValueGeneratorFactory() != null))
        {
            return base.TrySelect(property, typeBase, out valueGenerator);
        }

        var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        if (property.FindAnnotation(SqlServerAnnotationNames.HiLoSequenceName)?.Value is string hiLoName)
        {
            var schema = property.FindAnnotation(SqlServerAnnotationNames.HiLoSequenceSchema)?.Value as string;
            var sequence = _sqlGenerationHelper.DelimitIdentifier(hiLoName, schema);
            var blockSize = property.DeclaringType.Model.FindSequence(hiLoName, schema)?.IncrementBy ?? SqlServerAnnotationNames.DefaultHiLoBlockSize;
            var state = _hiLoStates.For(_connection.ConnectionString, sequence, blockSize);
            valueGenerator = HiLo(clrType, state, "SELECT NEXT VALUE FOR " + sequence);
        }
        else if (property.IsPrimaryKey() && (property.FindAnnotation(SqlServerAnnotationNames.SequenceName)?.Value is string sequenceName))
        {
            var schema = property.FindAnnotation(SqlServerAnnotationNames.SequenceSchema)?.Value as string;
            valueGenerator = Draw(clrType, "SELECT NEXT VALUE FOR " + _sqlGenerationHelper.DelimitIdentifier(sequenceName, schema));
        }

        return (valueGenerator != null) || base.TrySelect(property, typeBase, out valueGenerator);
    }

    private ValueGenerator? HiLo(Type clrType, HiLoValueGeneratorState state, string sql)
        => clrType == typeof(int) ? new SqlServerHiLoValueGenerator<int>(state, sql, _connection, _rawSqlCommandBuilder, _commandLogger)
            : clrType == typeof(long) ? new SqlServerHiLoValueGenerator<long>(state, sql, _connection, _rawSqlCommandBuilder, _commandLogger)
            : clrType == typeof(short) ? new SqlServerHiLoValueGenerator<short>(state, sql, _connection, _rawSqlCommandBuilder, _commandLogger)
            : clrType == typeof(byte) ? new SqlServerHiLoValueGenerator<byte>(state, sql, _connection, _rawSqlCommandBuilder, _commandLogger)
            : null;

    private ValueGenerator? Draw(Type clrType, string sql)
        => clrType == typeof(int) ? new AdoNetSequenceValueGenerator<int>(sql, _connection, _rawSqlCommandBuilder, _commandLogger)
            : clrType == typeof(long) ? new AdoNetSequenceValueGenerator<long>(sql, _connection, _rawSqlCommandBuilder, _commandLogger)
            : clrType == typeof(short) ? new AdoNetSequenceValueGenerator<short>(sql, _connection, _rawSqlCommandBuilder, _commandLogger)
            : clrType == typeof(byte) ? new AdoNetSequenceValueGenerator<byte>(sql, _connection, _rawSqlCommandBuilder, _commandLogger)
            : null;
}

/// <summary>
///     The block each hi-lo sequence is numbering, one per database (by connection string) and sequence, shared by every
///     context of the service provider so two contexts never number the same block twice.
/// </summary>
internal sealed class SqlServerHiLoStates
{
    private readonly ConcurrentDictionary<(string Database, string Sequence), HiLoValueGeneratorState> _states = new();

    public HiLoValueGeneratorState For(string? connectionString, string sequence, int blockSize)
        => _states.GetOrAdd((connectionString ?? "", sequence), _ => new HiLoValueGeneratorState(blockSize));
}

/// <summary>Draws the next low value from the sequence; EF's hi-lo state numbers the block it starts.</summary>
internal sealed class SqlServerHiLoValueGenerator<TValue>(
    HiLoValueGeneratorState state,
    string nextValueSql,
    IRelationalConnection connection,
    IRawSqlCommandBuilder rawSqlCommandBuilder,
    IRelationalCommandDiagnosticsLogger commandLogger)
    : HiLoValueGenerator<TValue>(state)
{
    public override bool GeneratesTemporaryValues
        => false;

    protected override long GetNewLowValue()
        => ToLong(rawSqlCommandBuilder.Build(nextValueSql).ExecuteScalar(Parameters()));

    protected override async Task<long> GetNewLowValueAsync(CancellationToken cancellationToken = default)
        => ToLong(await rawSqlCommandBuilder.Build(nextValueSql).ExecuteScalarAsync(Parameters(), cancellationToken).ConfigureAwait(false));

    private RelationalCommandParameterObject Parameters()
        => new(connection, null, null, null, commandLogger, CommandSource.ValueGenerator);

    private static long ToLong(object? value)
        => Convert.ToInt64(value ?? throw new InvalidOperationException("The hi-lo sequence returned no value."), CultureInfo.InvariantCulture);
}
