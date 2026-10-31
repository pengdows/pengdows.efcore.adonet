using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
namespace pengdows.efcore.adonet.Query;

/// <summary>
///     <see cref="Guid.NewGuid" />, <see cref="System.Text.RegularExpressions.Regex.IsMatch(string, string)" />, the
///     <see cref="Convert" /> methods to scalar types, and <c>ToString()</c> of numbers and GUIDs, which are casts.
/// </summary>
internal class AdoNetMiscellaneousTranslator(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    FunctionSyntax functions) : IMethodCallTranslator
{
    private static readonly HashSet<Type> ConvertTargets =
        [typeof(bool), typeof(byte), typeof(decimal), typeof(double), typeof(short), typeof(int), typeof(long), typeof(string)];

    // Types whose invariant text a cast to text writes as .NET does.
    private static readonly HashSet<Type> TextualTypes =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(decimal), typeof(Guid), typeof(string)
    ];

    private static readonly HashSet<Type> ConvertSources =
    [
        typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long),
        typeof(ulong), typeof(decimal), typeof(double), typeof(float), typeof(string)
    ];

    private static SqlExpression Unboxed(SqlExpression argument)
        => argument is SqlUnaryExpression { OperatorType: System.Linq.Expressions.ExpressionType.Convert, Type: var type, Operand: var operand }
            && (type == typeof(object))
                ? operand
                : argument;

    private static Type SourceType(SqlExpression argument)
    {
        var type = Unboxed(argument).Type;
        return Nullable.GetUnderlyingType(type) ?? type;
    }

    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if ((method.DeclaringType == typeof(Guid)) && (method.Name == nameof(Guid.NewGuid)) && (arguments.Count == 0))
        {
            return sqlExpressionFactory.Call(functions.NewGuid, [], typeof(Guid), typeMappingSource.FindMapping(typeof(Guid)));
        }

        if ((method.DeclaringType == typeof(System.Text.RegularExpressions.Regex))
            && (method.Name == nameof(System.Text.RegularExpressions.Regex.IsMatch))
            && (instance == null)
            && method.GetParameters() is [{ ParameterType: var input }, { ParameterType: var pattern }]
            && (input == typeof(string))
            && (pattern == typeof(string)))
        {
            var mapping = ExpressionExtensions.InferTypeMapping(arguments[0], arguments[1]) ?? typeMappingSource.FindMapping(typeof(string));
            return sqlExpressionFactory.Call(
                functions.RegexIsMatch,
                [sqlExpressionFactory.ApplyTypeMapping(arguments[0], mapping)!, sqlExpressionFactory.ApplyTypeMapping(arguments[1], mapping)!],
                typeof(bool),
                typeMappingSource.FindMapping(typeof(bool)));
        }

        // Enumerable.First and Contains over a byte array column.
        if ((method.DeclaringType == typeof(Enumerable))
            && (instance == null)
            && (arguments.Count > 0)
            && (arguments[0].Type == typeof(byte[])))
        {
            var bytes = sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[0])!;
            var byteMapping = typeMappingSource.FindMapping(typeof(byte));
            return (method.Name, arguments.Count) switch
            {
                (nameof(Enumerable.First), 1)
                    => sqlExpressionFactory.Call(functions.ByteAt, [bytes, sqlExpressionFactory.Constant(1)], typeof(byte), byteMapping),
                (nameof(Enumerable.ElementAt), 2) when arguments[1].Type == typeof(int)
                    => sqlExpressionFactory.Call(
                        functions.ByteAt,
                        [bytes, sqlExpressionFactory.Add(sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[1])!, sqlExpressionFactory.Constant(1))],
                        typeof(byte),
                        byteMapping),
                (nameof(Enumerable.Contains), 2)
                    => sqlExpressionFactory.Call(
                        functions.BytesContain,
                        [bytes, sqlExpressionFactory.ApplyTypeMapping(arguments[1], byteMapping)!],
                        typeof(bool),
                        typeMappingSource.FindMapping(typeof(bool))),
                _ => null
            };
        }

        // A value boxed to object (Convert.ToInt32((object)x)) converts as the value it holds.
        if ((method.DeclaringType == typeof(Convert))
            && (instance == null)
            && (arguments.Count == 1)
            && ConvertTargets.Contains(method.ReturnType)
            && (ConvertSources.Contains(SourceType(arguments[0]))
                // A date as text: the database's format, where .NET's is the current culture's.
                || ((method.ReturnType == typeof(string)) && (SourceType(arguments[0]) == typeof(DateTime)))))
        {
            return sqlExpressionFactory.Convert(
                sqlExpressionFactory.ApplyDefaultTypeMapping(Unboxed(arguments[0]))!,
                method.ReturnType,
                typeMappingSource.FindMapping(method.ReturnType));
        }

        if ((instance != null)
            && (method.Name == nameof(ToString))
            && (arguments.Count == 0)
            && TextualTypes.Contains(Nullable.GetUnderlyingType(instance.Type) ?? instance.Type))
        {
            return instance.Type == typeof(string)
                ? instance
                : sqlExpressionFactory.Convert(instance, typeof(string), typeMappingSource.FindMapping(typeof(string)));
        }

        return null;
    }
}
