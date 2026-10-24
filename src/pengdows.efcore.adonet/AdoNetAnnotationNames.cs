namespace pengdows.efcore.adonet;

public static class AdoNetAnnotationNames
{
    public const string Prefix = "AdoNet:";

    /// <summary>
    ///     An integer key column the database generates. The migrations SQL generator writes it as the dialect's identity
    ///     column, or, for <see cref="SequenceKeyRetrieval" />, creates the key's sequence with the table.
    /// </summary>
    public const string Identity = Prefix + "Identity";

    /// <summary>
    ///     On a dropped or renamed table: its identity columns (<c>string[]</c>), so a <see cref="SequenceKeyRetrieval" />
    ///     dialect can drop or rename their sequences along with it.
    /// </summary>
    public const string IdentityColumns = Prefix + "IdentityColumns";
}
