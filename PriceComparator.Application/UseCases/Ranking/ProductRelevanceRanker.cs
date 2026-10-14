using PriceComparator.Application.Interfaces;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.Models;
using PriceComparator.Domain.Services;

namespace PriceComparator.Application.UseCases.Ranking;

public sealed class ProductRelevanceRanker : ISingletonDependency
{
    private const int DefaultMinimumScore = 300;

    public IReadOnlyCollection<RankedProduct> Rank(string query, IEnumerable<Domain.Entities.Product> products, IReadOnlyCollection<Concept> concepts, int minimumScore = DefaultMinimumScore)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(concepts);

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        /*
        var prodSubTypeNull = products
            .Select(product => CalculateScore(query, product, concepts))
            .Where(product => product.Score == 0)
            .Select(x => new
            {
                Product = x.Product.Name,
                x.Product.Price,
                x.Product.CanonicalKey,
                x.Score
            })
            .ToArray();
            */
            
            
        return products
            .Select(product => CalculateScore(query, product, concepts))
            .Where(product => product.Score >= minimumScore)
            .OrderByDescending(product => product.Score)
            .ToArray();
    }

    private static RankedProduct CalculateScore(string query, Product product, IReadOnlyCollection<Concept> concepts)
    {
        var normalizedQuery = ProductNormalizer.Normalize(query);
        var normalizedName = ProductNormalizer.Normalize(product.Name);
        var normalizedBrand = ProductNormalizer.Normalize(product.Brand);
        var queryConcept = ProductConceptDetector.DetectFromText(query, concepts);
        var productConcept = ProductConceptDetector.DetectClasification(product, queryConcept!);
        var productClasificado = ProductConceptDetector.DetectClasification(product, queryConcept!);

        var reasons = new List<string>();
        double score = 0;

        // 1. NOMBRE EXACTO
        var exactName = normalizedName.Equals(normalizedQuery, StringComparison.Ordinal);

        if (exactName)
        {
            score += 1000;
            reasons.Add("Nombre exacto: +1000");
        }

        // 2. COMIENZA CON LA CONSULTA
        var startsWithQuery = normalizedName.Equals(normalizedQuery, StringComparison.Ordinal) || normalizedName.StartsWith(normalizedQuery + " ", StringComparison.Ordinal);

        if (startsWithQuery)
        {
            score += 500;
            reasons.Add("El nombre comienza con la búsqueda: +500");
        }

        // 3. CONTIENE LA CONSULTA
        var containsQuery = ProductNormalizer.ContainsTerm(normalizedName, normalizedQuery);

        if (containsQuery)
        {
            score += 200;
            reasons.Add("El nombre contiene la búsqueda: +200");
        }

        // 4 Y 5. CONCEPTO DEL PRODUCTO
        if (queryConcept is not null)
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
        var categoryMatch = productClasificado.Categories.Any(category => ProductNormalizer.ContainsTerm(category, normalizedQuery));

        if (categoryMatch)
        {
            score += 300;
            reasons.Add("Coincidencia de categoría: +300");
        }

        // 7. MARCA
        var brandMatch = normalizedBrand.Equals(normalizedQuery, StringComparison.Ordinal);

        if (brandMatch)
        {
            score += 500;
            reasons.Add("Coincidencia exacta de marca: +500");
        }

        // 8. COBERTURA DE TOKENS
        var tokenCoverage = CalculateTokenCoverage(query, productClasificado);
        score += tokenCoverage * 300;

        if (tokenCoverage > 0)
        {
            reasons.Add($"Cobertura de términos: {JavaScriptRound(tokenCoverage * 100)}%");
        }

        // 9. POSICIÓN DEL PRIMER TÉRMINO
        var queryTokens = ProductNormalizer.Tokenize(normalizedQuery);
        var nameTokens = ProductNormalizer.Tokenize(normalizedName);

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

        if (productClasificado.Clasification?.Subtype is null)
        {
            score = 0;
        }
        
        var finalScore = JavaScriptRound(score);
        var debug = new ProductScoreDebug(
                    QueryConcept: queryConcept?.Id,
                    ProductConcept: productConcept.Clasification?.Id,
                    ExactName: exactName,
                    StartsWithQuery: startsWithQuery,
                    ContainsQuery: containsQuery,
                    CategoryMatch: categoryMatch,
                    BrandMatch: brandMatch,
                    TokenCoverage: tokenCoverage,
                    Reasons: reasons);
        
        productClasificado.Score = score;
        return new RankedProduct(Product: productClasificado, Score: finalScore, Debug: debug);
    }

    private static double CalculateTokenCoverage(string query, Product product)
    {
        var queryTokens = ProductNormalizer.Tokenize(query);
        var matchedTokens = 0;

        if (queryTokens.Length == 0)
        {
            return 0;
        }

        var searchableText = ProductNormalizer.Normalize(string.Join(" ", new[]
        {
            product.Name,
            product.Brand ?? string.Empty
        }
        .Concat(product.Categories)));

        foreach (var token in queryTokens)
        {
            if (ProductNormalizer.ContainsTerm(searchableText, token))
            {
                matchedTokens++;
            }
        }

        return (double)matchedTokens / queryTokens.Length;
    }

    private static int JavaScriptRound(double value)
    {
        return (int)Math.Floor(value + 0.5);
    }
}