using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>A model for core tests only: every mapped CLR type, nullable columns, keys of each integer width.</summary>
public class Item
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool Active { get; set; }
    public bool? Flag { get; set; }
    public decimal Price { get; set; }

    [ConcurrencyCheck]
    public int Version { get; set; }

    public byte Tiny { get; set; }
    public short Small { get; set; }
    public long Big { get; set; }
    public float Single { get; set; }
    public double Double { get; set; }
    public DateTime At { get; set; }
    public DateTimeOffset AtOffset { get; set; }
    public DateOnly Day { get; set; }
    public TimeOnly Time { get; set; }
    public TimeSpan Span { get; set; }
    public Guid Uid { get; set; }
    public byte[]? Data { get; set; }
    public List<Part> Parts { get; set; } = [];
}

public class Part
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string? Label { get; set; }
    public Item Item { get; set; } = null!;
}

public class LongKeyed
{
    public long Id { get; set; }
    public string? Name { get; set; }
}

public class ShortKeyed
{
    public short Id { get; set; }
    public string? Name { get; set; }
}

/// <summary>A non-key column the database fills in.</summary>
public class Defaulted
{
    public int Id { get; set; }
    public int Counter { get; set; }
}

public class ItemContext(DbContextOptions<ItemContext> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<LongKeyed> LongKeyed => Set<LongKeyed>();
    public DbSet<ShortKeyed> ShortKeyed => Set<ShortKeyed>();
    public DbSet<Defaulted> Defaulted => Set<Defaulted>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Defaulted>().Property(d => d.Counter).HasDefaultValueSql("0");
}
