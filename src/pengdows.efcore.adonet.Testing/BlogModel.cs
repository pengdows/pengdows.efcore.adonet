using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Testing;

public class Blog
{
    public int Id { get; set; }

    [ConcurrencyCheck]
    public string Name { get; set; } = "";

    public bool IsActive { get; set; }
    public decimal Rating { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid ExternalId { get; set; }
    public List<Post> Posts { get; set; } = [];
}

public class Post
{
    public int Id { get; set; }
    public int BlogId { get; set; }
    public string Title { get; set; } = "";
    public Blog Blog { get; set; } = null!;
}

/// <summary>Nothing but a generated key: an insert has no columns to write.</summary>
public class Tally
{
    public int Id { get; set; }
}

public class BlogContext(DbContextOptions<BlogContext> options) : DbContext(options)
{
    public DbSet<Blog> Blogs => Set<Blog>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Tally> Tallies => Set<Tally>();
}
