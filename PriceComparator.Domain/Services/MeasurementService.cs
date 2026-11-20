using System.Globalization;
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
        var volumeMatch = Regex.Match(
            name,
            @"\b(\d+(?:[.,]\d+)?)\s*(ml|cc|l|lt|lts|litro|litros)\b",
            RegexOptions.IgnoreCase
        );

        if (!volumeMatch.Success)
            return null;

        // Valor original: "2,5" o "2.5"
        var originalValue = volumeMatch.Groups[1].Value;

        // Valor normalizado: "2,5" → "2.5"
        var value = originalValue.Replace(',', '.');
        var numericValue = decimal.Parse(value, CultureInfo.InvariantCulture);

        // Unidad original: "ml", "cc", "l", "lt", "litros", etc.
        var rawUnit = volumeMatch.Groups[2].Value.ToLowerInvariant();

        var isLiter = rawUnit is "l" or "lt" or "lts" or "litro" or "litros";

        var unit = isLiter ? Unit.L : Unit.Ml;

        // Unidad y descripción normalizadas
        var unitText = unit.ToString();
        var description = unit.GetDescription();

        return new Measurement
        {
            Type = UnitType.Volume,
            Quantity = 1,
            ValueUnit = numericValue,
            Unit = unit,

            // Ejemplo: "2.5 L"
            DisplayUnit = $"{value} {unitText}",

            // Ejemplo: "2.5L"
            DisplayCanKey = $"{value}{unitText}",

            // Ejemplo: "2.5 Litros"
            TotalDisplay = $"{value} {description}",

            // Conserva el texto original: "2,5 L" o "2.5 L"
            DisplayReplace = volumeMatch.Value
        };
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

        // Valor original: "2,5" o "2.5"
        var originalValue = weightMatch.Groups[1].Value;

        // Valor normalizado: "2,5" → "2.5"
        var value = originalValue.Replace(',', '.');

        var numericValue = decimal.Parse(
            value,
            CultureInfo.InvariantCulture
        );

        // Unidad original: "mg", "g", "gr", "grs", "kg"
        var rawUnit = weightMatch.Groups[2].Value.ToLowerInvariant();

        // Normalizar la unidad sin convertir el valor
        var unit = rawUnit switch
        {
            "kg" => Unit.Kg,
            "mg" => Unit.Mg,
            _ => Unit.G
        };

        // Unidad y descripción normalizadas
        var unitText = unit.ToString();
        var description = unit.GetDescription();

        return new Measurement
        {
            Type = UnitType.Weight,
            Quantity = 1,
            ValueUnit = numericValue,
            Unit = unit,

            // Ejemplo: "2.5 Kg"
            DisplayUnit = $"{value} {unitText}",

            // Ejemplo: "2.5Kg"
            DisplayCanKey = $"{value}{unitText}",

            // Ejemplo: "2.5 Kilogramos"
            TotalDisplay = $"{value} {description}",

            // Conserva el texto original: "2,5 kg" o "2.5 kg"
            DisplayReplace = weightMatch.Value
        };
    }

    private static Measurement? ExtractUnits(string name)
    {
        var unitsMatch = Regex.Match(
            name,
            @"\b(?:(?<unitText>pack)\s*(?<quantity>\d+)|(?<quantity>\d+)\s*(?<unitText>unidades?|un|uds?)\.?(?:\s+(?<connector>de)\b)?)",
            RegexOptions.IgnoreCase
        );

        if (!unitsMatch.Success)
            return null;

        // Cantidad de unidades: "6 un" → 6
        var quantity = int.Parse(
            unitsMatch.Groups["quantity"].Value
        );

        // Obtener el contenido de cada unidad: "220 ml" o "2,5 kg"
        var content = ExtractVolume(name) ?? ExtractWeight(name);

        if (content is null)
            return null;

        // Datos normalizados de la medida
        var value = content.ValueUnit.ToString(
            CultureInfo.InvariantCulture
        );

        var unit = content.Unit;
        var unitText = unit.ToString();
        var description = unit.GetDescription();

        // Texto original de la cantidad de unidades
        var originalUnits = unitsMatch.Value;

        // Texto original de la medida
        var originalContent = content.DisplayReplace;

        // Conservar el fragmento completo cuando ambas partes
        // están juntas: "6 un de 220 ml"
        var start = unitsMatch.Index;
        var end = name.IndexOf(
            originalContent,
            unitsMatch.Index + unitsMatch.Length,
            StringComparison.OrdinalIgnoreCase
        );

        var displayReplace = end >= 0
            ? name.Substring(
                start,
                end + originalContent.Length - start
            )
            : originalUnits;

        return new Measurement
        {
            Type = UnitType.Units,

            // Ejemplo: 6
            Quantity = quantity,

            // Ejemplo: 220
            ValueUnit = content.ValueUnit,

            // Ejemplo: Ml
            Unit = unit,

            // Ejemplo: "220 Ml"
            DisplayUnit = $"{value} {unitText}",

            // Ejemplo: "220Ml"
            DisplayCanKey = $"{value}{unitText}",

            // Ejemplo: "220 Mililitros"
            TotalDisplay = $"{value} {description}",

            // Ejemplo: "6 un de 220 ml"
            DisplayReplace = displayReplace
        };
    }
}