using PriceComparator.Api.Configurations;
using PriceComparator.Api.Diagnostics;
using PriceComparator.Api.Filters;
using PriceComparator.Application;
using PriceComparator.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddInfrastructure();

// ======================================================
// CONFIGURACIONES HTTP
// ======================================================

builder.Services.AddApiExceptionHandling();
builder.Services.AddApiCors(builder.Configuration);
builder.Services.AddApiRateLimiting();
builder.Host.UseDefaultServiceProvider((context, options) =>
{
    var isDevelopment = context.HostingEnvironment.IsDevelopment();

    options.ValidateScopes = isDevelopment;
    options.ValidateOnBuild = isDevelopment;
});
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
app.UseExceptionHandler();
app.UseRouting();
app.UseCors(CorsConfiguration.Frontend);
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.Run();