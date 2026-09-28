using Microsoft.Playwright;

namespace PriceComparator.Infrastructure.Browsers;

public sealed class PlaywrightHtmlBrowser : IAsyncDisposable
{
    private IPlaywright? _playwright;

    // Solo se mantienen vivos en Development.
    private IBrowserContext? _developmentContext;
    private IPage? _developmentPage;

    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<string> GetHtmlAsync(
        string url,
        CancellationToken cancellationToken = default,
        int waitAfterLoadMs = 0,
        int keepPageOpenMs = 0,
        bool waitForNextData = true,
        string? waitForSelector = null,
        int selectorTimeoutMs = 15000)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (IsProduction())
            {
                return await GetProductionHtmlAsync(
                    url,
                    cancellationToken,
                    waitAfterLoadMs,
                    waitForNextData,
                    waitForSelector,
                    selectorTimeoutMs);
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

    private async Task<string> GetProductionHtmlAsync(
        string url,
        CancellationToken cancellationToken,
        int waitAfterLoadMs,
        bool waitForNextData,
        string? waitForSelector,
        int selectorTimeoutMs)
    {
        await EnsurePlaywrightAsync();

        Console.WriteLine(
            "[PLAYWRIGHT] Production - iniciando Chromium headless.");

        await using var browser =
            await _playwright!.Chromium.LaunchAsync(
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

        await using var context =
            await browser.NewContextAsync(
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
            Console.WriteLine(
                $"[PLAYWRIGHT] Navegando: {url}");

            await page.GotoAsync(
                url,
                new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 60000
                });

            Console.WriteLine(
                $"[PLAYWRIGHT] URL final: {page.Url}");

            /*
             * Si el sitio nos bloqueó, abortamos inmediatamente.
             *
             * El finally de este método cerrará la página y los
             * await using liberarán context y browser.
             *
             * La excepción llegará al SearchProductOffersUseCase,
             * que convertirá esta tienda en [].
             */
            if (await IsBlockedPageAsync(page))
            {
                throw new InvalidOperationException(
                    $"El sitio bloqueó la navegación de Playwright. URL: {page.Url}");
            }

            if (!string.IsNullOrEmpty(waitForSelector))
            {
                await WaitCustomSelectorAsync(
                    page,
                    waitForSelector,
                    selectorTimeoutMs,
                    cancellationToken);
            }
            else if (waitForNextData)
            {
                await WaitForNextDataAsync(
                    page,
                    cancellationToken);
            }
            else
            {
                Console.WriteLine(
                    "[PLAYWRIGHT] Espera de __NEXT_DATA__ deshabilitada.");
            }

            if (waitAfterLoadMs > 0)
            {
                Console.WriteLine(
                    $"[PLAYWRIGHT] Esperando {waitAfterLoadMs} ms para contenido dinámico...");

                await WaitAsync(
                    waitAfterLoadMs,
                    cancellationToken);
            }

            /*
             * Volvemos a comprobar el bloqueo.
             *
             * Algunos sitios pueden mostrar el challenge después
             * de que la navegación inicial ya terminó.
             */
            if (await IsBlockedPageAsync(page))
            {
                throw new InvalidOperationException(
                    $"El sitio bloqueó la navegación de Playwright. URL: {page.Url}");
            }

            var html = await page.ContentAsync();

            Console.WriteLine(
                $"[PLAYWRIGHT] URL actual al extraer HTML: {page.Url}");

            Console.WriteLine(
                $"[PLAYWRIGHT] HTML obtenido. Tamaño: {html.Length} caracteres.");

            return html;
        }
        finally
        {
            if (!page.IsClosed)
            {
                await page.CloseAsync();
            }

            Console.WriteLine(
                "[PLAYWRIGHT] Production - página cerrada.");

            /*
             * context y browser se liberan automáticamente
             * por los await using al salir del método.
             */
            Console.WriteLine(
                "[PLAYWRIGHT] Production - contexto y navegador serán liberados.");
        }
    }

    // ============================================================
    // DEVELOPMENT
    // ============================================================

    private async Task<string> GetDevelopmentHtmlAsync(
        string url,
        CancellationToken cancellationToken,
        int waitAfterLoadMs,
        int keepPageOpenMs,
        bool waitForNextData,
        string? waitForSelector,
        int selectorTimeoutMs)
    {
        await EnsureDevelopmentContextAsync();

        var page = await GetOrCreateDevelopmentPageAsync();

        try
        {
            Console.WriteLine(
                $"[PLAYWRIGHT] Navegando: {url}");

            await page.GotoAsync(
                url,
                new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 60000
                });

            Console.WriteLine(
                $"[PLAYWRIGHT] URL final: {page.Url}");

            /*
             * Ya NO esperamos verificaciones manuales.
             *
             * Si Líder, Tottus u otro supermercado muestra
             * challenge/bloqueo, abortamos inmediatamente.
             */
            if (await IsBlockedPageAsync(page))
            {
                throw new InvalidOperationException(
                    $"El sitio bloqueó la navegación de Playwright. URL: {page.Url}");
            }

            if (!string.IsNullOrEmpty(waitForSelector))
            {
                await WaitCustomSelectorAsync(
                    page,
                    waitForSelector,
                    selectorTimeoutMs,
                    cancellationToken);
            }
            else if (waitForNextData)
            {
                await WaitForNextDataAsync(
                    page,
                    cancellationToken);
            }
            else
            {
                Console.WriteLine(
                    "[PLAYWRIGHT] Espera de __NEXT_DATA__ deshabilitada.");
            }

            if (waitAfterLoadMs > 0)
            {
                Console.WriteLine(
                    $"[PLAYWRIGHT] Esperando {waitAfterLoadMs} ms para contenido dinámico...");

                await WaitAsync(
                    waitAfterLoadMs,
                    cancellationToken);
            }

            /*
             * Segunda comprobación por si el challenge apareció
             * después de la navegación inicial.
             */
            if (await IsBlockedPageAsync(page))
            {
                throw new InvalidOperationException(
                    $"El sitio bloqueó la navegación de Playwright. URL: {page.Url}");
            }

            var html = await page.ContentAsync();

            Console.WriteLine(
                $"[PLAYWRIGHT] HTML obtenido. Tamaño: {html.Length} caracteres.");

            // Solo Development guarda HTML de diagnóstico.
            await SaveHtmlAsync(
                html,
                page.Url,
                cancellationToken);

            if (keepPageOpenMs > 0)
            {
                Console.WriteLine(
                    $"[PLAYWRIGHT] Manteniendo página visible {keepPageOpenMs} ms...");

                await WaitAsync(
                    keepPageOpenMs,
                    cancellationToken);
            }

            return html;
        }
        catch
        {
            /*
             * Si esta navegación falla —incluido un bloqueo—
             * cerramos la página persistente.
             *
             * La próxima tienda recibirá una página nueva.
             */
            if (!page.IsClosed)
            {
                await page.CloseAsync();
            }

            if (ReferenceEquals(_developmentPage, page))
            {
                _developmentPage = null;
            }

            Console.WriteLine(
                "[PLAYWRIGHT] Development - página liberada después del error/bloqueo.");

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

        Console.WriteLine(
            "[PLAYWRIGHT] Playwright inicializado.");
    }

    private async Task EnsureDevelopmentContextAsync()
    {
        if (_developmentContext is not null)
        {
            return;
        }

        await EnsurePlaywrightAsync();

        var userDataDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "PriceComparator",
            "Playwright");

        Directory.CreateDirectory(userDataDirectory);

        Console.WriteLine(
            $"[PLAYWRIGHT] Perfil persistente: {userDataDirectory}");

        _developmentContext =
            await _playwright!.Chromium.LaunchPersistentContextAsync(
                userDataDirectory,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = false,
                    ViewportSize = null,

                    Args =
                    [
                        "--start-maximized"
                    ]
                });

        Console.WriteLine(
            "[PLAYWRIGHT] Contexto persistente iniciado.");

        _developmentPage =
            await GetOrCreateDevelopmentPageAsync();
    }

    private async Task<IPage> GetOrCreateDevelopmentPageAsync()
    {
        if (_developmentPage is not null &&
            !_developmentPage.IsClosed)
        {
            return _developmentPage;
        }

        if (_developmentContext is null)
        {
            throw new InvalidOperationException(
                "El contexto de Playwright no está inicializado.");
        }

        var pages = _developmentContext.Pages;

        /*
         * Buscamos una página que todavía esté abierta.
         */
        var existingPage = pages.FirstOrDefault(
            existing => !existing.IsClosed);

        if (existingPage is not null)
        {
            _developmentPage = existingPage;

            return _developmentPage;
        }

        _developmentPage =
            await _developmentContext.NewPageAsync();

        return _developmentPage;
    }

    // ============================================================
    // BLOCK / CAPTCHA
    // ============================================================

    private static async Task<bool> IsBlockedPageAsync(
        IPage page)
    {
        /*
         * Líder.
         */
        if (page.Url.Contains(
                "/blocked",
                StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "[PLAYWRIGHT] Página bloqueada detectada por URL.");

            return true;
        }

        /*
         * Cloudflare / Tottus.
         */
        if (page.Url.Contains(
                "__cf_chl_rt_tk",
                StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "[PLAYWRIGHT] Challenge de Cloudflare detectado por URL.");

            return true;
        }

        var html = await page.ContentAsync();

        var robotOrHuman = html.Contains(
            "Robot or human?",
            StringComparison.OrdinalIgnoreCase);

        var pxCaptcha = html.Contains(
            "px-captcha",
            StringComparison.OrdinalIgnoreCase);

        var cloudflareChallenge =
            html.Contains(
                "cf-challenge",
                StringComparison.OrdinalIgnoreCase) ||
            html.Contains(
                "challenges.cloudflare.com",
                StringComparison.OrdinalIgnoreCase) ||
            html.Contains(
                "Just a moment",
                StringComparison.OrdinalIgnoreCase);

        if (!robotOrHuman &&
            !pxCaptcha &&
            !cloudflareChallenge)
        {
            return false;
        }

        Console.WriteLine(
            "[PLAYWRIGHT] Challenge/CAPTCHA detectado.");

        Console.WriteLine(
            $"[PLAYWRIGHT] Robot or human: {robotOrHuman}");

        Console.WriteLine(
            $"[PLAYWRIGHT] px-captcha: {pxCaptcha}");

        Console.WriteLine(
            $"[PLAYWRIGHT] Cloudflare challenge: {cloudflareChallenge}");

        return true;
    }

    // ============================================================
    // SELECTOR & NEXT DATA WAITING
    // ============================================================

    private static async Task WaitCustomSelectorAsync(
        IPage page,
        string selector,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Console.WriteLine(
            $"[PLAYWRIGHT] Esperando selector: {selector}...");

        try
        {
            await page.WaitForSelectorAsync(
                selector,
                new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Attached,
                    Timeout = timeoutMs
                });

            Console.WriteLine(
                $"[PLAYWRIGHT] Selector {selector} encontrado con éxito.");
        }
        catch (TimeoutException)
        {
            Console.WriteLine(
                $"[PLAYWRIGHT] No se encontró el selector {selector}. " +
                $"URL actual: {page.Url}");

            /*
             * Si el selector no apareció porque el sitio nos bloqueó,
             * lanzamos una excepción para abortar esa tienda.
             */
            if (await IsBlockedPageAsync(page))
            {
                throw new InvalidOperationException(
                    $"El sitio bloqueó la navegación de Playwright. URL: {page.Url}");
            }

            /*
             * Si simplemente no apareció el selector pero tampoco
             * existe un bloqueo, dejamos continuar al parser.
             */
        }
    }

    private static async Task WaitForNextDataAsync(
        IPage page,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Console.WriteLine(
            "[PLAYWRIGHT] Esperando __NEXT_DATA__...");

        try
        {
            await page.WaitForSelectorAsync(
                "script#__NEXT_DATA__",
                new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Attached,
                    Timeout = 10000
                });

            Console.WriteLine(
                "[PLAYWRIGHT] __NEXT_DATA__ encontrado.");
        }
        catch (TimeoutException)
        {
            Console.WriteLine(
                $"[PLAYWRIGHT] No se encontró __NEXT_DATA__. URL actual: {page.Url}");

            if (await IsBlockedPageAsync(page))
            {
                throw new InvalidOperationException(
                    $"El sitio bloqueó la navegación de Playwright. URL: {page.Url}");
            }

            /*
             * Si no fue bloqueo, mantenemos el comportamiento anterior:
             * propagamos el timeout.
             */
            throw;
        }
    }

    // ============================================================
    // DEVELOPMENT HTML
    // ============================================================

    private static async Task SaveHtmlAsync(
        string html,
        string currentUrl,
        CancellationToken cancellationToken)
    {
        var outputDirectory = Path.Combine(
            Directory.GetCurrentDirectory(),
            "browser-html");

        Directory.CreateDirectory(outputDirectory);

        var uri = new Uri(currentUrl);

        var pageName = uri.AbsolutePath
            .Trim('/')
            .Replace("/", "-");

        if (string.IsNullOrWhiteSpace(pageName))
        {
            pageName = "index";
        }

        var timestamp =
            DateTime.Now.ToString("yyyyMMdd-HHmmss");

        var fileName =
            $"{pageName}-{timestamp}.html";

        var filePath = Path.Combine(
            outputDirectory,
            fileName);

        await File.WriteAllTextAsync(
            filePath,
            html,
            cancellationToken);

        Console.WriteLine(
            $"[PLAYWRIGHT] HTML guardado en: {filePath}");
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static bool IsProduction()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(
                "ASPNETCORE_ENVIRONMENT"),
            "Production",
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WaitAsync(
        int milliseconds,
        CancellationToken cancellationToken)
    {
        if (milliseconds <= 0)
        {
            return;
        }

        await Task.Delay(
            milliseconds,
            cancellationToken);
    }

    // ============================================================
    // DISPOSE
    // ============================================================

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine(
            "[PLAYWRIGHT] Liberando recursos...");

        if (_developmentPage is not null &&
            !_developmentPage.IsClosed)
        {
            await _developmentPage.CloseAsync();

            _developmentPage = null;
        }

        if (_developmentContext is not null)
        {
            await _developmentContext.CloseAsync();

            _developmentContext = null;
        }

        _playwright?.Dispose();

        _playwright = null;

        _lock.Dispose();

        Console.WriteLine(
            "[PLAYWRIGHT] Recursos liberados.");
    }
}