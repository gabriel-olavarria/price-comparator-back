using System.ComponentModel;

namespace PriceComparator.Domain.Enums;

public enum Unit
{
    [Description("Litro")]
    L,

    [Description("Mililitro")]
    Ml,

    [Description("Kilogramo")]
    Kg,

    [Description("Gramo")]
    G,

    [Description("Miligramo")]
    Mg
}