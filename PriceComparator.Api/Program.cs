using PriceComparator.Api.Configurations;
using PriceComparator.Api.Diagnostics;
using PriceComparator.Api.Security;
using PriceComparator.Api.Filters;
using PriceComparator.Application;
using PriceComparator.Infrastructure;
// Habilitar TLS 1.3 mediante Network.framework en macOS.
if (OperatingSystem.IsMacOS())
{
    AppContext.SetSwitch(
        "System.Net.Security.UseNetworkFramework",
        true
    );
}

var builder = WebApplication.CreateBuilder(args);

// Render: cargar secretos de producción
const string secretsPath = "/etc/secrets/secrets.json";

if (builder.Environment.IsProduction())
{
    if (!File.Exists(secretsPath))
    {
        throw new FileNotFoundException("No se encontró el archivo de secretos de Render.", secretsPath);
    }

    builder.Configuration.AddJsonFile(secretsPath, optional: false, reloadOnChange: false);
}

builder.Services.AddMemoryCache();

// ======================================================
// CONTROLLERS Y VALIDACIONES
// ======================================================

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});
builder.Services.AddApiValidation();
builder.Services.AddOpenApi();

// ======================================================
// CLEAN ARCHITECTURE
// ======================================================

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ======================================================
// CONFIGURACIONES HTTP
// ======================================================

builder.Services.AddApiExceptionHandling();
builder.Services.Configure<MobileClientOptions>(
    builder.Configuration.GetSection(MobileClientOptions.SectionName));
builder.Services.AddSingleton<MobileClientKeyValidator>();
builder.Services.AddApiCors(builder.Configuration);
builder.Services.AddApiRateLimiting();
builder.Host.UseDefaultServiceProvider((context, options) =>
{
    var isDevelopment = context.HostingEnvironment.IsDevelopment();
    options.ValidateScopes = isDevelopment;
    options.ValidateOnBuild = isDevelopment;
});

builder.Services.AddHttpClient();

// ======================================================
// APPLICATION PIPELINE - DIAGNÓSTICO
// ======================================================

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    DependencyDiagnostics.Validate(app.Services, builder.Services, logger);
}
app.UseApiSecurityHeaders();
app.UseExceptionHandler();
app.UseRouting();
app.UseCors(CorsConfiguration.Frontend);
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.Run();