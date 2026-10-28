using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     A model that maps an entity type's inserts, updates or deletes to stored procedures is refused when the context starts,
///     naming the entity type and the database, where the dialect says the database has none
///     (<see cref="ISqlDialect.SupportsStoredProcedures" />); elsewhere it starts. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Infrastructure/Internal/SqliteModelValidator.cs
/// </summary>
public class StoredProcedureCapabilityTests
{
    public static TheoryData<string> Operations
        => new() { "insert", "update", "delete" };

    [Theory]
    [MemberData(nameof(Operations))]
    public void Mapping_an_operation_to_a_procedure_is_refused_where_the_database_has_none(string operation)
    {
        var database = new FakeDatabase();
        using var context = database.ContextOf<ProcedureContext>(TestDialects.NoStoredProcedures, o => new ProcedureContext(o, operation));

        // Its own type, so a caller (the specification suite's capability skip) can tell the refusal from a model error.
        var error = Assert.Throws<pengdows.efcore.adonet.Infrastructure.StoredProceduresNotSupportedException>(() => context.Model);

        Assert.Contains(nameof(Widget), error.Message);
        Assert.Contains("No stored procedures", error.Message);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public void Mapping_an_operation_to_a_procedure_starts_where_the_database_has_them(string operation)
    {
        var database = new FakeDatabase();
        using var context = database.ContextOf<ProcedureContext>(TestDialects.Ansi, o => new ProcedureContext(o, operation));

        Assert.NotNull(context.Model.FindEntityType(typeof(Widget)));
    }

    [Fact]
    public void A_model_without_procedures_starts_where_the_database_has_none()
    {
        var database = new FakeDatabase();
        using var context = database.ContextOf<ProcedureContext>(TestDialects.NoStoredProcedures, o => new ProcedureContext(o, null));

        Assert.NotNull(context.Model.FindEntityType(typeof(Widget)));
    }

    public sealed class Widget
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public sealed class ProcedureContext(DbContextOptions<ProcedureContext> options, string? operation) : DbContext(options)
    {
        public DbSet<Widget> Widgets => Set<Widget>();

        public string? Operation => operation;

        // A model per operation, not one per context type.
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.ReplaceService<IModelCacheKeyFactory, OperationModelCacheKeyFactory>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var widget = modelBuilder.Entity<Widget>();
            switch (operation)
            {
                case "insert":
                    widget.InsertUsingStoredProcedure("Widget_Insert", p => p.HasParameter(w => w.Name).HasResultColumn(w => w.Id));
                    break;
                case "update":
                    widget.UpdateUsingStoredProcedure("Widget_Update", p => p.HasOriginalValueParameter(w => w.Id).HasParameter(w => w.Name));
                    break;
                case "delete":
                    widget.DeleteUsingStoredProcedure("Widget_Delete", p => p.HasOriginalValueParameter(w => w.Id));
                    break;
            }
        }
    }

    private sealed class OperationModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => (context.GetType(), ((ProcedureContext)context).Operation, designTime);
    }
}
