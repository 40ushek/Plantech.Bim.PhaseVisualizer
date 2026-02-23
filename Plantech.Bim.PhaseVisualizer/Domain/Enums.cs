using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Plantech.Bim.PhaseVisualizer.Domain;

[JsonConverter(typeof(StringEnumConverter))]
internal enum PhaseValueType
{
    String,
    Number,
    Boolean,
    Integer,
    Int = Integer,
}

[JsonConverter(typeof(StringEnumConverter))]
internal enum PhaseAggregateType
{
    First,
    Distinct,
    Count,
    Min,
    Max,
}

[JsonConverter(typeof(StringEnumConverter))]
internal enum PhaseObjectScope
{
    Visible,
}

[JsonConverter(typeof(StringEnumConverter))]
internal enum PhaseColumnObjectType
{
    Phase,
    Part,
    AssemblyMainPart,
}

