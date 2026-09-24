using Microsoft.Playwright;

namespace PriceComparator.Infrastructure.Browsers;

public sealed class PlaywrightHtmlBrowser : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowserContext? _context;
    private IPage? _page;

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
            await EnsureInitializedAsync();

            _page = await GetOrCreatePageAsync();

            Console.WriteLine($"[PLAYWRIGHT] Navegando: {url}");

            await _page.GotoAsync(
                url,
                new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 60000
                });

            Console.WriteLine(
                $"[PLAYWRIGHT] URL final: {_page.Url}");

            if (IsBlockedPage(_page))
            {
                Console.WriteLine(
                    "[PLAYWRIGHT] Lider solicitó verificación manual.");

                Console.WriteLine(
                    "[PLAYWRIGHT] Completa la verificación en la ventana del navegador.");

                await WaitForManualVerificationAsync(
                    _page,
                    cancellationToken);

                Console.WriteLine(
                    $"[PLAYWRIGHT] URL después de verificar: {_page.Url}");
            }

            await WaitForNextDataAsync(
                _page,
                cancellationToken);

            if (waitAfterLoadMs > 0)
            {
                Console.WriteLine(
                    $"[PLAYWRIGHT] Esperando {waitAfterLoadMs} ms para contenido dinámico...");

                await WaitAsync(
                    waitAfterLoadMs,
                    cancellationToken);
            }

            // Obtiene el HTML actual de la página
            var html = await _page.ContentAsync();

            Console.WriteLine(
                $"[PLAYWRIGHT] HTML obtenido. Tamaño: {html.Length} caracteres.");

            // Guarda una copia física del HTML para depuración
            await SaveHtmlAsync(
                html,
                _page.Url,
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
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_context is not null)
        {
            return;
        }

        _playwright = await Playwright.CreateAsync();

        var userDataDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "PriceComparator",
            "Playwright");

        Directory.CreateDirectory(userDataDirectory);

        Console.WriteLine(
            $"[PLAYWRIGHT] Perfil persistente: {userDataDirectory}");

        _context =
            await _playwright.Chromium.LaunchPersistentContextAsync(
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

        _page = await GetOrCreatePageAsync();
    }

    private async Task<IPage> GetOrCreatePageAsync()
    {
        if (_page is not null && !_page.IsClosed)
        {
            return _page;
        }

        if (_context is null)
        {
            throw new InvalidOperationException(
                "El contexto de Playwright no está inicializado.");
        }

        var pages = _context.Pages;

        if (pages.Count > 0)
        {
            _page = pages[0];

            return _page;
        }

        _page = await _context.NewPageAsync();

        return _page;
    }

    private static bool IsBlockedPage(IPage page)
    {
        return page.Url.Contains(
            "/blocked",
            StringComparison.OrdinalIgnoreCase);
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

            if (!IsBlockedPage(page))
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

        var timestamp = DateTime.Now
            .ToString("yyyyMMdd-HHmmss");

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
            "[PLAYWRIGHT] Cerrando navegador...");

        if (_page is not null && !_page.IsClosed)
        {
            await _page.CloseAsync();

            _page = null;
        }

        if (_context is not null)
        {
            await _context.CloseAsync();

            _context = null;
        }

        _playwright?.Dispose();

        _playwright = null;

        _lock.Dispose();

        Console.WriteLine(
            "[PLAYWRIGHT] Navegador cerrado.");
    }
}