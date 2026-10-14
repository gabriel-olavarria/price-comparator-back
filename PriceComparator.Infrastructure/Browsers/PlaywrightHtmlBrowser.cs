using Microsoft.Playwright;
using PriceComparator.Application.Interfaces;

namespace PriceComparator.Infrastructure.Browsers;

public sealed class PlaywrightHtmlBrowser : IAsyncDisposable, ISingletonDependency
{
    private IPlaywright? _playwright;
    // Solo se mantienen vivos en Development.
    private IBrowserContext? _developmentContext;
    private IPage? _developmentPage;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<string> GetHtmlAsync(string url, CancellationToken cancellationToken = default, int waitAfterLoadMs = 0, int keepPageOpenMs = 0, bool waitForNextData = true, string? waitForSelector = null, int selectorTimeoutMs = 15000)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (IsProduction())
            {
                return await GetProductionHtmlAsync(url, cancellationToken, waitAfterLoadMs, waitForNextData, waitForSelector, selectorTimeoutMs);
            }

            return await GetDevelopmentHtmlAsync(
                url,
                cancellationToken,
                waitAfterLoadMs,
                keepPageOpenMs,
                waitForNextData,
                waitForSelector,
                selectorTimeoutMs);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ============================================================
    // PRODUCTION
    // ============================================================

    private async Task<string> GetProductionHtmlAsync(string url, CancellationToken cancellationToken, int waitAfterLoadMs, bool waitForNextData, string? waitForSelector, int selectorTimeoutMs)
    {
        await EnsurePlaywrightAsync();
        Console.WriteLine("[PLAYWRIGHT] Production - iniciando Chromium headless.");
        await using var browser = await _playwright!.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Args =
                    [
                        "--no-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-gpu",
                        "--disable-extensions",
                        "--disable-background-networking"
                    ]
                });

        await using var context = await browser.NewContextAsync(
                new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize
                    {
                        Width = 1366,
                        Height = 768
                    },

                    UserAgent =
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                        "AppleWebKit/537.36 (KHTML, like Gecko) " +
                        "Chrome/122.0.0.0 Safari/537.36"
                });

        var page = await context.NewPageAsync();
        try
        {
            Console.WriteLine($"[PLAYWRIGHT] Navegando: {url}");

            await page.GotoAsync(url,
                new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 60000
                });

            Console.WriteLine($"[PLAYWRIGHT] URL final: {page.Url}");

            if (await IsBlockedPageAsync(page))
            {
                throw new InvalidOperationException($"El sitio bloqueó la navegación de Playwright. URL: {page.Url}");
            }

            if (!string.IsNullOrEmpty(waitForSelector))
            {
                await WaitCustomSelectorAsync(page, waitForSelector, selectorTimeoutMs, cancellationToken);
            }
            else if (waitForNextData)
            {
                await WaitForNextDataAsync(page, cancellationToken);
            }
            else
            {
                Console.WriteLine("[PLAYWRIGHT] Espera de __NEXT_DATA__ deshabilitada.");
            }

            if (waitAfterLoadMs > 0)
            {
                Console.WriteLine($"[PLAYWRIGHT] Esperando {waitAfterLoadMs} ms para contenido dinámico...");
                await WaitAsync(waitAfterLoadMs, cancellationToken);
            }

            if (await IsBlockedPageAsync(page))
            {
                throw new InvalidOperationException($"El sitio bloqueó la navegación de Playwright. URL: {page.Url}");
            }

            var html = await page.ContentAsync();
            Console.WriteLine($"[PLAYWRIGHT] URL actual al extraer HTML: {page.Url}");
            Console.WriteLine($"[PLAYWRIGHT] HTML obtenido. Tamaño: {html.Length} caracteres.");
            return html;
        }
        finally
        {
            if (!page.IsClosed)
            {
                await page.CloseAsync();
            }

            Console.WriteLine("[PLAYWRIGHT] Production - página cerrada.");
            Console.WriteLine("[PLAYWRIGHT] Production - contexto y navegador serán liberados.");
        }
    }

    // ============================================================
    // DEVELOPMENT
    // ============================================================

    private async Task<string> GetDevelopmentHtmlAsync(string url, CancellationToken cancellationToken, int waitAfterLoadMs, int keepPageOpenMs, bool waitForNextData, string? waitForSelector, int selectorTimeoutMs)
    {
        await EnsureDevelopmentContextAsync();
        var page = await GetOrCreateDevelopmentPageAsync();

        try
        {
            Console.WriteLine($"[PLAYWRIGHT] Navegando: {url}");

            await page.GotoAsync(url,
                new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 60000
                });

            Console.WriteLine($"[PLAYWRIGHT] URL final: {page.Url}");

            if (await IsBlockedPageAsync(page))
            {
                throw new InvalidOperationException($"El sitio bloqueó la navegación de Playwright. URL: {page.Url}");
            }

            if (!string.IsNullOrEmpty(waitForSelector))
            {
                await WaitCustomSelectorAsync(page, waitForSelector, selectorTimeoutMs, cancellationToken);
            }
            else if (waitForNextData)
            {
                await WaitForNextDataAsync(page, cancellationToken);
            }
            else
            {
                Console.WriteLine("[PLAYWRIGHT] Espera de __NEXT_DATA__ deshabilitada.");
            }

            if (waitAfterLoadMs > 0)
            {
                Console.WriteLine($"[PLAYWRIGHT] Esperando {waitAfterLoadMs} ms para contenido dinámico...");
                await WaitAsync(waitAfterLoadMs, cancellationToken);
            }

            if (await IsBlockedPageAsync(page))
            {
                throw new InvalidOperationException($"El sitio bloqueó la navegación de Playwright. URL: {page.Url}");
            }

            var html = await page.ContentAsync();
            Console.WriteLine($"[PLAYWRIGHT] HTML obtenido. Tamaño: {html.Length} caracteres.");

            if (keepPageOpenMs > 0)
            {
                Console.WriteLine($"[PLAYWRIGHT] Manteniendo página visible {keepPageOpenMs} ms...");
                await WaitAsync(keepPageOpenMs, cancellationToken);
            }

            return html;
        }
        catch
        {
            /*
             * Si falla la navegación, intentamos liberar la página.
             * El cierre también puede fallar si Chromium/contexto
             * ya murió, por lo que no dejamos que ese segundo error
             * oculte la excepción original.
             */
            if (!page.IsClosed)
            {
                try
                {
                    await page.CloseAsync();
                }
                catch (PlaywrightException exception)
                {
                    Console.WriteLine($"[PLAYWRIGHT] La página ya no pudo cerrarse: {exception.Message}");
                }
            }

            if (ReferenceEquals(_developmentPage, page))
            {
                _developmentPage = null;
            }

            Console.WriteLine("[PLAYWRIGHT] Development - página liberada después del error/bloqueo.");
            throw;
        }
    }

    // ============================================================
    // INITIALIZATION
    // ============================================================

    private async Task EnsurePlaywrightAsync()
    {
        if (_playwright is not null)
        {
            return;
        }

        _playwright = await Playwright.CreateAsync();
        Console.WriteLine("[PLAYWRIGHT] Playwright inicializado.");
    }

    private async Task EnsureDevelopmentContextAsync()
    {
        if (_developmentContext is not null)
        {
            return;
        }

        await EnsurePlaywrightAsync();

        var userDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PriceComparator",
            "Playwright");

        Directory.CreateDirectory(userDataDirectory);
        Console.WriteLine($"[PLAYWRIGHT] Perfil persistente: {userDataDirectory}");

        _developmentContext = await _playwright!.Chromium.LaunchPersistentContextAsync(userDataDirectory,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = false,
                    ViewportSize = null,
                    Args = ["--start-maximized"]
                });

        Console.WriteLine("[PLAYWRIGHT] Contexto persistente iniciado.");
        _developmentPage = await GetOrCreateDevelopmentPageAsync();
    }

    private async Task<IPage> GetOrCreateDevelopmentPageAsync()
    {
        if (_developmentPage is not null && !_developmentPage.IsClosed)
        {
            return _developmentPage;
        }

        if (_developmentContext is null)
        {
            throw new InvalidOperationException("El contexto de Playwright no está inicializado.");
        }

        var pages = _developmentContext.Pages;
        var existingPage = pages.FirstOrDefault(existing => !existing.IsClosed);

        if (existingPage is not null)
        {
            _developmentPage = existingPage;
            return _developmentPage;
        }

        _developmentPage = await _developmentContext.NewPageAsync();
        return _developmentPage;
    }

    // ============================================================
    // BLOCK / CAPTCHA
    // ============================================================

    private static async Task<bool> IsBlockedPageAsync(IPage page)
    {
        // Líder.
        if (page.Url.Contains("/blocked", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[PLAYWRIGHT] Página bloqueada detectada por URL.");
            return true;
        }

        // Cloudflare / Tottus.
        if (page.Url.Contains("__cf_chl_rt_tk", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[PLAYWRIGHT] Challenge de Cloudflare detectado por URL.");
            return true;
        }

        var html = await page.ContentAsync();
        var robotOrHuman = html.Contains("Robot or human?", StringComparison.OrdinalIgnoreCase);
        var pxCaptcha = html.Contains("px-captcha", StringComparison.OrdinalIgnoreCase);

        var cloudflareChallenge =
            html.Contains("cf-challenge", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("challenges.cloudflare.com", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("Just a moment", StringComparison.OrdinalIgnoreCase);

        if (!robotOrHuman && !pxCaptcha && !cloudflareChallenge)
        {
            return false;
        }

        Console.WriteLine("[PLAYWRIGHT] Challenge/CAPTCHA detectado.");
        Console.WriteLine($"[PLAYWRIGHT] Robot or human: {robotOrHuman}");
        Console.WriteLine($"[PLAYWRIGHT] px-captcha: {pxCaptcha}");
        Console.WriteLine($"[PLAYWRIGHT] Cloudflare challenge: {cloudflareChallenge}");

        return true;
    }

    // ============================================================
    // SELECTOR & NEXT DATA WAITING
    // ============================================================

    private static async Task WaitCustomSelectorAsync(IPage page, string selector, int timeoutMs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine($"[PLAYWRIGHT] Esperando selector: {selector}...");

        try
        {
            await page.WaitForSelectorAsync(selector,
                new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Attached,
                    Timeout = timeoutMs
                });

            Console.WriteLine($"[PLAYWRIGHT] Selector {selector} encontrado con éxito.");
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"[PLAYWRIGHT] No se encontró el selector {selector}. " + $"URL actual: {page.Url}");

            if (await IsBlockedPageAsync(page))
            {
                throw new InvalidOperationException($"El sitio bloqueó la navegación de Playwright. URL: {page.Url}");
            }
        }
    }

    private static async Task WaitForNextDataAsync(IPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine("[PLAYWRIGHT] Esperando __NEXT_DATA__...");
        
        try
        {
            await page.WaitForSelectorAsync("script#__NEXT_DATA__",
                new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Attached,
                    Timeout = 10000
                });

            Console.WriteLine("[PLAYWRIGHT] __NEXT_DATA__ encontrado.");
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"[PLAYWRIGHT] No se encontró __NEXT_DATA__. URL actual: {page.Url}");

            if (await IsBlockedPageAsync(page))
            {
                throw new InvalidOperationException($"El sitio bloqueó la navegación de Playwright. URL: {page.Url}");
            }

            throw;
        }
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static bool IsProduction()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), 
            "Production", 
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WaitAsync(int milliseconds, CancellationToken cancellationToken)
    {
        if (milliseconds <= 0)
        {
            return;
        }
        await Task.Delay(milliseconds, cancellationToken);
    }

    // ============================================================
    // DISPOSE
    // ============================================================

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine("[PLAYWRIGHT] Liberando recursos...");
        try
        {
            /*
             * LaunchPersistentContextAsync devuelve directamente
             * un IBrowserContext.
             * Al cerrar este contexto también se cierra el navegador
             * persistente asociado. No necesitamos cerrar la página
             * por separado.
             */
            if (_developmentContext is not null)
            {
                try
                {
                    await _developmentContext.CloseAsync();
                }
                catch (PlaywrightException exception)
                {
                    /*
                     * Durante el apagado Chromium puede haberse cerrado
                     * antes que el Host de ASP.NET.
                     * No usamos TargetClosedException porque esa clase
                     * no es públicamente accesible en esta versión.
                     */
                    Console.WriteLine("[PLAYWRIGHT] El contexto ya estaba cerrado " + $"o no pudo cerrarse: {exception.Message}");
                }
            }
        }
        finally
        {
            _developmentPage = null;
            _developmentContext = null;
            _playwright?.Dispose();
            _playwright = null;
            _lock.Dispose();
            Console.WriteLine("[PLAYWRIGHT] Recursos liberados.");
        }
    }
}