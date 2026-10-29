using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     Inserts, updates and deletes mapped to stored procedures: ISO's <c>CALL</c> with the driver's output parameters, or,
///     where the dialect reads outputs through session variables, the variables set, passed and selected back.
/// </summary>
public class ProcedureCallTests
{
    private static readonly IAdoNetDialect Variables = new PartsDialect(
        "Procedure output variables",
        new PartsSql(keys: KeyRetrieval.SelectAfter("last_id()", "row_count()"), procedureOutputVariable: "@{0}"),
        new DatabaseCapabilities { SupportsMultipleStatementsPerCommand = true, DefaultMaxBatchSize = 10 });

    private readonly FakeDatabase _db = new();

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private ProcContext Context(IAdoNetDialect dialect)
        => _db.ContextOf<ProcContext>(dialect, o => new ProcContext(o));

    [Fact]
    public async Task Iso_calls_the_procedure_with_its_output_as_a_parameter()
    {
        await using var context = Context(TestDialects.Ansi);
        context.Add(new Proc { Name = "a" });

        // fakeDb returns no output parameter values.
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));

        Assert.Equal("CALL \"Proc_Insert\"(@p0, @p1, @p2);", Assert.Single(_db.Commands));
    }

    [Fact]
    public async Task Variables_carry_an_output_out_of_the_call_and_back_in_a_select()
    {
        await using var context = Context(Variables);
        var proc = new Proc { Name = "a" };
        context.Add(proc);
        _db.Returns(FakeDatabase.Row("_out_p0", 7));

        await context.SaveChangesAsync(Ct);

        Assert.Equal(7, proc.Id);
        Assert.Equal("SET @_out_p0 = NULL;\nCALL \"Proc_Insert\"(@p1, @p2, @_out_p0);\nSELECT @_out_p0;", Assert.Single(_db.Commands));
        Assert.Equal("@p1='a', @p2='0'", Assert.Single(_db.Parameters));
    }

    [Fact]
    public async Task Variables_start_an_input_output_parameter_at_its_value()
    {
        await using var context = Context(Variables);
        var proc = new Proc { Id = 1, Name = "a", Version = 3 };
        context.Attach(proc);
        proc.Name = "b";
        _db.Returns(new Dictionary<string, object?> { ["_out_p0"] = 4, ["_out_p1"] = 1 });

        await context.SaveChangesAsync(Ct);

        Assert.Equal(4, proc.Version);
        Assert.Equal(
            "SET @_out_p0 = @p0;\nSET @_out_p1 = NULL;\nCALL \"Proc_Update\"(@p2, @p3, @_out_p0, @_out_p1);\n"
            + "SELECT @_out_p0, @_out_p1\nWHERE @_out_p1 = 1;",
            Assert.Single(_db.Commands));
        Assert.Equal("@p2='1', @p3='b', @p0='3'", Assert.Single(_db.Parameters));
    }

    [Fact]
    public async Task Variables_report_a_concurrency_failure_when_no_row_was_affected()
    {
        await using var context = Context(Variables);
        var proc = new Proc { Id = 1, Name = "a", Version = 3 };
        context.Attach(proc);
        proc.Name = "b";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Variables_read_a_rows_affected_output_alone()
    {
        await using var context = Context(Variables);
        context.Remove(new Proc { Id = 1, Version = 3 });
        _db.Returns(FakeDatabase.Row("_out_p0", 0));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(Ct));
        Assert.Equal("SET @_out_p0 = NULL;\nCALL \"Proc_Delete\"(@p1, @p2, @_out_p0);\nSELECT @_out_p0;", Assert.Single(_db.Commands));
    }

    [Fact]
    public void Connection_options_are_added_to_the_connection_string_connections_are_made_from()
    {
        var factory = new RecordingFactory();
        using var context = OptionsContext(b => b.UseAdoNet(factory, "Data Source=a", WithOptions));

        context.Database.GetDbConnection();

        Assert.Equal("Data Source=a", context.Database.GetConnectionString());
        Assert.Equal(["Data Source=a;Mode=Memory"], factory.DataSourcesCreated, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Connection_options_are_added_to_a_closed_connection()
    {
        using var connection = new fakeDbConnection { ConnectionString = "Data Source=a" };
        using var context = OptionsContext(b => b.UseAdoNet(connection, contextOwnsConnection: false, o => o.UseDialect(WithOptions)));

        context.Database.GetDbConnection();

        Assert.Equal("Data Source=a;Mode=Memory", connection.ConnectionString, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_connection_that_has_the_options_is_left_as_it_is()
    {
        using var connection = new fakeDbConnection { ConnectionString = "Data Source=a;mode=memory" };
        using var context = OptionsContext(b => b.UseAdoNet(connection, contextOwnsConnection: false, o => o.UseDialect(WithOptions)));

        context.Database.GetDbConnection();

        Assert.Equal("Data Source=a;mode=memory", connection.ConnectionString);
    }

    [Fact]
    public void An_open_connection_without_the_options_is_refused()
    {
        using var connection = new fakeDbConnection { ConnectionString = "Data Source=:memory:" };
        connection.Open();
        using var context = OptionsContext(b => b.UseAdoNet(connection, contextOwnsConnection: false, o => o.UseDialect(WithOptions)));

        var refused = Assert.Throws<InvalidOperationException>(() => context.Database.GetDbConnection());

        Assert.Contains("Mode=Memory", refused.Message);
    }

    [Fact]
    public void A_data_source_without_the_options_is_refused()
    {
        using var dataSource = new RecordingFactory().CreateDataSource("Data Source=a");
        using var context = OptionsContext(b => b.UseAdoNet(dataSource, WithOptions));

        var refused = Assert.Throws<InvalidOperationException>(() => context.Database.GetDbConnection());

        Assert.Contains("Mode=Memory", refused.Message);
    }

    private static readonly IAdoNetDialect WithOptions = new PartsDialect(
        "Connection options", new PartsSql(connectionOptions: new Dictionary<string, string> { ["Mode"] = "Memory" }));

    private static BlogContext OptionsContext(Func<DbContextOptionsBuilder<BlogContext>, DbContextOptionsBuilder> configure)
        => new((DbContextOptions<BlogContext>)configure(new DbContextOptionsBuilder<BlogContext>()).AllowManyDialects().Options);

    private sealed class RecordingFactory : fakeDbFactory
    {
        public RecordingFactory()
            : base(SupportedDatabase.Unknown)
        {
        }

        public List<string> DataSourcesCreated { get; } = [];

        public override DbDataSource CreateDataSource(string connectionString)
        {
            DataSourcesCreated.Add(connectionString);
            return base.CreateDataSource(connectionString);
        }
    }

    public class Proc
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Version { get; set; }
    }

    public class ProcContext(DbContextOptions<ProcContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Proc>(e =>
            {
                e.Property(p => p.Version).IsConcurrencyToken().ValueGeneratedOnUpdate();
                e.InsertUsingStoredProcedure("Proc_Insert", s => s.HasParameter(p => p.Name).HasParameter(p => p.Version).HasParameter(p => p.Id, p => p.IsOutput()));
                e.UpdateUsingStoredProcedure(
                    "Proc_Update",
                    s => s.HasOriginalValueParameter(p => p.Id)
                        .HasParameter(p => p.Name)
                        .HasOriginalValueParameter(p => p.Version, p => p.IsInputOutput())
                        .HasRowsAffectedParameter());
                e.DeleteUsingStoredProcedure(
                    "Proc_Delete", s => s.HasOriginalValueParameter(p => p.Id).HasOriginalValueParameter(p => p.Version).HasRowsAffectedParameter());
            });
    }
}
