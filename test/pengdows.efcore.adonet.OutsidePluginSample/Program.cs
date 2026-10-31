using Microsoft.EntityFrameworkCore;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;
using OutsidePlugin;

var connection = (fakeDbConnection)new fakeDbFactory(SupportedDatabase.PostgreSql).CreateConnection()!;
connection.ConnectionString = "Data Source=fake";
connection.EnqueueReaderResult(new Dictionary<string, object?>[] { new() { ["Id"] = 1, ["Name"] = "alpha" } });

var options = new DbContextOptionsBuilder<ShelfContext>().UseAdoNetExample(connection).Options;

using var context = new ShelfContext(options);
var shelves = context.Shelves.ToList();

var ok = shelves.Count == 1 && shelves[0].Name == "alpha";
Console.WriteLine(ok ? "OK: an outside plugin queried through the packed core" : "FAIL");
return ok ? 0 : 1;

public sealed class Shelf
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class ShelfContext(DbContextOptions<ShelfContext> options) : DbContext(options)
{
    public DbSet<Shelf> Shelves => Set<Shelf>();
}
