// Adapted from EF Core's SQLite NetTopologySuite plugin (SqliteJsonGeometryWktReaderWriter).
// Copyright (c) .NET Foundation. Licensed under the MIT license.

using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.Json;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace pengdows.efcore.adonet.NetTopologySuite;

/// <summary>
///     A geometry inside a JSON document, as its well-known text. Adapted from EF Core's SQLite NetTopologySuite plugin (MIT,
///     .NET Foundation): https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.NTS/Storage/Json/SqliteJsonGeometryWktReaderWriter.cs
/// </summary>
public sealed class JsonGeometryWktReaderWriter : JsonValueReaderWriter<Geometry>
{
    private static readonly WKTReader WktReader = new();
    private static readonly PropertyInfo InstanceProperty = typeof(JsonGeometryWktReaderWriter).GetProperty(nameof(Instance))!;

    public static JsonGeometryWktReaderWriter Instance { get; } = new();

    private JsonGeometryWktReaderWriter()
    {
    }

    public override Geometry FromJsonTyped(ref Utf8JsonReaderManager manager, object? existingObject = null)
        => WktReader.Read(manager.CurrentReader.GetString());

    public override void ToJsonTyped(Utf8JsonWriter writer, Geometry value)
        => writer.WriteStringValue(value.ToText());

    public override Expression ConstructorExpression
        => Expression.Property(null, InstanceProperty);
}
