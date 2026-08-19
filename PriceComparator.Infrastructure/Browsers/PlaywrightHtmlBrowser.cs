using Microsoft.Playwright;

namespace PriceComparator.Infrastructure.Browsers;

public sealed class PlaywrightHtmlBrowser : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowserContext? _context;
    private IPage? _page;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<string> GetHtmlAsync(string url, CancellationToken cancellationToken = default, int waitAfterLoadMs = 0, int keepPageOpenMs = 0)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            await EnsureInitializedAsync();

            if (_page is null || _page.IsClosed)
            {
                var pages = _context!.Pages;
                _page = pages.Count > 0 ? pages[0] : await _context.NewPageAsync();
            }

            Console.WriteLine($"[PLAYWRIGHT] Navegando: {url}");
            
            await _page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60000
            });

            Console.WriteLine($"[PLAYWRIGHT] URL final: {_page.Url}");

            if (_page.Url.Contains("/blocked", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Se bloqueó la navegación automatizada.");
            }

            await _page.WaitForSelectorAsync("script#__NEXT_DATA__", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Attached,
                Timeout = 3000
            });

            if (waitAfterLoadMs > 0)
            {
                Console.WriteLine($"[PLAYWRIGHT] Esperando {waitAfterLoadMs} ms para contenido dinámico...");
                await _page.WaitForTimeoutAsync(waitAfterLoadMs);
            }

            var html = await _page.ContentAsync();

            if (keepPageOpenMs > 0)
            {
                Console.WriteLine($"[PLAYWRIGHT] Manteniendo página visible {keepPageOpenMs} ms...");
                await _page.WaitForTimeoutAsync(keepPageOpenMs);
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
        var userDataDirectory = Path.Combine( Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PriceComparator", "Playwright");
        Directory.CreateDirectory(userDataDirectory);
        _context = await _playwright.Chromium.LaunchPersistentContextAsync(userDataDirectory, new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = false,
            ViewportSize = null,
            Args =
            [
                "--start-maximized"
            ]
        });
        Console.WriteLine("[PLAYWRIGHT] Contexto persistente iniciado.");
        var pages = _context.Pages;
        _page = pages.Count > 0 ? pages[0] : await _context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine("[PLAYWRIGHT] Cerrando navegador...");

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
        Console.WriteLine("[PLAYWRIGHT] Navegador cerrado.");
    }
}