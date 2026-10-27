// Portions adapted from Npgsql's BasicTypesQueryNpgsqlFixture.
// Copyright (c) 2002-2025, Npgsql. Licensed under the PostgreSQL License.

using Microsoft.EntityFrameworkCore.Query.Translations;
using Microsoft.EntityFrameworkCore.TestModels.BasicTypesModel;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     EF's basic types data, with each date and time cut to the database's resolution (and each offset zeroed where the
///     database keeps none) before it is seeded and expected, so a value reads back as it was written. EF's data holds
///     100-nanosecond ticks, finer than most databases keep. Adapted from Npgsql's BasicTypesQueryNpgsqlFixture (PostgreSQL
///     License):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/test/EFCore.PG.FunctionalTests/Query/Translations/BasicTypesQueryNpgsqlFixture.cs
/// </summary>
public abstract class BasicTypesAdoNetFixture<THost> : BasicTypesQueryFixtureBase, ITestSqlLoggerFactory
    where THost : IDatabaseHost, new()
{
    public TestSqlLoggerFactory TestSqlLoggerFactory
        => (TestSqlLoggerFactory)ListLoggerFactory;

    private BasicTypesData? _expectedData;

    protected override Task SeedAsync(BasicTypesContext context)
    {
        var data = (BasicTypesData)GetExpectedData();
        context.AddRange(data.BasicTypesEntities);
        context.AddRange(data.NullableBasicTypesEntities);
        return context.SaveChangesAsync();
    }

    public override ISetSource GetExpectedData()
        => _expectedData ??= AtResolution((BasicTypesData)base.GetExpectedData(), new THost());

    // Where the database keeps only a DateTimeOffset's instant, normalize the expected value to that instant at offset zero.
    private static BasicTypesData AtResolution(BasicTypesData data, THost host)
    {
        var ticks = host.TemporalResolution.Ticks;
        var offsets = host.KeepsOffsets;
        foreach (var item in data.BasicTypesEntities)
        {
            item.DateTime = new DateTime(Cut(item.DateTime.Ticks, ticks), item.DateTime.Kind);
            item.TimeOnly = new TimeOnly(Cut(item.TimeOnly.Ticks, ticks));
            item.TimeSpan = new TimeSpan(Cut(item.TimeSpan.Ticks, ticks));
            item.DateTimeOffset = offsets
                ? new DateTimeOffset(Cut(item.DateTimeOffset.Ticks, ticks), item.DateTimeOffset.Offset)
                : new DateTimeOffset(Cut(item.DateTimeOffset.UtcDateTime.Ticks, ticks), TimeSpan.Zero);
        }

        foreach (var item in data.NullableBasicTypesEntities)
        {
            if (item.DateTime is { } dateTime)
            {
                item.DateTime = new DateTime(Cut(dateTime.Ticks, ticks), dateTime.Kind);
            }

            if (item.TimeOnly is { } timeOnly)
            {
                item.TimeOnly = new TimeOnly(Cut(timeOnly.Ticks, ticks));
            }

            if (item.TimeSpan is { } timeSpan)
            {
                item.TimeSpan = new TimeSpan(Cut(timeSpan.Ticks, ticks));
            }

            if (item.DateTimeOffset is { } offset)
            {
                item.DateTimeOffset = offsets
                    ? new DateTimeOffset(Cut(offset.Ticks, ticks), offset.Offset)
                    : new DateTimeOffset(Cut(offset.UtcDateTime.Ticks, ticks), TimeSpan.Zero);
            }
        }

        return data;
    }

    // Toward zero, so a negative span keeps its sign.
    private static long Cut(long value, long ticks)
        => value - (value % ticks);
}
