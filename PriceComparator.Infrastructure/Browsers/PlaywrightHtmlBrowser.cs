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
        int keepPageOpenMs = 0)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (IsProduction())
            {
                return await GetProductionHtmlAsync(
                    url,
                    cancellationToken,
                    waitAfterLoadMs);
            }

            return await GetDevelopmentHtmlAsync(
                url,
                cancellationToken,
                waitAfterLoadMs,
                keepPageOpenMs);
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
        int waitAfterLoadMs)
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
                    }
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

            if (await IsBlockedPageAsync(page))
            {
                throw new InvalidOperationException(
                    $"El sitio bloqueó la navegación de Playwright. URL: {page.Url}");
            }

            await WaitForNextDataAsync(
                page,
                cancellationToken);

            if (waitAfterLoadMs > 0)
            {
                Console.WriteLine(
                    $"[PLAYWRIGHT] Esperando {waitAfterLoadMs} ms para contenido dinámico...");

                await WaitAsync(
                    waitAfterLoadMs,
                    cancellationToken);
            }

            var html = await page.ContentAsync();

            Console.WriteLine(
                $"[PLAYWRIGHT] HTML obtenido. Tamaño: {html.Length} caracteres.");

            Console.WriteLine(
                "[PLAYWRIGHT] Production - HTML de diagnóstico no será guardado.");

            return html;
        }
        finally
        {
            await page.CloseAsync();

            Console.WriteLine(
                "[PLAYWRIGHT] Production - página, contexto y navegador liberados.");
        }
    }

    // ============================================================
    // DEVELOPMENT
    // ============================================================

    private async Task<string> GetDevelopmentHtmlAsync(
        string url,
        CancellationToken cancellationToken,
        int waitAfterLoadMs,
        int keepPageOpenMs)
    {
        await EnsureDevelopmentContextAsync();

        var page = await GetOrCreateDevelopmentPageAsync();

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

        if (await IsBlockedPageAsync(page))
        {
            Console.WriteLine(
                "[PLAYWRIGHT] El sitio solicitó verificación manual.");

            Console.WriteLine(
                "[PLAYWRIGHT] Completa la verificación en la ventana del navegador.");

            await WaitForManualVerificationAsync(
                page,
                cancellationToken);

            Console.WriteLine(
                $"[PLAYWRIGHT] URL después de verificar: {page.Url}");
        }

        await WaitForNextDataAsync(
            page,
            cancellationToken);

        if (waitAfterLoadMs > 0)
        {
            Console.WriteLine(
                $"[PLAYWRIGHT] Esperando {waitAfterLoadMs} ms para contenido dinámico...");

            await WaitAsync(
                waitAfterLoadMs,
                cancellationToken);
        }

        var html = await page.ContentAsync();

        Console.WriteLine(
            $"[PLAYWRIGHT] HTML obtenido. Tamaño: {html.Length} caracteres.");

        // Solo guardamos HTML para diagnóstico local.
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

        if (pages.Count > 0)
        {
            _developmentPage = pages[0];

            return _developmentPage;
        }

        _developmentPage =
            await _developmentContext.NewPageAsync();

        return _developmentPage;
    }

    private static async Task<bool> IsBlockedPageAsync(
        IPage page)
    {
        if (page.Url.Contains(
                "/blocked",
                StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "[PLAYWRIGHT] Página bloqueada detectada por URL.");

            return true;
        }

        var html = await page.ContentAsync();

        var robotOrHuman = html.Contains(
            "Robot or human?",
            StringComparison.OrdinalIgnoreCase);

        var pxCaptcha = html.Contains(
            "px-captcha",
            StringComparison.OrdinalIgnoreCase);

        if (robotOrHuman || pxCaptcha)
        {
            Console.WriteLine(
                "[PLAYWRIGHT] Challenge/CAPTCHA detectado.");

            Console.WriteLine(
                $"[PLAYWRIGHT] Robot or human: {robotOrHuman}");

            Console.WriteLine(
                $"[PLAYWRIGHT] px-captcha: {pxCaptcha}");

            return true;
        }

        return false;
    }

    private static async Task WaitForManualVerificationAsync(
        IPage page,
        CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMinutes(2);
        var checkInterval = TimeSpan.FromSeconds(1);

        var startedAt = DateTime.UtcNow;

        while (DateTime.UtcNow - startedAt < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await IsBlockedPageAsync(page))
            {
                Console.WriteLine(
                    "[PLAYWRIGHT] Verificación manual completada.");

                return;
            }

            await Task.Delay(
                checkInterval,
                cancellationToken);
        }

        throw new TimeoutException(
            "No se completó la verificación manual dentro de 2 minutos.");
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
                Console.WriteLine(
                    "[PLAYWRIGHT] El sitio presentó un challenge.");
            }

            throw;
        }
    }

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