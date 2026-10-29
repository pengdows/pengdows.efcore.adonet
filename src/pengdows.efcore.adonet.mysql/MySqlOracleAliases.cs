using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     The names Oracle's MySql.EntityFrameworkCore gives its entry points and fluent methods, so an application moving from it
///     compiles with only its <c>using</c>s changed; each forwards to this provider's own API. The names and signatures come from
///     that package's public API and dev.mysql.com's Connector/NET documentation
///     (https://dev.mysql.com/doc/connector-net/en/connector-net-entityframework-core-charset.html); none of its code is read or
///     used (GPL).
/// </summary>
public static class MySqlOracleAliases
{
    /// <summary>Oracle's name for <c>UseAdoNetMySql</c>, with the connection string set later.</summary>
    public static DbContextOptionsBuilder UseMySQL(this DbContextOptionsBuilder optionsBuilder, Action<AdoNetDbContextOptionsBuilder>? mySqlOptionsAction = null)
        => optionsBuilder.UseAdoNetMySql(mySqlOptionsAction);

    /// <summary>Oracle's name for <c>UseAdoNetMySql</c>.</summary>
    public static DbContextOptionsBuilder UseMySQL(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? mySqlOptionsAction = null)
        => optionsBuilder.UseAdoNetMySql(connectionString, mySqlOptionsAction);

    /// <summary>Oracle's name for <c>UseAdoNetMySql</c>, over a connection the application owns.</summary>
    public static DbContextOptionsBuilder UseMySQL(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        Action<AdoNetDbContextOptionsBuilder>? mySqlOptionsAction = null)
        => optionsBuilder.UseAdoNetMySql(connection, contextOwnsConnection: false, mySqlOptionsAction);

    /// <summary>Oracle's name for <c>UseAdoNetMySql</c>, with the connection string set later.</summary>
    public static DbContextOptionsBuilder<TContext> UseMySQL<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        Action<AdoNetDbContextOptionsBuilder>? mySqlOptionsAction = null)
        where TContext : DbContext
        => optionsBuilder.UseAdoNetMySql(mySqlOptionsAction);

    /// <summary>Oracle's name for <c>UseAdoNetMySql</c>.</summary>
    public static DbContextOptionsBuilder<TContext> UseMySQL<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? mySqlOptionsAction = null)
        where TContext : DbContext
        => optionsBuilder.UseAdoNetMySql(connectionString, mySqlOptionsAction);

    /// <summary>Oracle's name for <c>UseAdoNetMySql</c>, over a connection the application owns.</summary>
    public static DbContextOptionsBuilder<TContext> UseMySQL<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        Action<AdoNetDbContextOptionsBuilder>? mySqlOptionsAction = null)
        where TContext : DbContext
        => optionsBuilder.UseAdoNetMySql(connection, contextOwnsConnection: false, mySqlOptionsAction);

    /// <summary>Registers <typeparamref name="TContext" /> for MySQL, as Oracle's <c>AddMySQLServer</c> (EF's <c>AddDbContext</c>).</summary>
    public static IServiceCollection AddMySQLServer<TContext>(
        this IServiceCollection serviceCollection,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? sqlServerOptionsAction = null,
        Action<DbContextOptionsBuilder>? optionsAction = null)
        where TContext : DbContext
        => serviceCollection.AddDbContext<TContext>(o =>
        {
            o.UseAdoNetMySql(connectionString, sqlServerOptionsAction);
            optionsAction?.Invoke(o);
        });

    /// <summary>The provider's services for MySQL in EF's internal service provider, as Oracle's <c>AddEntityFrameworkMySQL</c>.</summary>
    public static IServiceCollection AddEntityFrameworkMySQL(this IServiceCollection services)
        => services.AddEntityFrameworkAdoNet(MySqlDialect.Instance);

    /// <summary>
    ///     Oracle's third <c>EnableRetryOnFailure</c>: also retries the MySQL errors numbered in
    ///     <paramref name="errorNumbersToAdd" />, as either driver reports them (its exception's <c>Number</c>).
    /// </summary>
    public static AdoNetDbContextOptionsBuilder EnableRetryOnFailure(
        this AdoNetDbContextOptionsBuilder optionsBuilder,
        int maxRetryCount,
        TimeSpan maxRetryDelay,
        ICollection<int>? errorNumbersToAdd)
        => optionsBuilder.EnableRetryOnFailure(maxRetryCount, maxRetryDelay, RetriesOn(errorNumbersToAdd ?? []));

    // A driver's exception carries the server's error number as Number (MySqlConnector and MySql.Data alike), read by name so
    // that no driver is referenced.
    internal static Func<Exception, bool> RetriesOn(ICollection<int> errorNumbers)
    {
        var numbers = errorNumbers.ToHashSet();
        return exception => (exception is DbException)
                            && (exception.GetType().GetProperty("Number")?.GetValue(exception) is int number)
                            && numbers.Contains(number);
    }

    /// <summary>Oracle's name for the column's <c>HasCharSet</c>.</summary>
    public static PropertyBuilder ForMySQLHasCharset(this PropertyBuilder propertyBuilder, string charset)
        => propertyBuilder.HasCharSet(charset);

    /// <summary>Oracle's name for the table's <c>HasCharSet</c>.</summary>
    public static EntityTypeBuilder ForMySQLHasCharset(this EntityTypeBuilder entityTypeBuilder, string charset)
        => entityTypeBuilder.HasCharSet(charset);

    /// <summary>Oracle's name for EF's column <c>UseCollation</c>.</summary>
    public static PropertyBuilder ForMySQLHasCollation(this PropertyBuilder propertyBuilder, string collation)
        => propertyBuilder.UseCollation(collation);

    /// <summary>Oracle's name for the table's <c>UseCollation</c>.</summary>
    public static EntityTypeBuilder ForMySQLHasCollation(this EntityTypeBuilder entityTypeBuilder, string collation)
        => entityTypeBuilder.UseCollation(collation);

    /// <summary>Oracle's name for EF's <c>HasDefaultValueSql</c>.</summary>
    public static PropertyBuilder ForMySQLHasDefaultValueSql(this PropertyBuilder propertyBuilder, string sql)
        => propertyBuilder.HasDefaultValueSql(sql);

    /// <summary>Oracle's name for EF's <c>HasDefaultValue</c>.</summary>
    public static PropertyBuilder ForMySQLHasDefaultValue(this PropertyBuilder propertyBuilder, object? value = null)
        => propertyBuilder.HasDefaultValue(value);

    /// <summary>
    ///     A column of type <paramref name="typeName" /> the server numbers on insert (AUTO_INCREMENT), or, for a date and
    ///     time, sets on insert (<c>DEFAULT CURRENT_TIMESTAMP</c>): Oracle's name for <c>HasColumnType</c> and
    ///     <c>UseMySqlIdentityColumn</c>.
    /// </summary>
    public static PropertyBuilder UseMySQLAutoIncrementColumn(this PropertyBuilder propertyBuilder, string typeName)
        => propertyBuilder.HasColumnType(typeName).UseMySqlIdentityColumn();
}

/// <summary>Oracle's name for <see cref="MySqlMatchSearchMode" />, which <c>EF.Functions.Match</c> also takes.</summary>
public enum MySQLMatchSearchMode
{
    /// <summary>IN NATURAL LANGUAGE MODE, the default.</summary>
    NaturalLanguage,

    /// <summary>IN NATURAL LANGUAGE MODE WITH QUERY EXPANSION.</summary>
    NaturalLanguageWithQueryExpansion,

    /// <summary>IN BOOLEAN MODE.</summary>
    Boolean
}

/// <summary>How a column's value is generated, as Oracle names it: numbered or set on insert, or set on insert and update.</summary>
public enum MySQLValueGenerationStrategy
{
    /// <summary>Not generated by the server.</summary>
    None,

    /// <summary>AUTO_INCREMENT, or for a date and time, DEFAULT CURRENT_TIMESTAMP (<c>UseMySqlIdentityColumn</c>).</summary>
    IdentityColumn,

    /// <summary>A date and time set on insert and update (<c>UseMySqlComputedColumn</c>).</summary>
    ComputedColumn
}

/// <summary>The class's table or the property's column CHARACTER SET, as Oracle's attribute; a fluent call overrides it.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
public sealed class MySQLCharsetAttribute(string charset) : Attribute
{
    public string Charset { get; } = charset;
}

/// <summary>The class's table or the property's column COLLATE, as Oracle's attribute; a fluent call overrides it.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
public sealed class MySQLCollationAttribute(string collation) : Attribute
{
    public string Collation { get; } = collation;
}

internal sealed class MySqlAttributeConventionPlugin : IConventionSetPlugin
{
    public ConventionSet ModifyConventions(ConventionSet conventionSet)
    {
        var convention = new MySqlAttributeConvention();
        conventionSet.EntityTypeAddedConventions.Add(convention);
        conventionSet.PropertyAddedConventions.Add(convention);
        return conventionSet;
    }
}

/// <summary>Reads MySQLCharset and MySQLCollation as data annotations, which an explicit fluent call overrides.</summary>
internal sealed class MySqlAttributeConvention : IEntityTypeAddedConvention, IPropertyAddedConvention
{
    public void ProcessEntityTypeAdded(IConventionEntityTypeBuilder entityTypeBuilder, IConventionContext<IConventionEntityTypeBuilder> context)
    {
        var type = entityTypeBuilder.Metadata.ClrType;
        if (Attribute.GetCustomAttribute(type, typeof(MySQLCharsetAttribute)) is MySQLCharsetAttribute charSet)
        {
            entityTypeBuilder.HasAnnotation(MySqlModelOptionsExtensions.CharSetAnnotation, charSet.Charset, fromDataAnnotation: true);
        }

        if (Attribute.GetCustomAttribute(type, typeof(MySQLCollationAttribute)) is MySQLCollationAttribute collation)
        {
            entityTypeBuilder.HasAnnotation(MySqlModelOptionsExtensions.CollationAnnotation, collation.Collation, fromDataAnnotation: true);
        }
    }

    public void ProcessPropertyAdded(IConventionPropertyBuilder propertyBuilder, IConventionContext<IConventionPropertyBuilder> context)
    {
        if ((propertyBuilder.Metadata.PropertyInfo ?? (System.Reflection.MemberInfo?)propertyBuilder.Metadata.FieldInfo) is not { } member)
        {
            return;
        }

        if (Attribute.GetCustomAttribute(member, typeof(MySQLCharsetAttribute)) is MySQLCharsetAttribute charSet)
        {
            propertyBuilder.HasAnnotation(MySqlModelOptionsExtensions.CharSetAnnotation, charSet.Charset, fromDataAnnotation: true);
        }

        if (Attribute.GetCustomAttribute(member, typeof(MySQLCollationAttribute)) is MySQLCollationAttribute collation)
        {
            propertyBuilder.UseCollation(collation.Collation, fromDataAnnotation: true);
        }
    }
}
