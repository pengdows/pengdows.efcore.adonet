using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     Npgsql's model options in DDL: extensions, enum types (and properties mapped to them), collations, index methods, operator
///     classes, covering columns, NULLS NOT DISTINCT, concurrent indexes, identity kinds and options, and sequence defaults.
///     Provider-only: each is PostgreSQL DDL. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Migrations/NpgsqlMigrationsSqlGenerator.cs
/// </summary>
public sealed class PostgreSqlModelOptionsTests
{
    public enum Mood
    {
        Happy,
        SoSo,
        [PgName("very sad")] VerySad
    }

    private sealed class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Email { get; set; }
        public Mood Mood { get; set; }
        public Mood? Previous { get; set; }
        public long Ticket { get; set; }
    }

    private sealed class Order
    {
        public long Id { get; set; }
        public int Number { get; set; }
    }

    private sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<Person> People => Set<Person>();
        public DbSet<Order> Orders => Set<Order>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("pg_trgm");
            modelBuilder.HasPostgresExtension("tools", "hstore", "1.8");
            modelBuilder.HasPostgresEnum<Mood>();
            modelBuilder.HasPostgresEnum("audit", "level", ["low", "it's high"]);
            modelBuilder.HasCollation("german_ci", "de-u-ks-primary", "icu", deterministic: false);
            modelBuilder.Entity<Person>(person =>
            {
                person.Property(p => p.Id).UseIdentityAlwaysColumn().HasIdentityOptions(startValue: 100, incrementBy: 5, cyclic: true);
                person.Property(p => p.Ticket).UseSequence("tickets");
                person.HasIndex(p => p.Name).HasMethod("gin").HasOperators("gin_trgm_ops");
                person.HasIndex(p => p.Email).IsUnique().AreNullsDistinct(false).IncludeProperties(p => new { p.Name, p.Mood });
                person.HasIndex(p => p.Mood).IsCreatedConcurrently();
            });
            modelBuilder.Entity<Order>().Property(o => o.Id).UseIdentityByDefaultColumn();
        }
    }

    private static Context Create()
        => new(new DbContextOptionsBuilder<Context>().UseAdoNetPostgreSql("Host=localhost").Options);

    private static string Script()
    {
        using var context = Create();
        return context.Database.GenerateCreateScript();
    }

    [Fact]
    public void Extensions_are_created_first()
    {
        var script = Script();

        Assert.Contains("CREATE EXTENSION IF NOT EXISTS pg_trgm;", script);
        Assert.Contains("CREATE EXTENSION IF NOT EXISTS hstore SCHEMA tools VERSION \"1.8\";", script);
        Assert.True(script.IndexOf("CREATE EXTENSION", StringComparison.Ordinal) < script.IndexOf("CREATE TABLE", StringComparison.Ordinal));
    }

    [Fact]
    public void Enum_types_are_created_with_translated_labels()
    {
        var script = Script();

        Assert.Contains("CREATE TYPE mood AS ENUM ('happy', 'so_so', 'very sad');", script);
        Assert.Contains("CREATE TYPE audit.level AS ENUM ('low', 'it''s high');", script);
    }

    [Fact]
    public void An_enum_property_is_a_column_of_its_enum_type()
    {
        var script = Script();

        Assert.Contains("\"Mood\" mood NOT NULL", script);
        Assert.Contains("\"Previous\" mood", script);
    }

    [Fact]
    public void An_enum_is_compared_by_its_label_and_bound_untyped()
    {
        using var context = Create();
        var mood = Mood.VerySad;

        var literal = context.People.Where(p => p.Mood == Mood.SoSo).ToQueryString();
        var parameter = context.People.Where(p => p.Previous == mood).ToQueryString();
        var mapping = (RelationalTypeMapping)context.Model.FindEntityType(typeof(Person))!.FindProperty(nameof(Person.Mood))!.GetTypeMapping();
        using var command = new NpgsqlCommand();
        var bound = (NpgsqlParameter)mapping.CreateParameter(command, "p", Mood.VerySad);

        Assert.Contains("p.\"Mood\" = 'so_so'::mood", literal);
        Assert.Contains("p.\"Previous\" = @mood", parameter);
        Assert.Equal("very sad", bound.Value);
        Assert.Equal(NpgsqlDbType.Unknown, bound.NpgsqlDbType);
    }

    [Fact]
    public void Collations_are_created()
    {
        Assert.Contains("CREATE COLLATION german_ci (LOCALE = 'de-u-ks-primary', PROVIDER = icu, DETERMINISTIC = false);", Script());
    }

    [Fact]
    public void Index_method_and_operator_class()
    {
        Assert.Contains("CREATE INDEX \"IX_People_Name\" ON \"People\" USING gin (\"Name\" gin_trgm_ops);", Script());
    }

    [Fact]
    public void Covering_columns_and_nulls_not_distinct()
    {
        Assert.Contains(
            "CREATE UNIQUE INDEX \"IX_People_Email\" ON \"People\" (\"Email\") INCLUDE (\"Name\", \"Mood\") NULLS NOT DISTINCT;",
            Script());
    }

    [Fact]
    public void A_concurrent_index_is_created_outside_the_transaction()
    {
        using var context = Create();
        var generator = context.GetService<IMigrationsSqlGenerator>();
        var operation = new CreateIndexOperation { Name = "IX", Table = "People", Columns = ["Mood"] };
        operation.AddAnnotation("PostgreSql:CreatedConcurrently", true);

        var command = Assert.Single(generator.Generate([operation]));

        Assert.Contains("CREATE INDEX CONCURRENTLY \"IX\" ON \"People\" (\"Mood\");", command.CommandText);
        Assert.True(command.TransactionSuppressed);
        Assert.Contains("CREATE INDEX CONCURRENTLY \"IX_People_Mood\"", Script());
    }

    [Fact]
    public void Identity_kinds_and_options()
    {
        var script = Script();

        Assert.Contains(
            "\"Id\" INTEGER GENERATED ALWAYS AS IDENTITY (START WITH 100 INCREMENT BY 5 CYCLE) NOT NULL",
            script);
        Assert.Contains("\"Id\" BIGINT GENERATED BY DEFAULT AS IDENTITY NOT NULL", script);
    }

    [Fact]
    public void A_model_wide_identity_kind_applies_to_every_identity_column()
    {
        using var context = new AlwaysContext(new DbContextOptionsBuilder<AlwaysContext>().UseAdoNetPostgreSql("Host=localhost").Options);

        Assert.Contains("\"Id\" BIGINT GENERATED ALWAYS AS IDENTITY NOT NULL", context.Database.GenerateCreateScript());
    }

    [Fact]
    public void A_sequence_column_defaults_to_the_sequence()
    {
        var script = Script();

        Assert.Contains("CREATE SEQUENCE tickets", script);
        Assert.Contains("\"Ticket\" BIGINT DEFAULT (nextval('tickets')) NOT NULL", script);
    }

    [Fact]
    public void Adding_an_enum_label_alters_the_type_and_dropping_objects_drops_them()
    {
        using var context = Create();
        var generator = context.GetService<IMigrationsSqlGenerator>();
        var operation = new AlterDatabaseOperation();
        operation.OldDatabase.AddAnnotation("PostgreSql:Enum:.mood", new[] { "happy", "sad" });
        operation.OldDatabase.AddAnnotation("PostgreSql:Collation:.old", "x\u001f\u001f");
        operation.AddAnnotation("PostgreSql:Enum:.mood", new[] { "ecstatic", "happy", "meh", "sad" });

        var commands = generator.Generate([operation]);

        Assert.Contains(commands, c => c.CommandText.Contains("ALTER TYPE mood ADD VALUE 'ecstatic' BEFORE 'happy';") && c.TransactionSuppressed);
        Assert.Contains(commands, c => c.CommandText.Contains("ALTER TYPE mood ADD VALUE 'meh' AFTER 'happy';"));
        Assert.Contains(commands, c => c.CommandText.Contains("DROP COLLATION old;"));
    }

    private sealed class AlwaysContext(DbContextOptions<AlwaysContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.UseIdentityAlwaysColumns();
    }
}
