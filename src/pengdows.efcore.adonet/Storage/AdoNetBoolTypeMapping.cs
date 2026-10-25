using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Storage;

internal class AdoNetBoolTypeMapping : BoolTypeMapping
{
    private readonly string _trueLiteral;
    private readonly string _falseLiteral;

    public AdoNetBoolTypeMapping(string storeType, string trueLiteral, string falseLiteral)
        : base(storeType)
    {
        _trueLiteral = trueLiteral;
        _falseLiteral = falseLiteral;
    }

    protected AdoNetBoolTypeMapping(RelationalTypeMappingParameters parameters, string trueLiteral, string falseLiteral)
        : base(parameters)
    {
        _trueLiteral = trueLiteral;
        _falseLiteral = falseLiteral;
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetBoolTypeMapping(parameters, _trueLiteral, _falseLiteral);

    protected override string GenerateNonNullSqlLiteral(object value)
        => (bool)value ? _trueLiteral : _falseLiteral;
}
