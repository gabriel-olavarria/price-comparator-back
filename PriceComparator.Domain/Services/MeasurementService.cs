using System.Text.RegularExpressions;
using PriceComparator.Domain.Enums;
using PriceComparator.Domain.Extensions;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Domain.Services;

public static class MeasurementService  
{
    public static Measurement? Extract(string name)
    {
        var unitType = DetectUnitType(name);
        return unitType switch
        {
            UnitType.Volume => ExtractVolume(name),
            UnitType.Weight => ExtractWeight(name),
            UnitType.Units => ExtractUnits(name),
            _ => null
        };
    }
    
    private static UnitType? DetectUnitType(string name)
    {
        if (Regex.IsMatch(name, @"\bpack\b|\b\d+\s*(?:unidades?|un|uds?)\.?(?!\w)", RegexOptions.IgnoreCase))
        {
            return UnitType.Units;
        }
        
        if (Regex.IsMatch(name, @"\b\d+(?:[.,]\d+)?\s*(ml|cc|l|lt|lts|litro|litros)\b", RegexOptions.IgnoreCase))
        {
            return UnitType.Volume;
        }

        if (Regex.IsMatch(name, @"\b\d+(?:[.,]\d+)?\s*(kg|gr|g|gramos?)\b", RegexOptions.IgnoreCase))
        {
            return UnitType.Weight;
        }

        

        return null;
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
        var isLiter = rawUnit is "l" or "lt" or "lts" or "litro" or "litros";
        var displayUnit = isLiter ? Unit.L : Unit.Ml;

        return new Measurement(
            UnitType.Volume,
            1,
            isLiter ? value * 1000 : value,
            Unit.Ml,
            value,
            displayUnit,
            displayUnit.GetDescription(),
            $"{value} {displayUnit.GetDescription()}",
            $"{value} {displayUnit}"
        );
    }

    private static Measurement? ExtractWeight(string name)
    {
        var weightMatch = Regex.Match(
            name,
            @"\b(\d+(?:[.,]\d+)?)\s*(mg|g|gr|grs|kg)\b",
            RegexOptions.IgnoreCase
        );

        if (!weightMatch.Success)
            return null;

        var value = NormalizerService.ParseNumber(weightMatch.Groups[1].Value);
        var rawUnit = weightMatch.Groups[2].Value.ToLowerInvariant();

        var (normalizedValue, displayUnit) = rawUnit switch
        {
            "kg" => (value * 1000, Unit.Kg),
            "mg" => (value / 1000, Unit.Mg),
            _    => (value, Unit.G)
        };

        var unitName = displayUnit.GetDescription();

        return new Measurement(
            UnitType.Weight,
            1,
            normalizedValue,
            Unit.G,
            value,
            displayUnit,
            unitName,
            $"{value} {unitName}",
            $"{value} {displayUnit}"
        );
    }
    
    private static Measurement? ExtractUnits(string name)
    {
        var unitsMatch = Regex.Match(
            name,
            @"\b(?<quantity>\d+)\s*(?<unitText>unidades?|un|uds?)\.?(?:\s+(?<connector>de)\b)?",
            RegexOptions.IgnoreCase
        );
        
        if (!unitsMatch.Success)
        {
            return null;
        }

        var quantity = int.Parse(unitsMatch.Groups[1].Value);
        // Detectar contenido por unidad
        var content = ExtractVolume(name) ?? ExtractWeight(name);

        if (content is null)
            return null;
        
        var value = content.QuantityUnit;
        var displayUnit = content.DisplayUnit;
        var unitName = displayUnit.GetDescription();
        var unitText = unitsMatch.Groups["unitText"].Value.ToLowerInvariant();
        var connector = unitsMatch.Groups["connector"].Success ? " de" : "";
        var quantityText = $"{quantity} {unitText}{connector}";
        var displayContentName = name.Contains("pack") ? "Pack" : "Display"; 
        return new Measurement(
            UnitType.Units,
            quantity,
            content.QuantityUnitValue,
            content.QuantityUnit,
            content.Quantity,
            displayUnit,
            unitName,
            $"{quantityText} {content.QuantityUnitValue} {displayUnit.GetDescription().ToLowerInvariant()}",
            $"{quantityText} {content.QuantityUnitValue} {displayUnit.ToString().ToLowerInvariant()}",
            displayContentName
        );
    }
}