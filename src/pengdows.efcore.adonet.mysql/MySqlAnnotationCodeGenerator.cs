using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     Writes MySQL's annotations, in scaffolded code and migration snapshots, as the fluent methods that set them
///     (HasCharSet, UseCollation, HasTableOption, HasSpatialReferenceSystem, UseMySqlComputedColumn, IsFullText, IsSpatial,
///     HasPrefixLength, UseGuidCollation) instead of HasAnnotation calls. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Design/Internal/MySqlAnnotationCodeGenerator.cs
/// </summary>
internal sealed class MySqlAnnotationCodeGenerator(AnnotationCodeGeneratorDependencies dependencies) : AnnotationCodeGenerator(dependencies)
{
    private static readonly MethodInfo ModelHasCharSet = Method(nameof(MySqlModelOptionsExtensions.HasCharSet), typeof(ModelBuilder), typeof(string), typeof(DelegationModes?));
    private static readonly MethodInfo ModelUseCollation = Method(nameof(MySqlModelOptionsExtensions.UseCollation), typeof(ModelBuilder), typeof(string), typeof(DelegationModes?));
    private static readonly MethodInfo ModelUseGuidCollation = Method(nameof(MySqlModelOptionsExtensions.UseGuidCollation), typeof(ModelBuilder), typeof(string));
    private static readonly MethodInfo EntityHasCharSet = Method(nameof(MySqlModelOptionsExtensions.HasCharSet), typeof(EntityTypeBuilder), typeof(string), typeof(DelegationModes?));
    private static readonly MethodInfo EntityUseCollation = Method(nameof(MySqlModelOptionsExtensions.UseCollation), typeof(EntityTypeBuilder), typeof(string), typeof(DelegationModes?));
    private static readonly MethodInfo EntityHasTableOption = Method(nameof(MySqlModelOptionsExtensions.HasTableOption), typeof(EntityTypeBuilder), typeof(string), typeof(string));
    private static readonly MethodInfo PropertyHasCharSet = Method(nameof(MySqlModelOptionsExtensions.HasCharSet), typeof(PropertyBuilder), typeof(string));
    private static readonly MethodInfo PropertyHasSrid = Method(nameof(MySqlModelOptionsExtensions.HasSpatialReferenceSystem), typeof(PropertyBuilder), typeof(int?));
    private static readonly MethodInfo PropertyUseComputedColumn = Method(nameof(MySqlModelOptionsExtensions.UseMySqlComputedColumn), typeof(PropertyBuilder));
    private static readonly MethodInfo PropertyUseIdentityColumn = Method(nameof(MySqlModelOptionsExtensions.UseMySqlIdentityColumn), typeof(PropertyBuilder));
    private static readonly MethodInfo ModelAutoIncrementColumns = Method(nameof(MySqlModelOptionsExtensions.AutoIncrementColumns), typeof(ModelBuilder));
    private static readonly MethodInfo IndexHasPrefixLength = Method(nameof(MySqlModelOptionsExtensions.HasPrefixLength), typeof(IndexBuilder), typeof(int[]));
    private static readonly MethodInfo IndexIsSpatial = Method(nameof(MySqlModelOptionsExtensions.IsSpatial), typeof(IndexBuilder), typeof(bool));
    private static readonly MethodInfo KeyHasPrefixLength = Method(nameof(MySqlModelOptionsExtensions.HasPrefixLength), typeof(KeyBuilder), typeof(int[]));
    private static readonly MethodInfo IndexIsFullText = typeof(MySqlFullTextExtensions).GetMethod(
        nameof(MySqlFullTextExtensions.IsFullText), [typeof(IndexBuilder), typeof(bool), typeof(string)])!;

    // A delegation for the model's collation goes with it into our UseCollation; without one, EF's own UseCollation.
    public override IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(IModel model, IDictionary<string, IAnnotation> annotations)
    {
        var calls = new List<MethodCallCodeFragment>();
        if (annotations.Remove(MySqlModelOptionsExtensions.CollationDelegationAnnotation, out var collationDelegation))
        {
            annotations.Remove(MySqlModelOptionsExtensions.CollationAnnotation, out var collation);
            calls.Add(new MethodCallCodeFragment(ModelUseCollation, collation?.Value, collationDelegation.Value));
        }

        calls.AddRange(base.GenerateFluentApiCalls(model, annotations));
        Delegated(annotations, MySqlModelOptionsExtensions.CharSetAnnotation, MySqlModelOptionsExtensions.CharSetDelegationAnnotation, ModelHasCharSet, calls);
        if (annotations.Remove(MySqlModelOptionsExtensions.ValueGenerationStrategyAnnotation, out _))
        {
            calls.Add(new MethodCallCodeFragment(ModelAutoIncrementColumns));
        }

        if (annotations.Remove(MySqlModelOptionsExtensions.GuidCollationAnnotation, out var guidCollation))
        {
            calls.Add(new MethodCallCodeFragment(ModelUseGuidCollation, guidCollation.Value));
        }

        return calls;
    }

    public override IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(IEntityType entityType, IDictionary<string, IAnnotation> annotations)
    {
        var calls = base.GenerateFluentApiCalls(entityType, annotations).ToList();
        Delegated(annotations, MySqlModelOptionsExtensions.CharSetAnnotation, MySqlModelOptionsExtensions.CharSetDelegationAnnotation, EntityHasCharSet, calls);
        Delegated(annotations, MySqlModelOptionsExtensions.CollationAnnotation, MySqlModelOptionsExtensions.CollationDelegationAnnotation, EntityUseCollation, calls);
        if (annotations.Remove(MySqlModelOptionsExtensions.StoreOptionsAnnotation, out var options))
        {
            calls.AddRange(MySqlTableOptions.Deserialize(options.Value as string)
                .Select(o => new MethodCallCodeFragment(EntityHasTableOption, o.Key, o.Value)));
        }

        return calls;
    }

    public override IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(IProperty property, IDictionary<string, IAnnotation> annotations)
    {
        var calls = base.GenerateFluentApiCalls(property, annotations).ToList();
        if (annotations.Remove(MySqlModelOptionsExtensions.CharSetAnnotation, out var charSet))
        {
            calls.Add(new MethodCallCodeFragment(PropertyHasCharSet, charSet.Value));
        }

        if (annotations.Remove(MySqlModelOptionsExtensions.SpatialReferenceSystemAnnotation, out var srid))
        {
            calls.Add(new MethodCallCodeFragment(PropertyHasSrid, srid.Value));
        }

        if (annotations.Remove(MySqlModelOptionsExtensions.ValueGenerationStrategyAnnotation, out var strategy))
        {
            calls.Add(new MethodCallCodeFragment(
                strategy.Value is MySqlModelOptionsExtensions.ComputedColumnStrategy ? PropertyUseComputedColumn : PropertyUseIdentityColumn));
        }

        return calls;
    }

    public override IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(IIndex index, IDictionary<string, IAnnotation> annotations)
    {
        var calls = base.GenerateFluentApiCalls(index, annotations).ToList();
        annotations.Remove(MySqlFullTextExtensions.FullTextParserAnnotation, out var parser);
        if (annotations.Remove(MySqlFullTextExtensions.FullTextAnnotation, out var fullText) && (fullText.Value is true))
        {
            calls.Add(new MethodCallCodeFragment(IndexIsFullText, true, parser?.Value));
        }

        if (annotations.Remove(MySqlModelOptionsExtensions.SpatialAnnotation, out var spatial) && (spatial.Value is true))
        {
            calls.Add(new MethodCallCodeFragment(IndexIsSpatial, true));
        }

        if (annotations.Remove(MySqlModelOptionsExtensions.PrefixLengthAnnotation, out var prefixLengths))
        {
            calls.Add(new MethodCallCodeFragment(IndexHasPrefixLength, prefixLengths.Value));
        }

        return calls;
    }

    public override IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(IKey key, IDictionary<string, IAnnotation> annotations)
    {
        var calls = base.GenerateFluentApiCalls(key, annotations).ToList();
        if (annotations.Remove(MySqlModelOptionsExtensions.PrefixLengthAnnotation, out var prefixLengths))
        {
            calls.Add(new MethodCallCodeFragment(KeyHasPrefixLength, prefixLengths.Value));
        }

        return calls;
    }

    // A value and its delegation are one call; a delegation alone keeps the level above's from the levels it names.
    private static void Delegated(
        IDictionary<string, IAnnotation> annotations,
        string valueAnnotation,
        string delegationAnnotation,
        MethodInfo method,
        List<MethodCallCodeFragment> calls)
    {
        var hasValue = annotations.Remove(valueAnnotation, out var value);
        var hasDelegation = annotations.Remove(delegationAnnotation, out var delegation);
        if (hasValue || hasDelegation)
        {
            calls.Add(new MethodCallCodeFragment(method, value?.Value, delegation?.Value));
        }
    }

    private static MethodInfo Method(string name, params Type[] parameters)
        => typeof(MySqlModelOptionsExtensions).GetMethod(name, parameters)
            ?? throw new InvalidOperationException($"MySqlModelOptionsExtensions.{name} has no overload for {string.Join(", ", parameters.Select(p => p.Name))}.");
}
