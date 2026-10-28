namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     A primitive collection stored as the database's own array or list type, not as JSON: a plugin's type mapping implements
///     it, with <see cref="Microsoft.EntityFrameworkCore.Storage.CoreTypeMapping.ElementTypeMapping" /> set. Each member is a
///     template, as <see cref="FunctionSyntax" />'s are; a null function leaves that operation to a query over
///     <see cref="Elements" />.
/// </summary>
public interface IArrayTypeMapping
{
    /// <summary>
    ///     The elements of the array <c>{0}</c> as a table, as <see cref="JsonSyntax.ArrayElements" />: a column named <c>{1}</c>
    ///     numbering them from 1 in order, and one named <c>{2}</c> holding each element as the store type <c>{3}</c>.
    /// </summary>
    string Elements { get; }

    /// <summary>Whether the array <c>{0}</c> holds the element <c>{1}</c>.</summary>
    string? Contains { get; }

    /// <summary>The number of elements of the array <c>{0}</c>.</summary>
    string? Count { get; }

    /// <summary>The element of the array <c>{0}</c> at the 0-based position <c>{1}</c>, or null past its end.</summary>
    string? ElementAt { get; }
}
