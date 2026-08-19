using Microsoft.Playwright;

namespace PriceComparator.Infrastructure.Tests;

public static class PlaywrightTest
{
    public static async Task TestAsync()
    {
        using var playwright = await Playwright.CreateAsync();

        Console.WriteLine("1. Playwright iniciado");

        await using var browser =
            await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = false
                });

        Console.WriteLine("2. Chromium iniciado");

        var page = await browser.NewPageAsync();

        var url =
            "https://www.lider.cl/search?q=coca%20cola";

        Console.WriteLine($"3. Navegando: {url}");

        await page.GotoAsync(
            url,
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60000
            });

        Console.WriteLine($"4. URL final: {page.Url}");
        Console.WriteLine($"5. Título: {await page.TitleAsync()}");

        if (page.Url.Contains(
                "/blocked",
                StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("❌ Lider bloqueó el test.");
            return;
        }

        var nextDataLocator =
            page.Locator("script#__NEXT_DATA__");

        var count =
            await nextDataLocator.CountAsync();

        Console.WriteLine(
            $"6. __NEXT_DATA__ encontrados: {count}");

        if (count == 0)
        {
            Console.WriteLine(
                "❌ No se encontró __NEXT_DATA__.");

            return;
        }

        var nextData =
            await nextDataLocator.TextContentAsync();

        Console.WriteLine(
            $"7. Tamaño __NEXT_DATA__: {nextData?.Length ?? 0}");

        Console.WriteLine(
            "✅ Test terminado correctamente.");
    }
}