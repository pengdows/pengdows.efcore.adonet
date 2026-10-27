using System.Text;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>Provider-neutral invariants for generated DML. Exact quoting and key-retrieval syntax belong to the dialect.</summary>
internal static class UpdateSqlGeneratorAssertions
{
    public static void Delete(StringBuilder sql, bool concurrency)
    {
        var text = sql.ToString();
        Assert.StartsWith("DELETE FROM", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE", text, StringComparison.OrdinalIgnoreCase);
        if (concurrency)
        {
            Assert.Contains("ConcurrencyToken", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void Insert(StringBuilder sql, bool generatedColumns, bool identityOnly = false)
    {
        var text = sql.ToString();
        Assert.StartsWith("INSERT INTO", text, StringComparison.OrdinalIgnoreCase);
        if (!identityOnly)
        {
            Assert.Contains("VALUES", text, StringComparison.OrdinalIgnoreCase);
        }

        if (generatedColumns)
        {
            Assert.NotEmpty(text);
        }
    }

    public static void Update(StringBuilder sql, bool generatedColumns, bool concurrency)
    {
        var text = sql.ToString();
        Assert.StartsWith("UPDATE", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SET", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE", text, StringComparison.OrdinalIgnoreCase);
        if (generatedColumns || concurrency)
        {
            Assert.NotEmpty(text);
        }
    }
}
