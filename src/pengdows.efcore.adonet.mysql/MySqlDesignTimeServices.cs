using Microsoft.EntityFrameworkCore.Design;
using pengdows.efcore.adonet.Design;
using pengdows.efcore.adonet.mysql;

[assembly: DesignTimeProviderServices("pengdows.efcore.adonet.mysql.MySqlDesignTimeServices")]

namespace pengdows.efcore.adonet.mysql;

/// <summary>The provider's design-time services for MySQL.</summary>
public class MySqlDesignTimeServices() : AdoNetDesignTimeServices(MySqlDialect.Instance);
