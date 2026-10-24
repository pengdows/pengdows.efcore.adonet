using System.Text;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Storage;

public class AdoNetSqlGenerationHelper(RelationalSqlGenerationHelperDependencies dependencies, AdoNetSqlDialect dialect)
    : RelationalSqlGenerationHelper(dependencies)
{
    // Name given to DbParameter.ParameterName.
    public override string GenerateParameterName(string name)
        => dialect.ParameterNameIncludesMarker
            ? GenerateParameterNamePlaceholder(name)
            : name.StartsWith(dialect.ParameterMarker, StringComparison.Ordinal) ? name[dialect.ParameterMarker.Length..] : name;

    public override void GenerateParameterName(StringBuilder builder, string name)
        => builder.Append(GenerateParameterName(name));

    // Reference to the parameter inside the SQL text.
    public override string GenerateParameterNamePlaceholder(string name)
        => name.StartsWith(dialect.ParameterMarker, StringComparison.Ordinal) ? name : dialect.ParameterMarker + name;

    public override void GenerateParameterNamePlaceholder(StringBuilder builder, string name)
        => builder.Append(GenerateParameterNamePlaceholder(name));

    public override string EscapeIdentifier(string identifier)
        => identifier.Replace(dialect.IdentifierQuoteSuffix, dialect.IdentifierQuoteSuffix + dialect.IdentifierQuoteSuffix);

    public override void EscapeIdentifier(StringBuilder builder, string identifier)
        => builder.Append(EscapeIdentifier(identifier));

    public override string DelimitIdentifier(string identifier)
        => dialect.IdentifierQuotePrefix + EscapeIdentifier(identifier) + dialect.IdentifierQuoteSuffix;

    public override void DelimitIdentifier(StringBuilder builder, string identifier)
        => builder.Append(DelimitIdentifier(identifier));

    public override string DelimitIdentifier(string name, string? schema)
        => base.DelimitIdentifier(name, dialect.SupportsSchemas ? schema : null);

    public override void DelimitIdentifier(StringBuilder builder, string name, string? schema)
        => base.DelimitIdentifier(builder, name, dialect.SupportsSchemas ? schema : null);
}
