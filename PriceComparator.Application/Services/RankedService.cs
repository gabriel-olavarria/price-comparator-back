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

        // 1. NOMBRE EXACTO
        var exactName = product.Normalized!.Name.Equals(product.Normalized.Query, StringComparison.Ordinal);
        if (exactName)
        {
            score += 1000;
            reasons.Add("Nombre exacto: +1000");
        }

        // 2. COMIENZA CON LA CONSULTA
        var startsWithQuery = !exactName && product.Normalized.Name.StartsWith(product.Normalized.Query + " ", StringComparison.Ordinal);
        if (startsWithQuery)
        {
            score += 500;
            reasons.Add("El nombre comienza con la búsqueda: +500");
        }

        // 3. CONTIENE LA CONSULTA
        var containsQuery = NormalizerService.ContainsTerm(product.Normalized.Name, product.Normalized.Query);
        if (containsQuery)
        {
            score += 200;
            reasons.Add("El nombre contiene la búsqueda: +200");
        }

        // 4 Y 5. CONCEPTO DEL PRODUCTO
        if (product.Clasification?.Id is not null)
        {
            score += 700;
            reasons.Add("Mismo concepto: +700");
        }
        else
        {
            score -= 800;
            reasons.Add("Concepto diferente: -800");
        }

        // 6. CATEGORÍAS
        var categoryMatch = product.CategoriesStore.Any(category => NormalizerService.ContainsTerm(category, product.Normalized.Query));
        if (categoryMatch)
        {
            score += 300;
            reasons.Add("Coincidencia de categoría: +300");
        }

        // 7. MARCA
        var brandMatch = product.Normalized.Brand.Equals(product.Normalized.Query, StringComparison.Ordinal);
        if (brandMatch)
        {
            score += 500;
            reasons.Add("Coincidencia exacta de marca: +500");
        }

        // 8. COBERTURA DE TOKENS
        var tokenCoverage = CalculateTokenCoverage(query, product);
        score += tokenCoverage * 300;

        if (tokenCoverage > 0)
        {
            reasons.Add($"Cobertura de términos: {NormalizerService.JavaScriptRound(tokenCoverage * 100)}%");
        }

        // 9. POSICIÓN DEL PRIMER TÉRMINO
        var queryTokens = NormalizerService.Tokenize(product.Normalized.Query);
        var nameTokens = NormalizerService.Tokenize(product.Normalized.Name);
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

        if (product.Clasification?.Subtype is null)
        {
            score = 0;
        }
        
        var finalScore = NormalizerService.JavaScriptRound(score);
        var debug = new ProductScoreDebug(
                    ProductConcept: product.Clasification?.Id,
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