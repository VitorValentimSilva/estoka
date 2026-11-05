using System.Globalization;
using Estoka.Application;
using Estoka.Infrastructure;
using Estoka.Infrastructure.Identity;
using Estoka.Infrastructure.Persistence;
using Estoka.Infrastructure.Seed;
using Estoka.Web.Api;
using Estoka.Web.Components;
using Estoka.Web.Endpoints;
using HealthResponseWriter = Estoka.Web.Endpoints.HealthResponseWriter;
using Estoka.Web.Infrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using MudBlazor;
using MudBlazor.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Render (e outros PaaS) informam a porta pela variável PORT; localmente usamos 8080 no container.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var ptBr = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = ptBr;
CultureInfo.DefaultThreadCurrentUICulture = ptBr;

builder.AddEstokaTelemetry();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.Configure<BrandingOptions>(builder.Configuration.GetSection(BrandingOptions.Section));

// Autenticação por cookie (ASP.NET Core Identity) — sem cadastro público.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();
// API REST: autenticação por chave no cabeçalho X-Api-Key (esquema separado do cookie).
builder.Services.AddAuthentication()
    .AddScheme<ApiKeyAuthOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthOptions.Scheme, o => o.Key = builder.Configuration["Api:Key"]);
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "estoka.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/login";
    options.LogoutPath = "/conta/sair";
    options.AccessDeniedPath = "/acesso-negado";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));
builder.Services.AddPermissionPolicies();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingAuthStateProvider>();
builder.Services.AddScoped<CircuitHandler, CurrentUserCircuitHandler>();
builder.Services.AddHttpContextAccessor();

// Chaves do Data Protection no banco: o cookie de login continua válido após reiniciar o container.
builder.Services.AddDataProtection()
    .SetApplicationName("Estoka")
    .PersistKeysToDbContext<AppDbContext>();

builder.Services.AddEstokaRateLimiting(builder.Configuration);
builder.Services.AddHsts(o => o.MaxAge = TimeSpan.FromDays(180));
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
// HTML com acentos em UTF-8 (sem &#xE1;): páginas menores e mais legíveis.
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(o =>
    o.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEstokaOpenApi();

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 4000;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Atrás do proxy do Render (TLS termina no proxy): confiamos nos cabeçalhos X-Forwarded-*.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseForwardedHeaders();
app.UseEstokaSecurityHeaders();
if (!app.Environment.IsDevelopment())
    app.UseHsts(); // só envia o cabeçalho em HTTPS (o proxy do Render informa via X-Forwarded-Proto)
// API e downloads respondem ProblemDetails (IExceptionHandler); páginas vão para /erro.
// Sem UseHttpsRedirection/HSTS: o HTTPS termina no proxy (Render).
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    ExceptionHandlingPath = "/erro",
    CreateScopeForErrors = true,
    AllowStatusCode404Response = true,
});

app.UseRequestLocalization("pt-BR");
app.UseStatusCodePagesWithReExecute("/nao-encontrado", createScopeForStatusCodePages: true);
app.UseWebSockets();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Alimenta o usuário corrente do escopo da requisição (endpoints HTTP e renderização inicial).
app.Use((context, next) =>
{
    context.RequestServices.GetRequiredService<CurrentUser>().SetPrincipal(context.User);
    return next(context);
});
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = (context, report) => HealthResponseWriter.WriteAsync(context, report, includeErrors: app.Environment.IsDevelopment()),
}).AllowAnonymous();
app.MapAccountEndpoints();
app.MapDocumentEndpoints();
app.MapApiV1();
app.MapOpenApi().AllowAnonymous();
app.MapScalarApiReference("/scalar", o => o
        .WithTitle("Estoka API")
        .AddPreferredSecuritySchemes(ApiKeyAuthOptions.Scheme)
        // Só pré-preenche a chave quando ela é pública de propósito (demo); em produção real, Api:ShowKeyInDocs=false.
        .AddApiKeyAuthentication(ApiKeyAuthOptions.Scheme, scheme => scheme.Value = app.Configuration.GetValue("Api:ShowKeyInDocs", false)
            ? app.Configuration["Api:Key"] ?? string.Empty
            : string.Empty))
    .AllowAnonymous();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
