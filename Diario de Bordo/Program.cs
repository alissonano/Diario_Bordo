using Diario_de_Bordo.Data;
using Diario_de_Bordo.Models;
using Diario_de_Bordo.Services;
using Diario_de_Bordo.Services.SetupParsers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Serilog;
var builder = WebApplication.CreateBuilder(args);

// ===== Serviços =====

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(@"C:\logs\AuraMesLog-.txt",
                  rollingInterval: RollingInterval.Day, // Cria um arquivo novo todo dia
                  retainedFileCountLimit: 7)           // Mantém apenas os últimos 7 dias
    .CreateLogger();

builder.Host.UseSerilog();



// Controllers + Views
builder.Services.AddControllersWithViews();

// DbContext com PostgreSQL
builder.Services.AddDbContext<DiarioContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .EnableSensitiveDataLogging()
           .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information);
});

// Session
builder.Services.AddSession();

// HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Upload de arquivos grandes (até 200 MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 200_000_000; // 200MB
});

// ===== Autenticação por Cookie =====
const string AuthScheme = CookieAuthenticationDefaults.AuthenticationScheme;

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = AuthScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "MyAppCookie";
    options.LoginPath = "/Account/Login"; // página de login
    options.AccessDeniedPath = "/Shared/AccessDenied"; // acesso negado
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
});


// Autorização
builder.Services.AddLogging();
builder.Services.AddAuthorization();
builder.Services.AddScoped<Diario_de_Bordo.Services.PickupReportExtractor>();
builder.Services.AddScoped<Diario_de_Bordo.Services.PickupReportExtractorFuji>();
builder.Services.AddScoped<PickupReportExtractorFujiNexim>();
builder.Services.AddHostedService<FujiWorker>();
builder.Services.AddHostedService<PanaWorker>();
builder.Services.AddHostedService<YamahaWorker>();
builder.Services.AddScoped<AssembleonCsvParser>();
builder.Services.AddScoped<IEnumerable<ISetupParser>>(sp => new List<ISetupParser>
{
    sp.GetRequiredService<AssembleonCsvParser>()
    // sp.GetRequiredService<FujiCsvParser>()
});

builder.Services.AddScoped<SetupExtractorService>();

builder.Services.AddScoped<IFujiService, FujiService>();

builder.Services.AddHttpClient();
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var app = builder.Build();

// ===== Pipeline =====

// Serve arquivos estáticos (CSS/JS)
app.UseStaticFiles();

// Rotas
app.UseRouting();
// Session
app.UseSession();

// Autenticação e autorização
app.UseAuthentication();
app.UseAuthorization();



// Mapear rotas padrão
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
