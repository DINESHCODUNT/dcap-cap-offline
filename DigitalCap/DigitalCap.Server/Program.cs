using Azure.Identity;
using DigitalCap.Core.Services;
using DigitalCap.Infrastructure.DbContext;
using DigitalCap.Infrastructure.Service;
using DigitalCap.Persistence.Extensions;
using DigitalCap.WebApi.Core;
using ElectronNET.API;
using ElectronNET.API.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

// Initialize SQLCipher provider
SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlcipher());

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseElectron(args);

// Load secrets from Azure Key Vault (works locally via VS/CLI login; in Azure via Managed Identity)
var keyVaultUri = builder.Configuration["KeyVaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}
var configuration = builder.Configuration;
// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<OneDrivePathService>();
builder.Services.AddSingleton<OnlineStatusService>();

builder.Services.Configure<OpenIdConnectOptions>(options =>
{
    options.TokenValidationParameters.NameClaimType = "preferred_username";
    // OR
    options.TokenValidationParameters.NameClaimType = ClaimTypes.Upn;
});

if (!HybridSupport.IsElectronActive)
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
}
else
{
    var pathService = new OneDrivePathService();
    var workingDbPath = pathService.GetDatabasePath();
    var decryptedKey = AppKeyService.GetDecryptedKey();
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite($"Data Source={workingDbPath};Password={decryptedKey};"));
}


var adAudience = configuration["AzureAd:Audience"] ?? "d8a3a596-6fcc-4047-ac76-d990b30f1bb0";
var adAuthority = configuration["AzureAd:Authority"] ?? "https://login.microsoftonline.com/d810b06c-d004-4d52-b0aa-4f3581ee7020/v2.0";
var tenantId = "d810b06c-d004-4d52-b0aa-4f3581ee7020";

var validIssuers = new[]
{
    $"https://sts.windows.net/{tenantId}/",
    $"https://login.microsoftonline.com/{tenantId}/v2.0"
};

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Authority drives automatic OIDC discovery (signing keys, issuer).
    // Do NOT also set options.Audience — use ValidAudiences only to avoid
    // double-validation where the audience string added by options.Audience
    // conflicts with the list in ValidAudiences.
    options.Authority = adAuthority;
    options.RequireHttpsMetadata = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuers = validIssuers,

        ValidateAudience = true,
        // Accept client-id GUID, api://client-id URI, or legacy 00000002 resource
        ValidAudiences = new[]
        {
            adAudience,
            $"api://{adAudience}",
            "00000002-0000-0000-c000-000000000000"
        },

        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(5),

        // v2 tokens use preferred_username; v1 tokens use unique_name
        NameClaimType = "preferred_username",
        RoleClaimType = "roles",

        ValidateIssuerSigningKey = true,
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var msg = context.Exception.Message;
            Console.WriteLine($"[JWT] Auth failed ({context.Exception.GetType().Name}): {msg}");
            context.Response.Headers["X-Auth-Error"] = context.Exception.GetType().Name;
            context.Response.Headers["X-Auth-Error-Detail"] = msg.Length > 200 ? msg[..200] : msg;
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Console.WriteLine($"[JWT] Challenge: error={context.Error}, desc={context.ErrorDescription}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            // Resolve username from claims in priority order (v2 → v1 → fallbacks)
            var principal = context.Principal;
            var username = principal?.FindFirst("preferred_username")?.Value
                ?? principal?.FindFirst("unique_name")?.Value
                ?? principal?.FindFirst("upn")?.Value
                ?? principal?.FindFirst("email")?.Value
                ?? principal?.Identity?.Name;

            Console.WriteLine($"[JWT] Token valid. User={username}, Iss={principal?.FindFirst("iss")?.Value}, Aud={principal?.FindFirst("aud")?.Value}");
            context.HttpContext.Items["AuthProvider"] = "AzureAd";
            context.HttpContext.Items["AuthUser"] = username;
            return Task.CompletedTask;
        },
        OnMessageReceived = context =>
        {
            // Allow token from query string for SignalR / streaming (optional)
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken))
                context.Token = accessToken;
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddIdentityCore<ApplicationUser>
    (options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddSignInManager()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!HybridSupport.IsElectronActive)
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<OnlineStatusMiddleware>();

app.UseAuthorization();

// Ensure local SQLite database exists in OneDrive or LocalAppData from template
if (HybridSupport.IsElectronActive)
{
    var webRoot = app.Environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
    var offlineDbManager = new OfflineDatabaseManager(webRoot);
    offlineDbManager.EnsureWorkingDatabaseExists();
}

app.MapControllers();

app.MapFallbackToFile("/index.html");

if (HybridSupport.IsElectronActive)
{
    Task.Run(async () =>
    {
        // Setup View Menu matching digital-cap-offline
        var viewMenu = new MenuItem[]
        {
            new MenuItem { Role = MenuRole.toggledevtools },
            new MenuItem { Role = MenuRole.zoomin },
            new MenuItem { Role = MenuRole.zoomout },
            new MenuItem { Type = MenuType.separator },
            new MenuItem { Role = MenuRole.togglefullscreen }
        };

        var menu = new MenuItem[]
        {
            new MenuItem { Label = "View", Type = MenuType.submenu, Submenu = viewMenu }
        };

        Electron.Menu.SetApplicationMenu(menu);

        var iconPath = Path.Combine(app.Environment.WebRootPath, "abs-icon.ico");
        var options = new BrowserWindowOptions
        {
            Width = 1280,
            Height = 900,
            Show = false,
            Title = "CAP App",
            Icon = File.Exists(iconPath) ? iconPath : null,
            WebPreferences = new WebPreferences
            {
                NodeIntegration = false,
                ContextIsolation = true
            }
        };

        var window = await Electron.WindowManager.CreateWindowAsync(options);
        window.OnReadyToShow += () => window.Show();
        window.OnClosed += () => Electron.App.Quit();
    });
}

app.Run();
