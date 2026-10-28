using System.Data.Common;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet.Scaffolding;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     Reverse engineering reads through the dialect's schema reader, which may read any catalog: ISO's INFORMATION_SCHEMA
///     is one reader, not the contract (SQLite's catalog is sqlite_master and PRAGMAs).
/// </summary>
public class SchemaReaderTests
{
    [Fact]
    public void A_dialects_own_catalog_reader_reads_the_model()
    {
        var dialect = new CatalogDialect();
        using var connection = new fakeDbConnection { ConnectionString = "Data Source=fake" };

        var model = new AdoNetDatabaseModelFactory(dialect).Create(connection, new DatabaseModelFactoryOptions());

        Assert.Equal(["from the catalog"], model.Tables.Select(t => t.Name));
    }

    [Fact]
    public void A_catalog_delete_rule_reads_as_efs_referential_action()
        => Assert.Equal(
            [Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade, Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetNull, null],
            CatalogReader.Actions(["cascade", "SET NULL", "'; DROP TABLE x; --"]));

    private sealed class CatalogDialect : AdoNetDialect
    {
        public override string Name => "Catalog";
        public override SchemaReader SchemaReader { get; } = new CatalogReader();
    }

    private sealed class CatalogReader : SchemaReader
    {
        public override DatabaseModel Read(DbConnection connection, DatabaseModelFactoryOptions options, ISqlDialect sql)
        {
            var model = new DatabaseModel();
            model.Tables.Add(new DatabaseTable { Database = model, Name = "from the catalog" });
            return model;
        }

        public static List<Microsoft.EntityFrameworkCore.Migrations.ReferentialAction?> Actions(string[] rules)
            => rules.Select(r => new CatalogReader().DeleteAction(r)).ToList();
    }
}
