using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace pengdows.efcore.adonet.Tests;

/// <summary>A dialect's templates are checked when it is configured, not on the first query that uses one.</summary>
public class TemplateTests
{
    [Theory]
    [InlineData("UPPER({0)")]
    [InlineData("UPPER({a})")]
    [InlineData("UPPER({1})")]
    [InlineData("UPPER({-1})")]
    [InlineData("UPPER({0}})")]
    [InlineData("UPPER({{0})")]
    [InlineData("UPPER({})")]
    public void A_malformed_template_is_refused_when_the_dialect_is_configured(string template)
    {
        var dialect = new PartsDialect("Bad template", new PartsSql(functions: new UpperAs(template)));

        var refused = Assert.Throws<InvalidOperationException>(
            () => new FakeDatabase().Context(dialect).GetService<IAdoNetDialect>());

        Assert.Contains("Upper", refused.Message);
        Assert.Contains(template, refused.Message);
    }

    [Theory]
    [InlineData("{{fn UCASE({0})}}")]
    [InlineData("UPPER({0})")]
    [InlineData("{0}")]
    public void Well_formed_templates_are_accepted(string template)
    {
        var dialect = new PartsDialect("Good template " + template, new PartsSql(functions: new UpperAs(template)));

        using var context = new FakeDatabase().Context(dialect);

        Assert.Same(dialect, context.GetService<IAdoNetDialect>());
    }

    private sealed class UpperAs(string template) : FunctionSyntax
    {
        public override string Upper => template;
    }
}
