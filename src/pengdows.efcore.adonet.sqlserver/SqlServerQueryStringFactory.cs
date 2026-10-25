// Adapted from EF Core's SqlServerQueryStringFactory (MIT, .NET Foundation).

using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary><c>ToQueryString</c> as runnable T-SQL: each parameter is a <c>DECLARE</c> with its type and value.</summary>
public class SqlServerQueryStringFactory(IRelationalTypeMappingSource typeMappingSource) : IRelationalQueryStringFactory
{
    public virtual string Create(DbCommand command)
    {
        if (command.Parameters.Count == 0)
        {
            return command.CommandText;
        }

        var builder = new StringBuilder();
        foreach (DbParameter parameter in command.Parameters)
        {
            var typeName = TypeName(parameter);
            builder.Append("DECLARE ").Append(parameter.ParameterName).Append(' ').Append(typeName).Append(" = ");
            builder.Append(
                parameter.Value is null || parameter.Value == DBNull.Value
                    ? "NULL"
                    : typeMappingSource.FindMapping(parameter.Value.GetType(), typeName)?.GenerateSqlLiteral(parameter.Value)
                    ?? parameter.Value.ToString());
            builder.AppendLine(";");
        }

        return builder.AppendLine().Append(command.CommandText).ToString();
    }

    private static string TypeName(DbParameter parameter)
        => parameter is not SqlParameter sql
            ? "sql_variant"
            : sql.SqlDbType switch
            {
                SqlDbType.BigInt => "bigint",
                SqlDbType.Binary => "binary" + Size(parameter),
                SqlDbType.Bit => "bit",
                SqlDbType.Char => "char" + Size(parameter),
                SqlDbType.Date => "date",
                SqlDbType.DateTime => "datetime",
                SqlDbType.DateTime2 => "datetime2" + Facet(parameter.Precision),
                SqlDbType.DateTimeOffset => "datetimeoffset" + Facet(parameter.Precision),
                SqlDbType.Decimal => "decimal" + (parameter is { Precision: > 0, Scale: > 0 }
                    ? $"({parameter.Precision.ToString(CultureInfo.InvariantCulture)},{parameter.Scale.ToString(CultureInfo.InvariantCulture)})"
                    : Facet(parameter.Precision)),
                SqlDbType.Float => "float" + Size(parameter),
                SqlDbType.Image => "image",
                SqlDbType.Int => "int",
                SqlDbType.Money => "money",
                SqlDbType.NChar => "nchar" + Size(parameter),
                SqlDbType.NText => "ntext",
                SqlDbType.NVarChar => "nvarchar" + SizeOrMax(parameter),
                SqlDbType.Real => "real",
                SqlDbType.SmallDateTime => "smalldatetime",
                SqlDbType.SmallInt => "smallint",
                SqlDbType.SmallMoney => "smallmoney",
                SqlDbType.Structured => "structured",
                SqlDbType.Text => "text",
                SqlDbType.Time => "time" + Facet(parameter.Scale),
                SqlDbType.Timestamp => "rowversion",
                SqlDbType.TinyInt => "tinyint",
                SqlDbType.Udt => sql.UdtTypeName,
                SqlDbType.UniqueIdentifier => "uniqueIdentifier",
                SqlDbType.VarBinary => "varbinary" + SizeOrMax(parameter),
                SqlDbType.VarChar => "varchar" + SizeOrMax(parameter),
                SqlDbType.Xml => "xml",
                _ => "sql_variant"
            };

    private static string Size(DbParameter parameter)
        => Facet(parameter.Size);

    private static string SizeOrMax(DbParameter parameter)
        => parameter.Size == -1 ? "(max)" : Size(parameter);

    private static string Facet(int value)
        => value > 0 ? $"({value.ToString(CultureInfo.InvariantCulture)})" : "";
}
