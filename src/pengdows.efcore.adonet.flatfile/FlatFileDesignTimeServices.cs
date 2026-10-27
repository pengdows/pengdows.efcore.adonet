using Microsoft.EntityFrameworkCore.Design;
using pengdows.efcore.adonet.Design;
using pengdows.efcore.adonet.flatfile;

[assembly: DesignTimeProviderServices("pengdows.efcore.adonet.flatfile.FlatFileDesignTimeServices")]

namespace pengdows.efcore.adonet.flatfile;

/// <summary>The provider's design-time services for pengdows.flatfile.</summary>
public class FlatFileDesignTimeServices() : AdoNetDesignTimeServices(FlatFileDialect.Instance);
