using System.Text.RegularExpressions;
using PriceComparator.Domain.Enums;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Domain.Services;

public static class MeasurementService  
{
    public static Measurement Extract(string name)
    {
        /*
         * ========================================================
         * VOLUMEN
         * ========================================================
         */

        var volume = ExtractVolume(name);

        if (volume is not null)
        {
            return volume;
        }

        /*
         * ========================================================
         * PESO
         * ========================================================
         */

        var weight = ExtractWeight(name);

        if (weight is not null)
        {
            return weight;
        }
        /*
         * ========================================================
         * UNIDADES
         * ========================================================
         */

        var units = ExtractUnits(name);

        if (units is not null)
        {
            return units;
        }
        /*
         * No fue posible detectar medida.
         */

        return new Measurement(null, 0, null, null);
    }
    
    private static Measurement? ExtractVolume(string name)
    {
        var volumeMatch = Regex.Match(name, @"\b(\d+(?:[.,]\d+)?)\s*(ml|cc|l|lt|lts|litro|litros)\b");

        if (!volumeMatch.Success)
        {
            return null;
        }

        var value = NormalizerService.ParseNumber(volumeMatch.Groups[1].Value);
        var rawUnit = volumeMatch.Groups[2].Value;

        if (rawUnit is "l" or "lt" or "lts" or "litro" or "litros")
        {
            return new Measurement(UnitType.Volume, 1, value * 1000, Unit.Ml);
        }

        return new Measurement(UnitType.Volume, 1, value, Unit.Ml);
    }
    
    private static Measurement? ExtractWeight(string name)
    {
        var weightMatch = Regex.Match(name, @"\b(\d+(?:[.,]\d+)?)\s*(mg|g|gr|grs|kg)\b");

        if (!weightMatch.Success)
        {
            return null;
        }

        var value = NormalizerService.ParseNumber(weightMatch.Groups[1].Value);
        var rawUnit = weightMatch.Groups[2].Value;

        return rawUnit switch
        {
            "kg" => new Measurement(UnitType.Weight, 1, value * 1000, Unit.G),
            "mg" => new Measurement(UnitType.Weight, 1, value / 1000, Unit.G),
            _ => new Measurement(UnitType.Weight, 1, value, Unit.G)
        };
    }
    
    private static Measurement? ExtractUnits(string name)
    {
        var unitsMatch = Regex.Match(name, @"\b(\d+)\s*(un|unidad|unidades)\b");

        if (!unitsMatch.Success)
        {
            return null;
        }

        var quantity = int.Parse(unitsMatch.Groups[1].Value);

        return new Measurement(UnitType.Units, quantity, null, null);
    }
}