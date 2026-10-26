// Portions adapted from Pomelo's BuiltInDataTypesMySqlTest.
// Copyright (c) Pomelo Foundation. Licensed under the MIT license.

namespace pengdows.efcore.adonet.Specification.Tests;

// EF's built-in data types model makes MaxLengthDataTypes.StringUnbounded unbounded and then, immediately after, 9,000
// characters (Pomelo's fixture calls it a bug in the test's fluent configuration). Beside String9000 that is two 9,000-
// character columns, more than a MySQL row holds (65,535 bytes). Reset to unbounded, as Pomelo's fixture does. Adapted
// from Pomelo's BuiltInDataTypesMySqlTest (MIT, Pomelo Foundation):
// https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/test/EFCore.MySql.FunctionalTests/BuiltInDataTypesMySqlTest.cs

internal static class UnboundedString
{
    // The entity type is nested in EF's generic test base, so it is found by name.
    public static void Reset(ModelBuilder modelBuilder)
        => modelBuilder.Model.GetEntityTypes().SingleOrDefault(e => e.ClrType.Name == "MaxLengthDataTypes")
            ?.FindProperty("StringUnbounded")?.SetMaxLength(null);
}

public abstract partial class BuiltInDataTypesAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            UnboundedString.Reset(modelBuilder);
        }
    }
}

public abstract partial class CustomConvertersAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            UnboundedString.Reset(modelBuilder);
        }
    }
}

public abstract partial class ConvertToProviderTypesAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            UnboundedString.Reset(modelBuilder);
        }
    }
}
