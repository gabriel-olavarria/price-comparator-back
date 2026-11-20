using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using PriceComparator.Application.Models;
using PriceComparator.Domain.Services;

namespace PriceComparator.Application.Services;

public sealed class RankedService : IRankedService, ITransientDependency
{
    private const int DefaultMinimumScore = 300;

    public IReadOnlyCollection<Product> Rank(string query, IEnumerable<Product> products)
    {
        return
        [
            .. products
                .Select(product => CalculateScore(query, product))
                .Where(result => result.Score >= DefaultMinimumScore)
                .OrderByDescending(result => result.Score)
                .Select(result => result.Product)
        ];
    }

    private RankedProduct CalculateScore(string query, Product product)
    {

        var reasons = new List<string>();
        double score = 0;
        var normalizedName = product.Normalized?.Name ?? "";
        var normalizedQuery = product.Normalized?.Query ?? NormalizerService.Normalize(query);
        var normalizedBrand = product.Normalized?.Brand ?? "";

        // 1. NOMBRE EXACTO
        var exactName = !string.IsNullOrWhiteSpace(normalizedName) && normalizedName.Equals(normalizedQuery, StringComparison.Ordinal);
        if (exactName)
        {
            score += 1000;
            reasons.Add("Nombre exacto: +1000");
        }

        // 2. COMIENZA CON LA CONSULTA
        var startsWithQuery = !exactName && !string.IsNullOrWhiteSpace(normalizedQuery) && normalizedName.StartsWith(normalizedQuery + " ", StringComparison.Ordinal);
        if (startsWithQuery)
        {
            score += 500;
            reasons.Add("El nombre comienza con la búsqueda: +500");
        }

        // 3. CONTIENE LA CONSULTA
        var containsQuery = !string.IsNullOrWhiteSpace(normalizedQuery) && NormalizerService.ContainsTerm(normalizedName, normalizedQuery);
        if (containsQuery)
        {
            score += 200;
            reasons.Add("El nombre contiene la búsqueda: +200");
        }
        
        // 4. CATEGORÍAS
        var categoryMatch = product.CategoriesStore.Any(category => NormalizerService.ContainsTerm(category, normalizedQuery));
        if (categoryMatch)
        {
            score += 300;
            reasons.Add("Coincidencia de categoría: +300");
        }

        // 5. MARCA
        var brandMatch = !string.IsNullOrWhiteSpace(normalizedBrand) && normalizedBrand.Equals(normalizedQuery, StringComparison.Ordinal);
        if (brandMatch)
        {
            score += 500;
            reasons.Add("Coincidencia exacta de marca: +500");
        }

        // 6. COBERTURA DE TOKENS
        var tokenCoverage = CalculateTokenCoverage(query, product);
        score += tokenCoverage * 300;

        if (tokenCoverage > 0)
        {
            reasons.Add($"Cobertura de términos: {NormalizerService.JavaScriptRound(tokenCoverage * 100)}%");
        }

        // 7. POSICIÓN DEL PRIMER TÉRMINO
        var queryTokens = NormalizerService.Tokenize(normalizedQuery);
        var nameTokens = NormalizerService.Tokenize(normalizedName);
        if (queryTokens.Length > 0)
        {
            var firstTokenIndex = Array.FindIndex(nameTokens, token => token.Equals(queryTokens[0], StringComparison.Ordinal));

            switch (firstTokenIndex)
            {
                case 0:
                    score += 150;
                    reasons.Add("Primer término en posición 0: +150");
                    break;
                case 1:
                    score += 75;
                    break;
                case >= 2:
                    score += 20;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(product.Classification?.Concept))
        {
            score = 0;
        }
        
        var finalScore = NormalizerService.JavaScriptRound(score);
        var debug = new ProductScoreDebug(
                    ProductConcept: product.Classification?.Concept,
                    ExactName: exactName,
                    StartsWithQuery: startsWithQuery,
                    ContainsQuery: containsQuery,
                    CategoryMatch: categoryMatch,
                    BrandMatch: brandMatch,
                    TokenCoverage: tokenCoverage,
                    Reasons: reasons);
        
        product.Score = finalScore;
        return new RankedProduct(Product: product, Score: finalScore, Debug: debug);
    }

    private double CalculateTokenCoverage(string query, Product product)
    {
        var queryTokens = NormalizerService.Tokenize(query);
        var matchedTokens = 0;

        if (queryTokens.Length == 0)
        {
            return 0;
        }

        var searchableText = NormalizerService.Normalize(string.Join(" ", new[]
        {
            product.Name,
            product.Brand ?? string.Empty
        }
        .Concat(product.CategoriesStore)));

        foreach (var token in queryTokens)
        {
            if (NormalizerService.ContainsTerm(searchableText, token))
            {
                matchedTokens++;
            }
        }

        return (double)matchedTokens / queryTokens.Length;
    }
}