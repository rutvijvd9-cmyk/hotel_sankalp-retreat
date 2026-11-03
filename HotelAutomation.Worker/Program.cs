using HotelAutomation.Connectors.Pms;
using HotelAutomation.Connectors.WhatsApp;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Core.Models;
using HotelAutomation.Data;
using HotelAutomation.Data.Repositories;
using HotelAutomation.Worker;
using HotelAutomation.Worker.Logging;
using HotelAutomation.Worker.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Dapper;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.DataProtection;

// ── App Data & Logs Directory Initialization ─────────────────────────────────
var appDataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "HotelSankalpRetreat");
Directory.CreateDirectory(appDataDir);

var logsDir = Path.Combine(appDataDir, "logs");
Directory.CreateDirectory(logsDir);

// Initialize synchronous crash logger immediately before anything else
CrashLogger.Initialize(logsDir);

// ── Global Unhandled Exception Handlers (captures all fatal crashes to disk) ──
AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
{
    var ex = eventArgs.ExceptionObject as Exception;
    CrashLogger.LogCrash("AppDomain.UnhandledException", ex, $"IsTerminating: {eventArgs.IsTerminating}");
};

TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
{
    CrashLogger.LogCrash("TaskScheduler.UnobservedTaskException", eventArgs.Exception);
    eventArgs.SetObserved();
};

// ── Single-Instance Global Mutex (Guarantees only one instance can run) ─────
using var singleInstanceMutex = new Mutex(true, @"Global\HotelAutomation_TheSankalpRetreat_Worker", out bool isNewInstance);
if (!isNewInstance)
{
    Console.WriteLine("[Startup] Another instance of HotelAutomation.Worker is already running. Exiting cleanly.");
    return;
}

// ── Free Port 8001 and Stop Any Stuck/Duplicate Worker Instances ──────────────
PortAndProcessManager.EnsurePortAndProcessAvailable(8001);

var builder = WebApplication.CreateBuilder(args);

// ── Always bind to localhost:8001 regardless of environment or launchSettings ──
builder.WebHost.UseUrls("http://localhost:8001");

builder.WebHost.ConfigureKestrel(options => 
{
    options.Limits.MaxRequestBodySize = 250_000_000; // 250 MB
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 250_000_000; // 250 MB
});

// ── Unified Logging Pipeline (Console + Disk Rolling Files + Database) ────────
var unifiedLoggerProvider = new UnifiedLoggerProvider(logsDir);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddProvider(unifiedLoggerProvider);

// Enable CORS for the React UI
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// ── Database ──────────────────────────────────────────────────────────────────
var dbDestPath = Path.Combine(appDataDir, "hotel_automation.db");

// On first install, copy the seed database shipped with the app
var dbSeedPath = Path.Combine(AppContext.BaseDirectory, "hotel_automation.db");
if (!File.Exists(dbDestPath) && File.Exists(dbSeedPath))
{
    File.Copy(dbSeedPath, dbDestPath);
}

// Also redirect the uploads folder to AppData so user files are writable
var uploadsDataDir = Path.Combine(appDataDir, "uploads");
Directory.CreateDirectory(uploadsDataDir);

// Redirect signatures folder to AppData (Program Files is read-only for normal users)
var signaturesDataDir = Path.Combine(appDataDir, "signatures");
Directory.CreateDirectory(signaturesDataDir);

// Redirect backup_date folder to AppData
var backupDateDir = Path.Combine(appDataDir, "backup_date");
Directory.CreateDirectory(backupDateDir);

var rawConnString = builder.Configuration.GetConnectionString("Sqlite")
    ?? "Data Source=hotel_automation.db";

// Replace relative filename with absolute AppData path
var connectionString = $"Data Source={dbDestPath}";

builder.Services.AddSingleton(new SqliteConnectionFactory(connectionString));


// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ITemplateRepository, TemplateRepository>();
builder.Services.AddSingleton<ISmartListRepository, SmartListRepository>();
builder.Services.AddSingleton<ICampaignRepository, CampaignRepository>();
builder.Services.AddSingleton<IRuleRepository, RuleRepository>();
builder.Services.AddSingleton<IOutboxRepository, OutboxRepository>();
builder.Services.AddSingleton<LinkShortenerService>();
builder.Services.AddSingleton<EventLogRepository>();
builder.Services.AddSingleton<SystemLogRepository>();
builder.Services.AddSingleton<JobRepository>();
builder.Services.AddSingleton<CategoryRepository>();
builder.Services.AddSingleton<AppConfigRepository>();
builder.Services.AddSingleton<ContactRepository>();
builder.Services.AddSingleton<CsvImportService>();
builder.Services.AddSingleton<BroadcastRepository>();
builder.Services.AddSingleton<UnsubscribedContactRepository>();
builder.Services.AddSingleton<ContactConsentRepository>();
builder.Services.AddSingleton<PmsMappingRepository>();
builder.Services.AddSingleton<TemplateVariantRepository>();
builder.Services.AddSingleton<HotelAutomation.Worker.Services.VariantSelector>();
builder.Services.AddSingleton<HotelAutomation.Worker.Services.TemplateSyncService>();
builder.Services.AddSingleton<HotelAutomation.Worker.Services.DatabaseBackupService>();

// ── Email Alert System ────────────────────────────────────────────────────────
builder.Services.Configure<HotelAutomation.Worker.Services.EmailAlertOptions>(
    builder.Configuration.GetSection(HotelAutomation.Worker.Services.EmailAlertOptions.SectionName));
builder.Services.AddSingleton<HotelAutomation.Worker.Services.EmailAlertService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<HotelAutomation.Worker.Services.EmailAlertService>());

// ── Data Protection (Persist encryption keys across app restarts/reboots) ──
var dataProtectionKeysDir = Path.Combine(appDataDir, "data_protection_keys");
Directory.CreateDirectory(dataProtectionKeysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysDir))
    .SetApplicationName("HotelSankalpRetreat");

// ── Security & Authentication ──────────────────────────────────────────────────
builder.Services.AddSingleton<HotelAutomation.Core.Security.PasswordHasher>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<ActivityLogRepository>();
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name        = "HotelAuth";
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.None; // localhost HTTP
        // Persist cookie for 365 days with sliding expiration so staff remain logged in
        options.ExpireTimeSpan     = TimeSpan.FromDays(365);
        options.SlidingExpiration  = true;
        options.Cookie.MaxAge      = TimeSpan.FromDays(365);
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

// ── WhatsApp Connector ────────────────────────────────────────────────────────
builder.Services.Configure<WhatsAppOptions>(
    builder.Configuration.GetSection(WhatsAppOptions.SectionName));
builder.Services.AddSingleton<Microsoft.Extensions.Options.IConfigureOptions<WhatsAppOptions>, HotelAutomation.Worker.Services.ConfigureWhatsAppOptions>();

builder.Services.AddHttpClient<WhatsAppApiClient>();
builder.Services.AddSingleton<IMessageChannel, WhatsAppMessageSender>();
builder.Services.AddSingleton<IMessageChannel, SmsMessageSender>();

// ── PMS Connector ─────────────────────────────────────────────────────────────
var pmsType = builder.Configuration.GetValue<string>("PmsConnectorType") ?? "Manual";
if (pmsType == "Database")
{
    var pmsOptions = new DatabasePmsOptions();
    builder.Configuration.GetSection(DatabasePmsOptions.SectionName).Bind(pmsOptions);
    builder.Services.AddSingleton(pmsOptions);
    builder.Services.AddSingleton<IPmsEventSource>(sp => 
        new DatabasePmsConnector(
            sp.GetRequiredService<DatabasePmsOptions>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DatabasePmsConnector>>(),
            builder.Configuration.GetConnectionString("Sqlite")!
        ));
}
else if (pmsType == "Pdf")
{
    // ── Virtual Printer / PDF Print Bridge connector ────────────────────────
    // Watches a folder for PDF files produced by the virtual printer,
    // extracts guest data (name, phone, room, amount, tax) and fires PmsEvents.
    var pdfBridgeOptions = new HotelAutomation.Connectors.Pms.PdfBridgeOptions();
    builder.Configuration.GetSection(HotelAutomation.Connectors.Pms.PdfBridgeOptions.SectionName)
        .Bind(pdfBridgeOptions);
    builder.Services.AddSingleton(pdfBridgeOptions);
    builder.Services.AddSingleton<HotelAutomation.Connectors.Pms.PdfDataExtractor>();
    builder.Services.AddSingleton<HotelAutomation.Connectors.Pms.PdfPmsConnector>();
    builder.Services.AddSingleton<IPmsEventSource>(
        sp => sp.GetRequiredService<HotelAutomation.Connectors.Pms.PdfPmsConnector>());
}

// Always register ManualPmsConnector as a service to prevent Minimal API parameter binding failures
builder.Services.AddSingleton<ManualPmsConnector>();

if (pmsType != "Database" && pmsType != "Pdf")
{
    builder.Services.AddSingleton<IPmsEventSource>(sp => sp.GetRequiredService<ManualPmsConnector>());
}

// ── PDF Generation ─────────────────────────────────────────────────────────────
try
{
    // Native DLL import resolver for QuestPDF native libraries (QuestPdfSkia.dll)
    var questPdfAssembly = typeof(QuestPDF.Settings).Assembly;
    NativeLibrary.SetDllImportResolver(questPdfAssembly, (libraryName, assembly, searchPath) =>
    {
        if (libraryName.IndexOf("QuestPdfSkia", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var candidateDirs = new List<string>();

            if (!string.IsNullOrEmpty(AppContext.BaseDirectory))
                candidateDirs.Add(AppContext.BaseDirectory);

            var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (!string.IsNullOrEmpty(exeDir))
                candidateDirs.Add(exeDir);

            var bundleDir = AppContext.GetData("BUNDLE_EXTRACT_BASE_DIR") as string;
            if (!string.IsNullOrEmpty(bundleDir))
                candidateDirs.Add(bundleDir);

            foreach (var dir in candidateDirs)
            {
                var directFile = Path.Combine(dir, "QuestPdfSkia.dll");
                if (File.Exists(directFile) && NativeLibrary.TryLoad(directFile, out var h1))
                    return h1;

                var runtimeFile = Path.Combine(dir, "runtimes", "win-x64", "native", "QuestPdfSkia.dll");
                if (File.Exists(runtimeFile) && NativeLibrary.TryLoad(runtimeFile, out var h2))
                    return h2;
            }
        }
        return IntPtr.Zero;
    });

    QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    builder.Services.AddSingleton<IPdfGenerator, HotelAutomation.Worker.Services.InvoicePdfGenerator>();
    Console.WriteLine("[INFO] QuestPDF initialized successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"[WARNING] QuestPDF native libraries unavailable: {ex.Message}. Falling back to FallbackPdfGenerator.");
    builder.Services.AddSingleton<IPdfGenerator, HotelAutomation.Worker.Services.FallbackPdfGenerator>();
}

// ── Virtual Printer Installer (Phase 2) ────────────────────────────────
// Runs at startup on Windows. Installs 'Hotel Sankalp Printer' automatically.
// Safely skipped on macOS/Linux (dev machines). Requires PmsConnectorType = "Pdf".
if (pmsType == "Pdf")
{
    builder.Services.AddHostedService<HotelAutomation.Worker.Services.VirtualPrinterInstaller>();
}

// ── Hosted Services (Background Engine) ───────────────────────────────────────
builder.Services.AddHostedService<HotelWorker>();
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<JobProcessor>();
builder.Services.AddHostedService<CampaignProcessor>();
builder.Services.AddHostedService<DateTriggerProcessor>();
builder.Services.AddHostedService<HotelAutomation.Worker.Services.TemplateSyncScheduler>();
builder.Services.AddHostedService<HotelAutomation.Worker.Services.DatabaseBackupScheduler>();
builder.Services.AddHostedService<HotelAutomation.Worker.Services.DatabaseMaintenanceScheduler>();

#if WINDOWS
builder.Services.AddHostedService<TrayIconService>();
#endif

var app = builder.Build();

// Connect the repository to the UnifiedLoggerProvider for database log streaming
var sysLogRepository = app.Services.GetRequiredService<SystemLogRepository>();
unifiedLoggerProvider.SetRepository(sysLogRepository);

// Log application startup details
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("=================================================");
startupLogger.LogInformation("Hotel Automation System starting up on {Url}", "http://localhost:8001");
startupLogger.LogInformation("AppData: {AppDataDir} | Logs: {LogsDir}", appDataDir, logsDir);
startupLogger.LogInformation("OS: {OS} | Process ID: {PID} | CLR: {Version}", Environment.OSVersion, Environment.ProcessId, Environment.Version);
startupLogger.LogInformation("=================================================");

// ── Global Exception Middleware (Captures all unhandled API request crashes) ──
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled API exception on {Method} {Path}", context.Request.Method, context.Request.Path);

        CrashLogger.LogCrash($"API.{context.Request.Method}_{context.Request.Path}", ex,
            $"Query: {context.Request.QueryString} | ClientIP: {context.Connection.RemoteIpAddress}");

        try
        {
            var sysLog = context.RequestServices.GetService<SystemLogRepository>();
            if (sysLog != null)
            {
                await sysLog.LogAsync(
                    "FATAL",
                    "System Crash",
                    $"API {context.Request.Method} {context.Request.Path}",
                    $"Unhandled HTTP error: {ex.Message}",
                    $"Query: {context.Request.QueryString} | User: {context.User.Identity?.Name ?? "Anonymous"}",
                    ex.ToString());
            }
        }
        catch { }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "An internal system error occurred.",
            message = ex.Message,
            path = context.Request.Path.Value
        });
    }
});

// ── Active-user tracker for PDF signature lookup ──────────────────────────────
// Stores the username of whoever is currently logged-in (or impersonated).
// Written on every login / impersonate / stop-impersonate / logout so that
// the PDF connector always stamps the correct user's signature even when the
// printer fires outside a normal HTTP request cycle.
string? _activeSignatureUsername = null;
var _lastActiveMap = new System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(context.User.Identity.Name))
    {
        _activeSignatureUsername = context.User.Identity.Name;
        var userRepo = context.RequestServices.GetRequiredService<UserRepository>();
        // Throttle DB updates: only write LastActiveAt to SQLite once every 5 minutes per user
        var now = DateTimeOffset.UtcNow;
        if (!_lastActiveMap.TryGetValue(context.User.Identity.Name, out var lastWritten) || (now - lastWritten).TotalMinutes >= 5)
        {
            _lastActiveMap[context.User.Identity.Name] = now;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = app.Services.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<UserRepository>();
                    await repo.UpdateLastActiveAsync(context.User.Identity!.Name!);
                }
                catch { /* Non-critical failure */ }
            });
        }
    }
    await next();
});

// ── Seed Default User ─────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var userRepo = scope.ServiceProvider.GetRequiredService<UserRepository>();
    var hasher = scope.ServiceProvider.GetRequiredService<HotelAutomation.Core.Security.PasswordHasher>();
    var defaultHash = hasher.HashPassword("AdminPassword123");
    var srkHash = hasher.HashPassword("Scalifts$789!");
    try
    {
        // Seed default accounts
        userRepo.SeedDefaultOwnerAsync(defaultHash, srkHash).GetAwaiter().GetResult();

        // ── Force-update SRKadmin password on every startup ──────────────────────
        // This ensures the password is always current even if the account already existed.
        var srkUser = userRepo.GetByUsernameAsync("SRKadmin").GetAwaiter().GetResult();
        if (srkUser != null && !hasher.VerifyPassword("Scalifts$789!", srkUser.PasswordHash))
        {
            srkUser.PasswordHash = srkHash;
            userRepo.UpdateAsync(srkUser).GetAwaiter().GetResult();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Non-fatal user initialization warning: {ex.Message}");
    }


    // Bind signature path callback on the PDF connector
    var pdfConnector = scope.ServiceProvider.GetService<HotelAutomation.Connectors.Pms.PdfPmsConnector>();
    if (pdfConnector != null)
    {
        pdfConnector.SignaturesDataDir = signaturesDataDir;
        pdfConnector.GetActiveUserSignaturePath = async () =>
        {
            using var lookupScope = app.Services.CreateScope();
            var lookupRepo = lookupScope.ServiceProvider.GetRequiredService<UserRepository>();

            // 1. Check in-memory tracked username (set on login/impersonate/HTTP middleware)
            var username = _activeSignatureUsername;
            if (!string.IsNullOrEmpty(username))
            {
                var user = await lookupRepo.GetByUsernameAsync(username);
                if (!string.IsNullOrWhiteSpace(user?.SignaturePath))
                    return user.SignaturePath;
            }

            // 2. Fall back to last active user in DB
            var lastActiveUser = await lookupRepo.GetLastActiveUserAsync();
            if (!string.IsNullOrWhiteSpace(lastActiveUser?.SignaturePath))
            {
                return lastActiveUser.SignaturePath;
            }

            // 3. Fall back to any active user with a signature uploaded (preferring Manager -> Owner -> Employer -> Developer)
            var allUsers = await lookupRepo.GetAllAsync();
            var userWithSig = allUsers.FirstOrDefault(u => u.IsActive && !string.IsNullOrWhiteSpace(u.SignaturePath) && u.Role == "Manager")
                           ?? allUsers.FirstOrDefault(u => u.IsActive && !string.IsNullOrWhiteSpace(u.SignaturePath) && u.Role == "Owner")
                           ?? allUsers.FirstOrDefault(u => u.IsActive && !string.IsNullOrWhiteSpace(u.SignaturePath));

            return userWithSig?.SignaturePath;
        };
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();

// Use the AppData uploads folder (writable even when installed in Program Files)
var uploadsPath = uploadsDataDir;
if (!System.IO.Directory.Exists(uploadsPath)) System.IO.Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

// Serve signatures from AppData (writable even when installed in Program Files)
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(signaturesDataDir),
    RequestPath = "/signatures"
});

// ── API Endpoints for UI ──────────────────────────────────────────────────────

// Auth endpoints
app.MapPost("/api/auth/login", async (LoginRequest req, UserRepository userRepo, HotelAutomation.Core.Security.PasswordHasher hasher, ActivityLogRepository activityLog, HttpContext context) =>
{
    var ip = context.Connection.RemoteIpAddress?.ToString();
    var user = await userRepo.GetByUsernameAsync(req.Username);
    if (user == null || !user.IsActive || !hasher.VerifyPassword(req.Password, user.PasswordHash))
    {
        // Log failed login attempt
        _ = activityLog.LogAsync(req.Username ?? "unknown", "LOGIN_FAILED",
            $"Failed login attempt — user not found, inactive, or wrong password", ip);
        return Results.Json(new { error = "Invalid username or password" }, statusCode: 400);
    }

    var claims = new List<System.Security.Claims.Claim>
    {
        new(System.Security.Claims.ClaimTypes.Name, user.Username),
        new(System.Security.Claims.ClaimTypes.Role, user.Role),
        new("UserId", user.Id.ToString())
    };

    var identity = new System.Security.Claims.ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new System.Security.Claims.ClaimsPrincipal(identity);

    await context.SignInAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, principal);
    _activeSignatureUsername = user.Username;   // track for PDF signature

    _ = activityLog.LogAsync(user.Username, "LOGIN", $"Logged in as {user.Role}", ip);

    return Results.Ok(new { username = user.Username, role = user.Role });
});

app.MapPost("/api/auth/logout", async (ActivityLogRepository activityLog, HttpContext context) =>
{
    var username = context.User.Identity?.Name ?? "unknown";
    var ip = context.Connection.RemoteIpAddress?.ToString();
    _activeSignatureUsername = null;   // clear on logout
    await context.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
    _ = activityLog.LogAsync(username, "LOGOUT", null, ip);
    return Results.Ok(new { message = "Logged out successfully" });
});


app.MapGet("/api/auth/me", (HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
        var isImpersonating = context.User.HasClaim(c => c.Type == "OriginalUsername");
        return Results.Ok(new { username = context.User.Identity.Name, role, isImpersonating });
    }
    return Results.Unauthorized();
});

// Impersonate another user (Owner and Developer only)
app.MapPost("/api/auth/impersonate/{id:int}", async (int id, UserRepository userRepo, ActivityLogRepository activityLog, HttpContext context) =>
{
    var isAuthorized = context.User.IsInRole("Owner") || context.User.IsInRole("Developer") || 
                       (context.User.HasClaim(c => c.Type == "OriginalRole") && 
                        (context.User.FindFirst("OriginalRole")?.Value == "Owner" || 
                         context.User.FindFirst("OriginalRole")?.Value == "Developer"));

    if (!isAuthorized)
    {
        return Results.Forbid();
    }

    var targetUser = await userRepo.GetByIdAsync(id);
    if (targetUser == null || !targetUser.IsActive)
    {
        return Results.BadRequest(new { error = "Target user not found or inactive" });
    }

    var originalUsername = context.User.FindFirst("OriginalUsername")?.Value ?? context.User.Identity?.Name ?? "";
    var originalRole = context.User.FindFirst("OriginalRole")?.Value ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Owner";

    var claims = new List<System.Security.Claims.Claim>
    {
        new(System.Security.Claims.ClaimTypes.Name, targetUser.Username),
        new(System.Security.Claims.ClaimTypes.Role, targetUser.Role),
        new("UserId", targetUser.Id.ToString()),
        new("OriginalUsername", originalUsername),
        new("OriginalRole", originalRole)
    };

    var identity = new System.Security.Claims.ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new System.Security.Claims.ClaimsPrincipal(identity);

    await context.SignInAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, principal);
    _activeSignatureUsername = targetUser.Username;   // track impersonated user for PDF signature

    _ = activityLog.LogAsync(originalUsername, "IMPERSONATE",
        $"Started impersonating user '{targetUser.Username}' (Role: {targetUser.Role})",
        context.Connection.RemoteIpAddress?.ToString());

    return Results.Ok(new { username = targetUser.Username, role = targetUser.Role, isImpersonating = true });
}).RequireAuthorization();

// Stop impersonation and restore original Owner session
app.MapPost("/api/auth/stop-impersonating", async (UserRepository userRepo, ActivityLogRepository activityLog, HttpContext context) =>
{
    if (!context.User.HasClaim(c => c.Type == "OriginalUsername"))
    {
        return Results.BadRequest(new { error = "Not currently impersonating a user" });
    }

    var originalUsername = context.User.FindFirst("OriginalUsername")?.Value;
    var originalRole = context.User.FindFirst("OriginalRole")?.Value;
    var currentUsername = context.User.Identity?.Name ?? "unknown";

    if (string.IsNullOrEmpty(originalUsername) || string.IsNullOrEmpty(originalRole))
    {
        return Results.BadRequest(new { error = "Original user details missing" });
    }

    var originalUser = await userRepo.GetByUsernameAsync(originalUsername);
    if (originalUser == null || !originalUser.IsActive)
    {
        return Results.BadRequest(new { error = "Original Owner account is no longer active" });
    }

    var claims = new List<System.Security.Claims.Claim>
    {
        new(System.Security.Claims.ClaimTypes.Name, originalUser.Username),
        new(System.Security.Claims.ClaimTypes.Role, originalUser.Role),
        new("UserId", originalUser.Id.ToString())
    };

    var identity = new System.Security.Claims.ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new System.Security.Claims.ClaimsPrincipal(identity);

    await context.SignInAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, principal);
    _activeSignatureUsername = originalUser.Username;   // restore original user for PDF signature

    _ = activityLog.LogAsync(originalUsername, "STOP_IMPERSONATE",
        $"Stopped impersonating '{currentUsername}', restored own session",
        context.Connection.RemoteIpAddress?.ToString());

    return Results.Ok(new { username = originalUser.Username, role = originalUser.Role, isImpersonating = false });
}).RequireAuthorization();


// User Management (Owner & Developer can view users)
app.MapGet("/api/users", async (UserRepository userRepo, HttpContext context) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
    {
        return Results.Forbid();
    }
    var users = await userRepo.GetAllAsync();
    return Results.Ok(users.Select(u => new { u.Id, u.Username, u.Role, u.IsActive, u.CreatedAt, u.SignaturePath }));
}).RequireAuthorization();

// Create User (Owner & Developer can create users)
app.MapPost("/api/users", async (CreateUserRequest req, UserRepository userRepo, HotelAutomation.Core.Security.PasswordHasher hasher, ActivityLogRepository activityLog, HttpContext context) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
    {
        return Results.Forbid();
    }

    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.Role))
    {
        return Results.BadRequest(new { error = "All fields are required" });
    }

    var normalizedRole = req.Role.Trim();
    if (normalizedRole != "Owner" && normalizedRole != "Developer" && normalizedRole != "Manager" && normalizedRole != "Employer")
    {
        return Results.BadRequest(new { error = "Invalid role" });
    }

    var existing = await userRepo.GetByUsernameAsync(req.Username);
    if (existing != null)
    {
        return Results.BadRequest(new { error = "Username already exists" });
    }

    var newUser = new User
    {
        Username = req.Username.Trim(),
        PasswordHash = hasher.HashPassword(req.Password),
        Role = normalizedRole,
        IsActive = true,
        CreatedAt = DateTimeOffset.Now
    };

    await userRepo.AddAsync(newUser);
    _ = activityLog.LogAsync(context.User.Identity?.Name ?? "unknown", "USER_CREATED",
        $"Created user '{req.Username}' with role '{normalizedRole}'",
        context.Connection.RemoteIpAddress?.ToString());
    return Results.Ok(new { message = "User created successfully" });
}).RequireAuthorization();

// Toggle user status (Owner & Developer)
app.MapPut("/api/users/{id:int}/status", async (int id, ToggleStatusRequest req, UserRepository userRepo, ActivityLogRepository activityLog, HttpContext context) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
    {
        return Results.Forbid();
    }

    var user = await userRepo.GetByIdAsync(id);
    if (user == null)
    {
        return Results.NotFound();
    }

    // Cannot disable themselves
    if (user.Username == context.User.Identity?.Name)
    {
        return Results.BadRequest(new { error = "Cannot disable your own account" });
    }

    user.IsActive = req.IsActive;
    await userRepo.UpdateAsync(user);
    _ = activityLog.LogAsync(context.User.Identity?.Name ?? "unknown", "USER_STATUS_CHANGED",
        $"Set user '{user.Username}' IsActive = {req.IsActive}",
        context.Connection.RemoteIpAddress?.ToString());
    return Results.Ok(new { message = "User status updated successfully" });
}).RequireAuthorization();

// Update user role (Owner & Developer: promote or demote)
app.MapPut("/api/users/{id:int}/role", async (int id, UpdateRoleRequest req, UserRepository userRepo, ActivityLogRepository activityLog, HttpContext context) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
    {
        return Results.Forbid();
    }

    if (string.IsNullOrWhiteSpace(req.Role))
    {
        return Results.BadRequest(new { error = "Role is required" });
    }

    var normalizedRole = req.Role.Trim();
    if (normalizedRole != "Owner" && normalizedRole != "Developer" && normalizedRole != "Manager" && normalizedRole != "Employer")
    {
        return Results.BadRequest(new { error = "Invalid role specified" });
    }

    var user = await userRepo.GetByIdAsync(id);
    if (user == null)
    {
        return Results.NotFound();
    }

    // Protect from demoting themselves if they are the logged in user
    if (user.Username == context.User.Identity?.Name && normalizedRole != user.Role)
    {
        return Results.BadRequest(new { error = "Cannot change your own role. Have another administrator update your role." });
    }

    var oldRole = user.Role;
    user.Role = normalizedRole;
    await userRepo.UpdateAsync(user);
    _ = activityLog.LogAsync(context.User.Identity?.Name ?? "unknown", "USER_ROLE_CHANGED",
        $"Changed role of '{user.Username}' from '{oldRole}' to '{normalizedRole}'",
        context.Connection.RemoteIpAddress?.ToString());
    return Results.Ok(new { message = $"User role updated to {normalizedRole} successfully" });
}).RequireAuthorization();

// Reset a user's password (Owner & Developer)
app.MapPut("/api/users/{id:int}/reset-password", async (int id, ResetPasswordRequest req, UserRepository userRepo, HotelAutomation.Core.Security.PasswordHasher hasher, ActivityLogRepository activityLog, HttpContext context) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
    {
        return Results.Forbid();
    }

    if (string.IsNullOrWhiteSpace(req.Password))
    {
        return Results.BadRequest(new { error = "New password is required" });
    }

    var user = await userRepo.GetByIdAsync(id);
    if (user == null)
    {
        return Results.NotFound();
    }

    user.PasswordHash = hasher.HashPassword(req.Password);
    await userRepo.UpdateAsync(user);
    _ = activityLog.LogAsync(context.User.Identity?.Name ?? "unknown", "USER_PASSWORD_RESET",
        $"Reset password for user '{user.Username}'",
        context.Connection.RemoteIpAddress?.ToString());
    return Results.Ok(new { message = "User password reset successfully" });
}).RequireAuthorization();

// Delete a user (Owner & Developer)
app.MapDelete("/api/users/{id:int}", async (int id, UserRepository userRepo, ActivityLogRepository activityLog, HttpContext context) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
    {
        return Results.Forbid();
    }

    var user = await userRepo.GetByIdAsync(id);
    if (user == null)
    {
        return Results.NotFound();
    }

    // Owner cannot delete themselves
    if (user.Username == context.User.Identity?.Name)
    {
        return Results.BadRequest(new { error = "Cannot delete your own account" });
    }

    await userRepo.DeleteAsync(id);
    _ = activityLog.LogAsync(context.User.Identity?.Name ?? "unknown", "USER_DELETED",
        $"Deleted user '{user.Username}' (Role: {user.Role})",
        context.Connection.RemoteIpAddress?.ToString());
    return Results.Ok(new { message = "User deleted successfully" });
}).RequireAuthorization();


// Upload user signature & stamp (Owner & Developer only)
app.MapPost("/api/users/{id:int}/signature", async (int id, HttpContext context, UserRepository userRepo) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
    {
        return Results.Forbid();
    }

    var user = await userRepo.GetByIdAsync(id);
    if (user == null) return Results.NotFound();

    var form = await context.Request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "No file uploaded" });
    }

    // Save to AppData\signatures (writable even when installed in Program Files)
    var uploadDir = signaturesDataDir;
    if (!Directory.Exists(uploadDir))
    {
        Directory.CreateDirectory(uploadDir);
    }

    var ext = Path.GetExtension(file.FileName);
    if (string.IsNullOrEmpty(ext)) ext = ".png";
    var fileName = $"user_{id}_signature{ext}";
    var filePath = Path.Combine(uploadDir, fileName);

    using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    var relativeUrl = $"/signatures/{fileName}";
    await userRepo.UpdateSignaturePathAsync(id, relativeUrl);

    return Results.Ok(new { message = "Signature uploaded successfully", signaturePath = relativeUrl });
}).RequireAuthorization();

// Get all role permissions (for sidebar dynamic loading)
// ── Activity Log Endpoints (Owner & Developer) ───────────────────────────────
app.MapGet("/api/activity-logs", async (ActivityLogRepository activityLog, HttpContext context, int limit = 200) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
        return Results.Forbid();
    var logs = await activityLog.GetRecentLogsAsync(limit);
    return Results.Ok(logs);
}).RequireAuthorization();

app.MapGet("/api/activity-logs/user/{username}", async (string username, ActivityLogRepository activityLog, HttpContext context, int limit = 100) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
        return Results.Forbid();
    var logs = await activityLog.GetLogsByUserAsync(username, limit);
    return Results.Ok(logs);
}).RequireAuthorization();

// ── System Diagnostics & Crash Log Endpoints (Owner & Developer) ─────────────
app.MapGet("/api/system-logs", async (
    SystemLogRepository logRepo,
    HttpContext context,
    string? category = null,
    string? level = null,
    string? search = null,
    int limit = 100,
    int offset = 0) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
        return Results.Forbid();

    var logs = await logRepo.GetLogsAsync(category, level, search, limit, offset);
    return Results.Ok(logs);
}).RequireAuthorization();

app.MapGet("/api/system-logs/stats", async (SystemLogRepository logRepo, HttpContext context) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
        return Results.Forbid();

    var stats = await logRepo.GetStatsAsync();
    return Results.Ok(stats);
}).RequireAuthorization();

app.MapGet("/api/system-logs/download", async (HttpContext context) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
        return Results.Forbid();

    var logsDirectory = Path.Combine(appDataDir, "logs");
    var todayLog = Path.Combine(logsDirectory, $"app-{DateTime.Now:yyyy-MM-dd}.log");
    if (!File.Exists(todayLog) && Directory.Exists(logsDirectory))
    {
        var latestLog = Directory.GetFiles(logsDirectory, "app-*.log").OrderByDescending(f => f).FirstOrDefault();
        if (latestLog != null) todayLog = latestLog;
    }

    if (!File.Exists(todayLog))
        return Results.NotFound(new { error = "No log files found on disk." });

    var bytes = await File.ReadAllBytesAsync(todayLog);
    return Results.File(bytes, "text/plain", Path.GetFileName(todayLog));
}).RequireAuthorization();

app.MapGet("/api/system-logs/crash-files", (HttpContext context) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
        return Results.Forbid();

    var logsDirectory = Path.Combine(appDataDir, "logs");
    if (!Directory.Exists(logsDirectory)) return Results.Ok(Array.Empty<object>());

    var files = Directory.GetFiles(logsDirectory, "crash-*.log")
        .Select(f => new
        {
            filename = Path.GetFileName(f),
            sizeBytes = new FileInfo(f).Length,
            lastModified = File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm:ss")
        })
        .OrderByDescending(f => f.lastModified)
        .ToList();

    return Results.Ok(files);
}).RequireAuthorization();

app.MapGet("/api/system-logs/crash-files/{filename}", async (string filename, HttpContext context) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
        return Results.Forbid();

    var logsDirectory = Path.Combine(appDataDir, "logs");
    var safeFilename = Path.GetFileName(filename);
    var targetFile = Path.Combine(logsDirectory, safeFilename);

    if (!File.Exists(targetFile) || !safeFilename.StartsWith("crash-", StringComparison.OrdinalIgnoreCase))
        return Results.NotFound(new { error = "Crash log not found." });

    var bytes = await File.ReadAllBytesAsync(targetFile);
    return Results.File(bytes, "text/plain", safeFilename);
}).RequireAuthorization();

app.MapPost("/api/system-logs/clear", async (SystemLogRepository logRepo, HttpContext context, int olderThanDays = 15) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
        return Results.Forbid();

    var deleted = await logRepo.ClearLogsAsync(olderThanDays);
    return Results.Ok(new { message = $"Successfully cleared {deleted} logs older than {olderThanDays} days." });
}).RequireAuthorization();

// ── Email Alert Endpoints (Owner & Developer) ─────────────────────────────────
app.MapGet("/api/alerts/config", (HotelAutomation.Worker.Services.EmailAlertService alertService, HttpContext context) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
        return Results.Forbid();

    var opts = alertService.Options;
    return Results.Ok(new
    {
        enabled = opts.Enabled,
        senderEmail = opts.SenderEmail,
        senderName = opts.SenderName,
        smtpHost = opts.SmtpHost,
        smtpPort = opts.SmtpPort,
        recipients = opts.Recipients,
        deduplicationMinutes = opts.DeduplicationWindowMinutes
    });
}).RequireAuthorization();

app.MapPost("/api/alerts/test", async (HotelAutomation.Worker.Services.EmailAlertService alertService, HttpContext context) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
        return Results.Forbid();

    var user = context.User.Identity?.Name ?? "Admin";
    var result = await alertService.SendDirectAlertAsync(
        "TEST",
        "Diagnostics Verification",
        "Web UI Manual Test",
        $"This is a test alert notification triggered manually by user '{user}'.",
        $"Triggered from IP: {context.Connection.RemoteIpAddress} at {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}. All 4 recipient mailboxes should receive this notification.",
        null
    );

    if (result.Success)
    {
        return Results.Ok(new
        {
            success = true,
            message = $"Test alert successfully sent to {alertService.Options.Recipients.Count} recipients ({string.Join(", ", alertService.Options.Recipients)})"
        });
    }

    return Results.BadRequest(new
    {
        success = false,
        error = $"Failed to send test alert: {result.ErrorMessage}"
    });
}).RequireAuthorization();


app.MapGet("/api/permissions", async (UserRepository userRepo) =>
{
    var perms = await userRepo.GetPermissionsAsync();
    return Results.Ok(perms.Select(p => new { role = p.Role, module = p.Module, isEnabled = p.IsEnabled == 1 }));
}).RequireAuthorization();


// Save role permissions (Owner & Developer only)
app.MapPost("/api/permissions", async (SavePermissionRequest req, UserRepository userRepo, HttpContext context) =>
{
    if (!context.User.IsInRole("Owner") && !context.User.IsInRole("Developer"))
    {
        return Results.Forbid();
    }

    await userRepo.SavePermissionAsync(req.Role, req.Module, req.IsEnabled);
    return Results.Ok(new { message = "Permission saved successfully" });
}).RequireAuthorization();



// Change own password
app.MapPut("/api/users/password", async (ChangePasswordRequest req, UserRepository userRepo, HotelAutomation.Core.Security.PasswordHasher hasher, HttpContext context) =>
{
    var username = context.User.Identity?.Name;
    if (username == null) return Results.Unauthorized();

    var user = await userRepo.GetByUsernameAsync(username);
    if (user == null) return Results.NotFound();

    if (!hasher.VerifyPassword(req.OldPassword, user.PasswordHash))
    {
        return Results.BadRequest(new { error = "Incorrect current password" });
    }

    user.PasswordHash = hasher.HashPassword(req.NewPassword);
    await userRepo.UpdateAsync(user);
    return Results.Ok(new { message = "Password updated successfully" });
}).RequireAuthorization();

app.MapGet("/api/templates", async (ITemplateRepository repo, WhatsAppApiClient whatsappClient, int page = 1, int limit = 50) => 
{
    var (list, total) = await repo.GetAllPaginatedAsync(page, limit);
    
    var metaTemplatesDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    try
    {
        var (success, content, error) = await whatsappClient.GetTemplatesAsync();
        if (success && !string.IsNullOrEmpty(content))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in dataProp.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var nameProp) && item.TryGetProperty("status", out var statusProp))
                    {
                        var name = nameProp.GetString();
                        var status = statusProp.GetString();
                        if (name != null && status != null)
                        {
                            metaTemplatesDict[name] = status;
                        }
                    }
                }
            }
        }
    }
    catch { }

    var enriched = list.Select(t => {
        metaTemplatesDict.TryGetValue(t.ExternalTemplateName, out var status);
        return new {
            t.Id,
            t.Name,
            t.ExternalTemplateName,
            t.LanguageCode,
            t.ChannelType,
            t.BodyParamCount,
            t.FallbackChannelType,
            t.Category,
            t.HeaderType,
            t.BodyText,
            t.HeaderText,
            t.ParameterDefinitionsJson,
            t.FooterText,
            t.ButtonsJson,
            t.CreatedAt,
            Status = status ?? "NOT_FOUND"
        };
    });

    return Results.Ok(new { Templates = enriched, TotalCount = total });
});

app.MapGet("/api/rules", async (SqliteConnectionFactory factory) => 
{
    using var conn = await factory.OpenAsync();
    var rules = await conn.QueryAsync<HotelAutomation.Core.Models.AutomationRule>("SELECT * FROM AutomationRules");
    return rules;
});

app.MapGet("/api/outbox", async (SqliteConnectionFactory factory) => 
{
    using var conn = await factory.OpenAsync();
    var outbox = await conn.QueryAsync("SELECT * FROM OutboxMessages ORDER BY CreatedAt DESC LIMIT 50");
    return outbox;
});

app.MapGet("/api/logs", async (EventLogRepository repo) => 
{
    return await repo.GetRecentLogsAsync(100);
});

// Boilerplate stubs to support UI Builder saves if not implemented
app.MapPost("/api/templates", async (HotelAutomation.Core.Models.MessageTemplate template, ITemplateRepository repo, WhatsAppApiClient whatsappClient, Microsoft.Extensions.Logging.ILogger<Program> logger, bool localOnly = false) => 
{
    var trimmed = template with 
    { 
        Name = template.Name.Trim(), 
        ExternalTemplateName = template.ExternalTemplateName.Trim(), 
        LanguageCode = template.LanguageCode?.Trim() ?? "en" 
    };

    int paramCount = trimmed.BodyParamCount;
    List<HotelAutomation.Core.Models.ParameterDefinition>? paramDefs = null;
    if (!string.IsNullOrEmpty(trimmed.ParameterDefinitionsJson))
    {
        try
        {
            paramDefs = System.Text.Json.JsonSerializer.Deserialize<List<HotelAutomation.Core.Models.ParameterDefinition>>(trimmed.ParameterDefinitionsJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (paramDefs != null)
            {
                paramCount = paramDefs.Count;
            }
        }
        catch { }
    }

    var finalTemplate = trimmed with { BodyParamCount = paramCount };

    // If localOnly was explicitly requested, bypass Meta API completely and save directly to local SQLite
    if (localOnly)
    {
        logger.LogInformation("Saving template '{Name}' locally (bypassing Meta API call).", finalTemplate.Name);
        var localId = await repo.AddAsync(finalTemplate);
        return Results.Ok(new { id = localId, message = "Template saved locally." });
    }

    // Build ordered sample values list (position 1..N), falling back to placeholder if blank
    var orderedSamples = new List<string>();
    if (paramDefs != null && paramDefs.Count > 0)
    {
        var sorted = paramDefs.OrderBy(d => d.Position).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            var sv = sorted[i].SampleValue;
            orderedSamples.Add(string.IsNullOrWhiteSpace(sv) ? $"Sample{i + 1}" : sv);
        }
    }

    // Register template in Meta WABA
    var components = new List<object>();

    if (!string.Equals(finalTemplate.HeaderType, "None", StringComparison.OrdinalIgnoreCase))
    {
        if (string.Equals(finalTemplate.HeaderType, "Text", StringComparison.OrdinalIgnoreCase))
        {
            // Check if header text contains a variable like {{1}}
            bool headerHasVar = !string.IsNullOrEmpty(finalTemplate.HeaderText) &&
                                System.Text.RegularExpressions.Regex.IsMatch(finalTemplate.HeaderText, @"\{\{\d+\}\}");
            if (headerHasVar && orderedSamples.Count > 0)
            {
                components.Add(new {
                    type = "HEADER",
                    format = "TEXT",
                    text = finalTemplate.HeaderText ?? string.Empty,
                    example = new { header_text = new[] { orderedSamples[0] } }
                });
            }
            else
            {
                components.Add(new {
                    type = "HEADER",
                    format = "TEXT",
                    text = finalTemplate.HeaderText ?? string.Empty
                });
            }
        }
        else
        {
            components.Add(new {
                type = "HEADER",
                format = finalTemplate.HeaderType.ToUpperInvariant()
            });
        }
    }

    // Include example body_text if we have sample values - Meta requires this for templates with variables
    if (orderedSamples.Count > 0)
    {
        components.Add(new {
            type = "BODY",
            text = finalTemplate.BodyText ?? string.Empty,
            example = new { body_text = new[] { orderedSamples.ToArray() } }
        });
    }
    else
    {
        components.Add(new {
            type = "BODY",
            text = finalTemplate.BodyText ?? string.Empty
        });
    }

    if (!string.IsNullOrEmpty(finalTemplate.FooterText))
    {
        components.Add(new {
            type = "FOOTER",
            text = finalTemplate.FooterText
        });
    }

    if (!string.IsNullOrEmpty(finalTemplate.ButtonsJson))
    {
        try
        {
            var rawButtons = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(finalTemplate.ButtonsJson);
            if (rawButtons != null && rawButtons.Count > 0)
            {
                var formattedButtons = new List<object>();
                foreach (var btn in rawButtons)
                {
                    if (btn.TryGetProperty("type", out var typeProp))
                    {
                        var typeVal = typeProp.GetString();
                        if (typeVal == "QUICK_REPLY")
                        {
                            formattedButtons.Add(new {
                                type = "QUICK_REPLY",
                                text = btn.GetProperty("text").GetString()
                            });
                        }
                        else if (typeVal == "URL")
                        {
                            formattedButtons.Add(new {
                                type = "URL",
                                text = btn.GetProperty("text").GetString(),
                                url = btn.GetProperty("url").GetString()
                            });
                        }
                        else if (typeVal == "PHONE_NUMBER")
                        {
                            formattedButtons.Add(new {
                                type = "PHONE_NUMBER",
                                text = btn.GetProperty("text").GetString(),
                                phone_number = btn.GetProperty("phone_number").GetString()
                            });
                        }
                    }
                }

                if (formattedButtons.Count > 0)
                {
                    components.Add(new {
                        type = "BUTTONS",
                        buttons = formattedButtons
                    });
                }
            }
        }
        catch { }
    }

    var metaPayload = new {
        name = finalTemplate.ExternalTemplateName,
        category = finalTemplate.Category.ToUpperInvariant(),
        language = finalTemplate.LanguageCode,
        components = components
    };

    var (success, content, error) = await whatsappClient.CreateTemplateAsync(metaPayload);
    if (!success)
    {
        logger.LogError("Meta API rejected template creation. Response: {MetaError}", error ?? "Unknown error");
        var rawErr = error ?? "Unknown error";
        return Results.BadRequest(new { 
            message = rawErr.Contains("80008") 
                ? "Meta Rate Limit (#80008): Meta is currently throttling requests. You can save this template locally by clicking 'Save Locally'."
                : $"Meta API Error: {rawErr}",
            isRateLimited = rawErr.Contains("80008")
        });
    }

    var id = await repo.AddAsync(finalTemplate);
    return Results.Ok(new { id });
});

app.MapPost("/api/templates/{id}/test", async (int id, [Microsoft.AspNetCore.Mvc.FromBody] System.Text.Json.JsonElement body, ITemplateRepository templateRepo, IOutboxRepository outboxRepo, Microsoft.Extensions.Logging.ILogger<Program> logger) => 
{
    var phoneNumber = body.TryGetProperty("phoneNumber", out var p) ? p.GetString() : null;
    if (string.IsNullOrWhiteSpace(phoneNumber))
    {
        return Results.BadRequest(new { message = "Phone number is required." });
    }

    var template = await templateRepo.GetByIdAsync(id);
    if (template == null)
    {
        return Results.NotFound(new { message = "Template not found." });
    }

    var parameters = new List<string>();
    if (!string.IsNullOrEmpty(template.ParameterDefinitionsJson))
    {
        try
        {
            var defs = System.Text.Json.JsonSerializer.Deserialize<List<HotelAutomation.Core.Models.ParameterDefinition>>(template.ParameterDefinitionsJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (defs != null && defs.Count > 0)
            {
                parameters = defs
                    .OrderBy(d => d.Position)
                    .Select(d => !string.IsNullOrEmpty(d.SampleValue) ? d.SampleValue : $"Param{d.Position}")
                    .ToList();
            }
        }
        catch { }
    }

    if (parameters.Count == 0)
    {
        int count = template.BodyParamCount > 0 ? template.BodyParamCount : 0;
        if (count > 0)
        {
            var dummyData = new[] { "Test User", "101", "2500", "Sankalp Retreat", "9 Jul 2026", "12 Jul 2026", "TestGuest@2026", "BK-TEST-101" };
            for (int i = 0; i < count; i++)
            {
                parameters.Add(i < dummyData.Length ? dummyData[i] : $"TestParam{i + 1}");
            }
        }
    }

    var msg = new HotelAutomation.Core.Models.OutboxMessage
    {
        ToPhoneNumber = phoneNumber,
        TemplateId = template.Id,
        ParametersJson = System.Text.Json.JsonSerializer.Serialize(parameters)
    };
    
    try
    {
        await outboxRepo.AddAsync(msg);
        return Results.Ok(new { message = "Test message queued successfully!" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to queue test message for template {TemplateId}", id);
        return Results.StatusCode(500);
    }
});

app.MapPut("/api/templates/{id}", async (int id, HotelAutomation.Core.Models.MessageTemplate template, ITemplateRepository repo) => 
{
    var trimmed = template with 
    { 
        Name = template.Name.Trim(), 
        ExternalTemplateName = template.ExternalTemplateName.Trim(), 
        LanguageCode = template.LanguageCode?.Trim() ?? "en" 
    };

    int paramCount = trimmed.BodyParamCount;
    if (!string.IsNullOrEmpty(trimmed.ParameterDefinitionsJson))
    {
        try
        {
            var defs = System.Text.Json.JsonSerializer.Deserialize<List<HotelAutomation.Core.Models.ParameterDefinition>>(trimmed.ParameterDefinitionsJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (defs != null)
            {
                paramCount = defs.Count;
            }
        }
        catch { }
    }

    var finalTemplate = trimmed with { BodyParamCount = paramCount };
    var success = await repo.UpdateAsync(id, finalTemplate);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapDelete("/api/templates/{id}", async (int id, ITemplateRepository repo, WhatsAppApiClient whatsappClient) => 
{
    try 
    {
        var template = await repo.GetByIdAsync(id);
        if (template != null)
        {
            await whatsappClient.DeleteTemplateAsync(template.ExternalTemplateName);
        }
        var success = await repo.DeleteAsync(id);
        return success ? Results.Ok(new { message = "Template deleted successfully" }) : Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/templates/sync", async (HotelAutomation.Worker.Services.TemplateSyncService syncService, HttpContext context) => 
{
    var triggeredBy = context.User.Identity?.Name ?? "Manual";
    var (success, count, message) = await syncService.SyncTemplatesAsync(triggeredBy);
    return success 
        ? Results.Ok(new { message, addedCount = count }) 
        : Results.BadRequest(new { message });
});


// ── Template Variants API (A/B Testing) ──────────────────────────────────────
app.MapGet("/api/templates/{templateId}/variants", async (int templateId, TemplateVariantRepository repo) => 
{
    return Results.Ok(await repo.GetByTemplateIdAsync(templateId));
});

app.MapPost("/api/templates/{templateId}/variants", async (int templateId, HotelAutomation.Core.Models.TemplateVariant variant, TemplateVariantRepository repo) => 
{
    if (string.IsNullOrWhiteSpace(variant.VariantName))
        return Results.BadRequest("VariantName is required.");
    if (string.IsNullOrWhiteSpace(variant.Body))
        return Results.BadRequest("Variant Body is required.");
    if (variant.Weight < 1)
        return Results.BadRequest("Weight must be at least 1.");

    var newVariant = variant with { TemplateId = templateId, CreatedAt = DateTimeOffset.UtcNow };
    await repo.AddAsync(newVariant);
    return Results.Ok(new { message = "Variant added." });
});

app.MapDelete("/api/templates/{templateId}/variants/{variantId}", async (int variantId, TemplateVariantRepository repo) => 
{
    await repo.DeleteAsync(variantId);
    return Results.Ok(new { message = "Variant deleted." });
});

// ── Template Variant Analytics ────────────────────────────────────────────────
app.MapGet("/api/templates/{templateId}/variants/analytics", async (int templateId, SqliteConnectionFactory dbFactory) => 
{
    await using var conn = await dbFactory.OpenAsync();
    var sql = @"
        SELECT 
            v.Id as VariantId, 
            v.VariantName, 
            COUNT(o.Id) as TotalSent,
            SUM(CASE WHEN o.Status = 'Sent' THEN 1 ELSE 0 END) as Delivered,
            SUM(CASE WHEN o.Status = 'Failed' THEN 1 ELSE 0 END) as Failed
        FROM TemplateVariants v
        LEFT JOIN OutboxMessages o ON o.VariantId = v.Id
        WHERE v.TemplateId = @templateId
        GROUP BY v.Id, v.VariantName";
    
    var results = await conn.QueryAsync(sql, new { templateId });
    return Results.Ok(results);
});

// ── Smart Lists API ──────────────────────────────────────────────────────────
app.MapGet("/api/smart-lists", async (ISmartListRepository repo) => 
{
    return await repo.GetAllAsync();
});

app.MapPost("/api/smart-lists", async (HotelAutomation.Core.Models.SmartList list, ISmartListRepository repo) => 
{
    if (string.IsNullOrWhiteSpace(list.ListName) || string.IsNullOrWhiteSpace(list.FilterConditionsJson))
        return Results.BadRequest(new { message = "List Name and Filters are required." });
        
    var id = await repo.AddAsync(list);
    return Results.Ok(new { Id = id });
});

app.MapDelete("/api/smart-lists/{id}", async (int id, ISmartListRepository repo) => 
{
    var success = await repo.DeleteAsync(id);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapGet("/api/smart-lists/{id}/contacts", async (int id, ISmartListRepository listRepo, ContactRepository contactRepo, int page = 1, int limit = 50) => 
{
    var smartList = await listRepo.GetByIdAsync(id);
    if (smartList == null) return Results.NotFound();
    
    var (list, total) = await contactRepo.GetBySmartListAsync(smartList, page, limit);
    return Results.Ok(new { Contacts = list, TotalCount = total });
});

// ── Campaigns API ────────────────────────────────────────────────────────────
app.MapGet("/api/campaigns", async (ICampaignRepository repo) => await repo.GetAllAsync());

app.MapGet("/api/campaigns/{id}", async (int id, ICampaignRepository repo) => 
{
    var campaign = await repo.GetByIdAsync(id);
    return campaign != null ? Results.Ok(campaign) : Results.NotFound();
});

app.MapPost("/api/campaigns", async (HotelAutomation.Core.Models.Campaign campaign, ICampaignRepository repo) => 
{
    if (string.IsNullOrWhiteSpace(campaign.Name)) return Results.BadRequest("Name is required");
    var id = await repo.CreateAsync(campaign);
    return Results.Ok(new { Id = id });
});

app.MapPut("/api/campaigns/{id}", async (int id, HotelAutomation.Core.Models.Campaign campaign, ICampaignRepository repo) => 
{
    var success = await repo.UpdateAsync(id, campaign);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapDelete("/api/campaigns/{id}", async (int id, ICampaignRepository repo) => 
{
    var success = await repo.DeleteAsync(id);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapPost("/api/campaigns/{id}/enroll", async (int id, int smartListId, ICampaignRepository campRepo, ISmartListRepository slRepo, ContactRepository contactRepo, SqliteConnectionFactory factory) => 
{
    var campaign = await campRepo.GetByIdAsync(id);
    if (campaign == null || !campaign.Steps.Any()) return Results.BadRequest("Invalid campaign or no steps.");
    
    var smartList = await slRepo.GetByIdAsync(smartListId);
    if (smartList == null) return Results.BadRequest("Invalid Smart List.");
    
    // For large lists, we shouldn't fetch all into memory, but this is sufficient for MVP
    var (contacts, _) = await contactRepo.GetBySmartListAsync(smartList, 1, 10000);
    
    await using var conn = await factory.OpenAsync();
    using var transaction = conn.BeginTransaction();
    
    var firstStep = campaign.Steps.OrderBy(s => s.StepNumber).First();
    var nextTime = DateTimeOffset.UtcNow.AddMinutes(firstStep.DelayMinutes).ToString("O");
    
    var sql = @"
        INSERT OR IGNORE INTO ContactCampaignProgress (ContactId, CampaignStepId, Status, NextExecutionTime)
        VALUES (@ContactId, @CampaignStepId, 'Pending', @NextExecutionTime);
    ";
    
    await conn.ExecuteAsync(sql, contacts.Select(c => new
    {
        ContactId = c.Id,
        CampaignStepId = firstStep.Id,
        NextExecutionTime = nextTime
    }), transaction);
    
    transaction.Commit();
    return Results.Ok(new { EnrolledCount = contacts.Count });
});

app.MapPost("/api/rules", async (HotelAutomation.Core.Models.AutomationRule rule, SqliteConnectionFactory factory) => 
{
    using var conn = await factory.OpenAsync();
    var sql = @"INSERT INTO AutomationRules (Name, Description, TriggerEventType, TemplateId, IsEnabled, ConditionsJson, AudienceFilter, DelayMinutes, YesTemplateId, NoTemplateId) 
                VALUES (@Name, @Description, @TriggerEventType, @TemplateId, @IsEnabled, @ConditionsJson, @AudienceFilter, @DelayMinutes, @YesTemplateId, @NoTemplateId);
                SELECT last_insert_rowid();";
    var id = await conn.ExecuteScalarAsync<int>(sql, rule);
    return Results.Ok(new { id, rule.Name, rule.Description, rule.TriggerEventType, rule.TemplateId, rule.IsEnabled, rule.ConditionsJson, rule.AudienceFilter, rule.DelayMinutes, rule.YesTemplateId, rule.NoTemplateId });
});

app.MapPut("/api/rules/{id}", async (int id, HotelAutomation.Core.Models.AutomationRule rule, SqliteConnectionFactory factory) => 
{
    using var conn = await factory.OpenAsync();
    var sql = @"UPDATE AutomationRules 
                SET Name = @Name,
                    Description = @Description,
                    TriggerEventType = @TriggerEventType, 
                    TemplateId = @TemplateId, 
                    IsEnabled = @IsEnabled, 
                    ConditionsJson = @ConditionsJson, 
                    AudienceFilter = @AudienceFilter,
                    DelayMinutes = @DelayMinutes,
                    YesTemplateId = @YesTemplateId,
                    NoTemplateId = @NoTemplateId
                WHERE Id = @Id";
    var rows = await conn.ExecuteAsync(sql, new { rule.Name, rule.Description, rule.TriggerEventType, rule.TemplateId, rule.IsEnabled, rule.ConditionsJson, rule.AudienceFilter, rule.DelayMinutes, rule.YesTemplateId, rule.NoTemplateId, Id = id });
    return rows > 0 ? Results.Ok(new { id }) : Results.NotFound();
});

app.MapPut("/api/rules/{id}/toggle", async (int id, SqliteConnectionFactory factory) => 
{
    using var conn = await factory.OpenAsync();
    var rule = await conn.QueryFirstOrDefaultAsync<HotelAutomation.Core.Models.AutomationRule>("SELECT * FROM AutomationRules WHERE Id = @Id", new { Id = id });
    if (rule == null) return Results.NotFound();
    
    var newStatus = !rule.IsEnabled;
    var rows = await conn.ExecuteAsync("UPDATE AutomationRules SET IsEnabled = @IsEnabled WHERE Id = @Id", new { IsEnabled = newStatus, Id = id });
    return rows > 0 ? Results.Ok() : Results.NotFound();
});

app.MapGet("/api/rules/{id}/stats", async (int id, SqliteConnectionFactory factory) => 
{
    using var conn = await factory.OpenAsync();
    var triggered = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OutboxMessages WHERE RuleId = @Id", new { Id = id });
    var totalSent = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OutboxMessages WHERE RuleId = @Id AND Status = 'Sent'", new { Id = id });
    var failed = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OutboxMessages WHERE RuleId = @Id AND Status = 'Failed'", new { Id = id });
    var lastRunStr = await conn.ExecuteScalarAsync<string>("SELECT CreatedAt FROM OutboxMessages WHERE RuleId = @Id ORDER BY CreatedAt DESC LIMIT 1", new { Id = id });
    
    DateTime? lastRun = null;
    if (DateTime.TryParse(lastRunStr, out var parsed)) lastRun = parsed;

    return Results.Ok(new {
        Triggered = triggered,
        TotalSent = totalSent,
        Failed = failed,
        LastRun = lastRun
    });
});

app.MapGet("/api/rules/{id}/failures", async (int id, SqliteConnectionFactory factory) => 
{
    await using var conn = await factory.OpenAsync();
    var failures = await conn.QueryAsync(
        "SELECT ToPhoneNumber, FailureReason, CreatedAt FROM OutboxMessages WHERE RuleId = @id AND Status = 'Failed' ORDER BY CreatedAt DESC LIMIT 100", 
        new { id });
    return Results.Ok(failures);
});

// Per-step execution counts for a rule
app.MapGet("/api/rules/{id}/step-counts", async (int id, SqliteConnectionFactory factory) =>
{
    using var conn = await factory.OpenAsync();
    var rows = await conn.QueryAsync(
        "SELECT StepType, Count, LastAt FROM RuleExecutionCounts WHERE RuleId = @Id",
        new { Id = id });
    // Convert to a dict keyed by StepType for easy frontend consumption
    var result = rows.ToDictionary(
        r => (string)r.StepType,
        r => new { count = (long)r.Count, lastAt = (string?)r.LastAt }
    );
    return Results.Ok(result);
});

app.MapGet("/api/rules/{id}/analytics-details", async (int id, SqliteConnectionFactory factory) => 
{
    await using var conn = await factory.OpenAsync();
    var details = await conn.QueryAsync(@"
        SELECT o.ToPhoneNumber, o.Status, o.FailureReason, o.CreatedAt, 
               CASE WHEN u.PhoneNumber IS NOT NULL THEN 1 ELSE 0 END as IsUnsubscribed 
        FROM OutboxMessages o
        LEFT JOIN UnsubscribedContacts u ON o.ToPhoneNumber = u.PhoneNumber
        WHERE o.RuleId = @id 
        ORDER BY o.CreatedAt DESC LIMIT 500", new { id });
    return Results.Ok(details);
});

app.MapGet("/api/rules/{id}/contacts-log", async (int id, int? page, int? pageSize, string? status, SqliteConnectionFactory factory) =>
{
    var pg = page ?? 1;
    var ps = pageSize ?? 50;
    var offset = (pg - 1) * ps;
    using var conn = await factory.OpenAsync();

    string whereClause = "WHERE o.RuleId = @id";
    if (!string.IsNullOrEmpty(status))
    {
        whereClause += " AND o.Status = @status";
    }

    var total = await conn.ExecuteScalarAsync<int>(
        $"SELECT COUNT(*) FROM OutboxMessages o {whereClause}", new { id, status });

    var rows = await conn.QueryAsync($@"
        SELECT 
            o.Id,
            o.ToPhoneNumber,
            COALESCE(c.FirstName || ' ' || c.LastName, c.FirstName, c.LastName, '') as GuestName,
            o.Status,
            o.CreatedAt,
            o.SentAt,
            COALESCE(o.FailureReason, o.ErrorMessage) as FailureReason,
            o.RetryCount
        FROM OutboxMessages o
        LEFT JOIN Contacts c ON o.ToPhoneNumber = c.PhoneNumber
        {whereClause}
        ORDER BY o.CreatedAt DESC
        LIMIT @ps OFFSET @offset",
        new { id, status, ps, offset });

    return Results.Ok(new { total, page = pg, pageSize = ps, rows });
});

app.MapGet("/api/broadcast/analytics-details", async (int templateId, string startedAt, SqliteConnectionFactory factory) => 
{
    await using var conn = await factory.OpenAsync();
    var details = await conn.QueryAsync(@"
        SELECT o.ToPhoneNumber, o.Status, o.FailureReason, o.CreatedAt,
               CASE WHEN u.PhoneNumber IS NOT NULL THEN 1 ELSE 0 END as IsUnsubscribed 
        FROM OutboxMessages o
        LEFT JOIN UnsubscribedContacts u ON o.ToPhoneNumber = u.PhoneNumber
        WHERE o.TemplateId = @TemplateId AND o.CreatedAt = @StartedAt 
        ORDER BY o.CreatedAt DESC LIMIT 500", new { TemplateId = templateId, StartedAt = startedAt });
    return Results.Ok(details);
});

app.MapDelete("/api/rules/{id}", async (int id, SqliteConnectionFactory factory) => 
{
    using var conn = await factory.OpenAsync();
    using var tx = conn.BeginTransaction();
    try 
    {
        await conn.ExecuteAsync("UPDATE OutboxMessages SET RuleId = NULL WHERE RuleId = @Id", new { Id = id }, tx);
        await conn.ExecuteAsync("UPDATE ScheduledJobs SET RuleId = NULL WHERE RuleId = @Id", new { Id = id }, tx);
        var rows = await conn.ExecuteAsync("DELETE FROM AutomationRules WHERE Id = @Id", new { Id = id }, tx);
        tx.Commit();
        return rows > 0 ? Results.Ok() : Results.NotFound();
    }
    catch 
    {
        tx.Rollback();
        throw;
    }
});

// ── Opt-Outs Endpoint ──────────────────────────────────────────────────────
app.MapGet("/api/unsubscribes", async (SqliteConnectionFactory factory) => 
{
    await using var conn = await factory.OpenAsync();
    var unsubscribes = await conn.QueryAsync(@"
        SELECT u.PhoneNumber, u.UnsubscribedAt, u.Source, c.FirstName, c.LastName 
        FROM UnsubscribedContacts u
        LEFT JOIN Contacts c ON u.PhoneNumber = c.PhoneNumber
        ORDER BY u.UnsubscribedAt DESC
    ");
    return Results.Ok(unsubscribes);
});

app.MapDelete("/api/unsubscribes/{phoneNumber}", async (string phoneNumber, UnsubscribedContactRepository repo) => 
{
    await repo.RemoveAsync(phoneNumber);
    return Results.Ok();
});

// ── Preference Center API ──────────────────────────────────────────────────
app.MapGet("/api/preferences/{contactId}", async (int contactId, ContactConsentRepository repo) => 
{
    var prefs = await repo.GetConsentPreferencesAsync(contactId);
    return Results.Ok(prefs);
});

app.MapPost("/api/preferences/{contactId}", async (int contactId, List<HotelAutomation.Core.Models.ContactPreferenceUpdate> updates, ContactConsentRepository repo) => 
{
    foreach (var update in updates)
    {
        await repo.UpsertConsentAsync(contactId, update.CategoryId, update.IsSubscribed);
    }
    return Results.Ok();
});

// ── Broadcast Campaign Endpoint ────────────────────────────────────────────
app.MapPost("/api/broadcast", async (BroadcastRequest req, BroadcastRepository broadcastRepo) =>
{
    if (req.TemplateId <= 0) return Results.BadRequest("TemplateId is required.");
    var count = await broadcastRepo.QueueBroadcastAsync(req.TemplateId, req.CategoryId, req.CustomParametersJson ?? "{}");
    return Results.Ok(new { queued = count, message = $"{count} messages queued for broadcast." });
});

app.MapGet("/api/broadcast", async (int? page, int? limit, BroadcastRepository broadcastRepo) =>
{
    var p = page ?? 1;
    var l = limit ?? 50;
    var (data, total) = await broadcastRepo.GetRecentBroadcastsPagedAsync(p, l);
    return Results.Ok(new { data, total });
});

app.MapDelete("/api/broadcast", async (int templateId, string startedAt, BroadcastRepository broadcastRepo) => 
{
    var stoppedCount = await broadcastRepo.StopBroadcastAsync(templateId, startedAt);
    return Results.Ok(new { stopped = stoppedCount, message = $"Stopped {stoppedCount} pending messages." });
});

app.MapDelete("/api/broadcast/history", async (int templateId, string startedAt, BroadcastRepository broadcastRepo) => 
{
    var deletedCount = await broadcastRepo.DeleteBroadcastAsync(templateId, startedAt);
    return Results.Ok(new { deleted = deletedCount, message = $"Deleted {deletedCount} messages from broadcast history." });
});

app.MapPost("/api/test-event", (string? phone, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "PMS_CHECKOUT",
        GuestName = "Test Guest",
        PhoneNumber = string.IsNullOrWhiteSpace(phone) ? "+1234567890" : phone,
        RoomNumber = "101",
        Amount = 150.00m,
        Timestamp = DateTimeOffset.UtcNow
    });
    return Results.Ok(new { message = "Test event fired successfully" });
});



// ── Analytics Overview Endpoints (Part B) ──

app.MapGet("/api/analytics/overview", async (string? startDate, string? endDate, SqliteConnectionFactory dbFactory) =>
{
    using var conn = await dbFactory.OpenAsync();
    
    string dateFilter = "";
    var parameters = new DynamicParameters();
    
    if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
    {
        // Add time boundaries if they are just dates
        var start = startDate.Contains("T") ? startDate : startDate + "T00:00:00Z";
        var end = endDate.Contains("T") ? endDate : endDate + "T23:59:59Z";
        dateFilter = "WHERE CreatedAt >= @start AND CreatedAt <= @end";
        parameters.Add("start", start);
        parameters.Add("end", end);
    }

    var totalTriggered = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM OutboxMessages {dateFilter}", parameters);
    var sent = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM OutboxMessages WHERE Status = 'Sent' {(string.IsNullOrEmpty(dateFilter) ? "" : "AND CreatedAt >= @start AND CreatedAt <= @end")}", parameters);
    var failed = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM OutboxMessages WHERE Status = 'Failed' {(string.IsNullOrEmpty(dateFilter) ? "" : "AND CreatedAt >= @start AND CreatedAt <= @end")}", parameters);
    var pending = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM OutboxMessages WHERE Status = 'Pending' {(string.IsNullOrEmpty(dateFilter) ? "" : "AND CreatedAt >= @start AND CreatedAt <= @end")}", parameters);
    var delivered = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM OutboxMessages WHERE Status = 'Delivered' {(string.IsNullOrEmpty(dateFilter) ? "" : "AND CreatedAt >= @start AND CreatedAt <= @end")}", parameters);
    var read = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM OutboxMessages WHERE Status = 'Read' {(string.IsNullOrEmpty(dateFilter) ? "" : "AND CreatedAt >= @start AND CreatedAt <= @end")}", parameters);
    var clicked = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(DISTINCT o.Id) FROM OutboxMessages o INNER JOIN ShortenedLinks s ON o.Id = s.OutboxMessageId WHERE s.ClickedCount > 0 {(string.IsNullOrEmpty(dateFilter) ? "" : "AND o.CreatedAt >= @start AND o.CreatedAt <= @end")}", parameters);
    
    var totalInbox = 0;

    return Results.Ok(new
    {
        TotalTriggered = totalTriggered,
        Sent = sent,
        Delivered = delivered,
        Read = read,
        Failed = failed,
        Pending = pending,
        IncomingReplies = totalInbox,
        Clicked = clicked
    });
});

app.MapGet("/api/analytics/delivery-trends", async (string? startDate, string? endDate, SqliteConnectionFactory dbFactory) =>
{
    using var conn = await dbFactory.OpenAsync();
    
    string dateFilter = "WHERE CreatedAt >= date('now', '-7 days')";
    var parameters = new DynamicParameters();

    if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
    {
        var start = startDate.Contains("T") ? startDate : startDate + "T00:00:00Z";
        var end = endDate.Contains("T") ? endDate : endDate + "T23:59:59Z";
        dateFilter = "WHERE CreatedAt >= @start AND CreatedAt <= @end";
        parameters.Add("start", start);
        parameters.Add("end", end);
    }

    var trends = await conn.QueryAsync($@"
        SELECT 
            SUBSTR(CreatedAt, 1, 10) as Day,
            SUM(CASE WHEN Status IN ('Sent', 'Delivered', 'Read') THEN 1 ELSE 0 END) as Success,
            SUM(CASE WHEN Status = 'Failed' THEN 1 ELSE 0 END) as Failed
        FROM OutboxMessages
        {dateFilter}
        GROUP BY Day
        ORDER BY Day ASC;
    ", parameters);
    return Results.Ok(trends);
});

app.MapGet("/api/l/{shortCode}", async (string shortCode, SqliteConnectionFactory dbFactory) =>
{
    using var conn = await dbFactory.OpenAsync();
    var link = await conn.QueryFirstOrDefaultAsync("SELECT OriginalUrl, OutboxMessageId FROM ShortenedLinks WHERE ShortCode = @shortCode", new { shortCode });
    if (link == null)
    {
        return Results.NotFound("Link not found.");
    }

    // Record the click asynchronously in the background so we don't slow down redirect
    _ = Task.Run(async () =>
    {
        try
        {
            using var bgConn = await dbFactory.OpenAsync();
            var now = DateTimeOffset.UtcNow.ToString("O");
            await bgConn.ExecuteAsync(@"
                UPDATE ShortenedLinks 
                SET ClickedCount = ClickedCount + 1,
                    FirstClickedAt = COALESCE(FirstClickedAt, @now),
                    LastClickedAt = @now
                WHERE ShortCode = @shortCode", new { now, shortCode });
            
            // Optionally, we could update the OutboxMessage status to Clicked here, 
            // but reading the ShortenedLinks table is safer and preserves the Read status timeline.
        }
        catch { }
    });

    return Results.Redirect((string)link.OriginalUrl, permanent: false);
});

app.MapGet("/api/analytics/export", async (SqliteConnectionFactory dbFactory) =>
{
    using var conn = await dbFactory.OpenAsync();
    var messages = await conn.QueryAsync(@"
        SELECT 
            o.Id, 
            o.ToPhoneNumber, 
            t.Name as TemplateName, 
            o.Status, 
            o.FailureReason, 
            o.CreatedAt 
        FROM OutboxMessages o
        LEFT JOIN MessageTemplates t ON o.TemplateId = t.Id
        ORDER BY o.CreatedAt DESC;
    ");

    var sb = new System.Text.StringBuilder();
    sb.AppendLine("MessageId,Recipient,Template,Status,FailureReason,Timestamp");
    foreach (var m in messages)
    {
        var reason = (m.FailureReason ?? "").Replace(",", " ");
        sb.AppendLine($"{m.Id},{m.ToPhoneNumber},{m.TemplateName},{m.Status},{reason},{m.CreatedAt}");
    }

    return Results.Bytes(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "MessageReport.csv");
});

app.MapGet("/api/webhooks/whatsapp", (HttpContext ctx, Microsoft.Extensions.Options.IOptions<WhatsAppOptions> options) => 
{
    var mode = ctx.Request.Query["hub.mode"].ToString();
    var verifyToken = ctx.Request.Query["hub.verify_token"].ToString();
    var challenge = ctx.Request.Query["hub.challenge"].ToString();

    var expectedToken = options.Value.VerifyToken;
    if (string.IsNullOrEmpty(expectedToken))
    {
        expectedToken = "sankalp_retreat_verify_token"; // Default fallback
    }

    if (mode == "subscribe" && verifyToken == expectedToken)
    {
        return Results.Text(challenge, "text/plain");
    }

    return Results.BadRequest("Verification failed");
});

app.MapPost("/api/webhooks/whatsapp", async (HttpContext ctx, JobRepository jobRepo, SqliteConnectionFactory dbFactory, UnsubscribedContactRepository unsubRepo, HotelAutomation.Connectors.WhatsApp.WhatsAppApiClient whatsappClient, Microsoft.Extensions.Logging.ILogger<Program> logger) => 
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    logger.LogInformation("Webhook received. Body length: {Length}", body.Length);
    
    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var root = doc.RootElement;
        
        // Backward compatibility: if an older WebhookRelayPoller forwarded the new { id, payload } wrapper, unwrap it.
        if (root.TryGetProperty("payload", out var wrappedPayload) && root.TryGetProperty("id", out _))
        {
            root = wrappedPayload;
        }

        if (!root.TryGetProperty("entry", out var entryArray))
        {
            logger.LogWarning("Webhook payload missing 'entry' property.");
            return Results.BadRequest("Invalid payload");
        }

        foreach (var entry in entryArray.EnumerateArray())
        {
            var changesArray = entry.GetProperty("changes");
            foreach (var change in changesArray.EnumerateArray())
            {
                var value = change.GetProperty("value");
                
                // Handle inbound messages (Replies)
                if (value.TryGetProperty("messages", out var messages))
                {
                    logger.LogInformation("Found {Count} messages in webhook payload", messages.GetArrayLength());
                    foreach (var msg in messages.EnumerateArray())
                    {
                        var from = msg.GetProperty("from").GetString();

                        var msgType = "text";
                        if (msg.TryGetProperty("type", out var typeProp))
                        {
                            msgType = typeProp.GetString() ?? "text";
                        }

                        var text = "";
                        string? mediaUrl = null;

                        if (msgType == "text" && msg.TryGetProperty("text", out var textProp))
                        {
                            text = textProp.GetProperty("body").GetString() ?? "";
                        }
                        else if ((msgType == "image" || msgType == "audio" || msgType == "video" || msgType == "document") && msg.TryGetProperty(msgType, out var mediaProp))
                        {
                            if (mediaProp.TryGetProperty("caption", out var captionProp)) text = captionProp.GetString() ?? "";
                            else if (mediaProp.TryGetProperty("filename", out var fnProp)) text = fnProp.GetString() ?? "";

                            if (mediaProp.TryGetProperty("id", out var mediaIdProp))
                            {
                                var mediaId = mediaIdProp.GetString();
                                if (!string.IsNullOrEmpty(mediaId))
                                {
                                    var (success, bytes, mime, err) = await whatsappClient.DownloadMediaAsync(mediaId);
                                    if (success && bytes != null)
                                    {
                                        var ext = msgType switch {
                                            "image" => ".jpg",
                                            "audio" => ".ogg",
                                            "video" => ".mp4",
                                            "document" => ".pdf",
                                            _ => ".bin"
                                        };
                                        if (mime?.Contains("png") == true) ext = ".png";
                                        
                                        var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "inbox");
                                        Directory.CreateDirectory(dir);
                                        var filename = $"{Guid.NewGuid()}{ext}";
                                        await File.WriteAllBytesAsync(Path.Combine(dir, filename), bytes);
                                        mediaUrl = $"/uploads/inbox/{filename}";
                                    }
                                    else
                                    {
                                        logger.LogWarning("Failed to download media {MediaId}: {Error}", mediaId, err);
                                    }
                                }
                            }
                        }
                        else if (msgType != "text")
                        {
                            logger.LogWarning("Skipping unsupported message type {Type} from {From}", msgType, from);
                            continue;
                        }
                        
                        var senderName = string.Empty;
                        if (value.TryGetProperty("contacts", out var contacts))
                        {
                            foreach (var contact in contacts.EnumerateArray())
                            {
                                if (contact.TryGetProperty("wa_id", out var waId) && waId.GetString() == from)
                                {
                                    if (contact.TryGetProperty("profile", out var profile) && profile.TryGetProperty("name", out var nameProp))
                                    {
                                        senderName = nameProp.GetString() ?? string.Empty;
                                    }
                                }
                            }
                        }
                        

                        await jobRepo.CancelJobsForPhoneNumberAsync(from ?? "");
                        if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(text) && text.Trim().Equals("STOP", StringComparison.OrdinalIgnoreCase))
                        {
                            await unsubRepo.AddAsync(from, "STOP_Keyword");
                        }
                    }
                }
                
                // Handle user_preferences (Marketing opt-out toggles)
                if (value.TryGetProperty("user_preferences", out var userPreferences))
                {
                    foreach (var pref in userPreferences.EnumerateArray())
                    {
                        var from = pref.GetProperty("wa_id").GetString();
                        var prefValue = pref.GetProperty("value").GetString();
                        
                        if (!string.IsNullOrEmpty(from))
                        {
                            if (prefValue == "stop")
                                await unsubRepo.AddAsync(from, "WhatsApp_Native");
                            else if (prefValue == "resume")
                                await unsubRepo.RemoveAsync(from);
                        }
                    }
                }
                
                // Handle statuses (Delivered, Read)
                if (value.TryGetProperty("statuses", out var statuses))
                {
                    foreach (var status in statuses.EnumerateArray())
                    {
                        var id = status.GetProperty("id").GetString();
                        var statusStr = status.GetProperty("status").GetString(); // e.g., 'delivered', 'read'
                        
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(statusStr))
                        {
                            // Update our DB mapping
                            using var conn = await dbFactory.OpenAsync();
                            // Only update if it's progressing forward (e.g. Sent -> Delivered -> Read)
                            // For simplicity, we just overwrite if it's Delivered or Read.
                            var capitalizedStatus = char.ToUpper(statusStr[0]) + statusStr.Substring(1);
                            await conn.ExecuteAsync("UPDATE OutboxMessages SET Status = @Status WHERE ExternalMessageId = @Id", new { Status = capitalizedStatus, Id = id });
                        }
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Webhook parsing failed");
    }

    return Results.Ok();
});

app.MapPost("/api/manual/checkin", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "PMS_CHECKIN",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber ?? (payload.ExtensionData?.TryGetValue("room_number", out var rn) == true ? rn.ToString() : "N/A"),
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate ?? DateTimeOffset.UtcNow.ToString("d MMM yyyy"),
        CheckOutDate = payload.CheckOutDate,
        WifiPassword = payload.WifiPassword,
        BookingReference = payload.BookingReference,
        GuestFeedbackLink = payload.GuestFeedbackLink,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Manual Check-In triggered successfully." });
});

app.MapPost("/api/manual/checkout", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "PMS_CHECKOUT",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber ?? (payload.ExtensionData?.TryGetValue("room_number", out var rn) == true ? rn.ToString() : "N/A"),
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate,
        CheckOutDate = payload.CheckOutDate ?? DateTimeOffset.UtcNow.ToString("d MMM yyyy"),
        WifiPassword = payload.WifiPassword,
        BookingReference = payload.BookingReference,
        GuestFeedbackLink = payload.GuestFeedbackLink,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Manual Check-Out triggered successfully." });
});

app.MapPost("/api/pms/checkin", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "PMS_CHECKIN",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber ?? (payload.ExtensionData?.TryGetValue("room_number", out var rn) == true ? rn.ToString() : "N/A"),
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate ?? DateTimeOffset.UtcNow.ToString("d MMM yyyy"),
        CheckOutDate = payload.CheckOutDate,
        WifiPassword = payload.WifiPassword,
        BookingReference = payload.BookingReference,
        GuestFeedbackLink = payload.GuestFeedbackLink,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "CheckIn event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

app.MapPost("/api/pms/checkout", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "PMS_CHECKOUT",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber ?? (payload.ExtensionData?.TryGetValue("room_number", out var rn) == true ? rn.ToString() : "N/A"),
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate,
        CheckOutDate = payload.CheckOutDate ?? DateTimeOffset.UtcNow.ToString("d MMM yyyy"),
        WifiPassword = payload.WifiPassword,
        BookingReference = payload.BookingReference,
        GuestFeedbackLink = payload.GuestFeedbackLink,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "CheckOut event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

app.MapPost("/api/pms/booking-created", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "BOOKING_CREATED",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber ?? (payload.ExtensionData?.TryGetValue("room_number", out var rn) == true ? rn.ToString() : "N/A"),
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate,
        CheckOutDate = payload.CheckOutDate,
        WifiPassword = payload.WifiPassword,
        BookingReference = payload.BookingReference,
        GuestFeedbackLink = payload.GuestFeedbackLink,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Booking Created event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

// ── Booking Modified ──────────────────────────────────────────────────────────
app.MapPost("/api/pms/booking-modified", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "BOOKING_MODIFIED",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber ?? (payload.ExtensionData?.TryGetValue("room_number", out var rn) == true ? rn.ToString() : "N/A"),
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate,
        CheckOutDate = payload.CheckOutDate,
        WifiPassword = payload.WifiPassword,
        BookingReference = payload.BookingReference,
        GuestFeedbackLink = payload.GuestFeedbackLink,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Booking Modified event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

// ── Booking Cancelled ─────────────────────────────────────────────────────────
app.MapPost("/api/pms/booking-cancelled", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "BOOKING_CANCELLED",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber ?? (payload.ExtensionData?.TryGetValue("room_number", out var rn) == true ? rn.ToString() : "N/A"),
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate,
        CheckOutDate = payload.CheckOutDate,
        WifiPassword = payload.WifiPassword,
        BookingReference = payload.BookingReference,
        GuestFeedbackLink = payload.GuestFeedbackLink,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Booking Cancelled event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

// ── Room Service Request ───────────────────────────────────────────────────────
app.MapPost("/api/pms/room-service-request", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "ROOM_SERVICE_REQUEST",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber ?? (payload.ExtensionData?.TryGetValue("room_number", out var rn) == true ? rn.ToString() : "N/A"),
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate,
        CheckOutDate = payload.CheckOutDate,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Room Service Request event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

// ── Payment Received ──────────────────────────────────────────────────────────
app.MapPost("/api/pms/payment-received", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "PAYMENT_RECEIVED",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber ?? (payload.ExtensionData?.TryGetValue("room_number", out var rn) == true ? rn.ToString() : "N/A"),
        Amount = payload.TotalBill,
        BookingReference = payload.BookingReference,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Payment Received event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

// ── Complaint Raised ──────────────────────────────────────────────────────────
app.MapPost("/api/pms/complaint-raised", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "COMPLAINT_RAISED",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber ?? (payload.ExtensionData?.TryGetValue("room_number", out var rn) == true ? rn.ToString() : "N/A"),
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Complaint Raised event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

// ── Event / API Variable Trigger ──────────────────────────────────────────────
// POST /api/event-trigger/{slug}
// Any external system can call this with a JSON body containing arbitrary key-value pairs.
// The system will find all automation rules of type EVENT_API whose ConditionsJson contains
// a matching EventApiSlug, and fire them with the body fields merged as dynamic parameters.
app.MapPost("/api/event-trigger/{slug}", async (
    string slug,
    HttpContext ctx,
    SqliteConnectionFactory dbFactory,
    ManualPmsConnector pms,
    HotelAutomation.Data.Repositories.ContactRepository contactRepo,
    Microsoft.Extensions.Logging.ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(slug))
        return Results.BadRequest(new { error = "Slug is required." });

    using var reader = new System.IO.StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();

    // Validate incoming JSON
    Dictionary<string, string>? incomingFields = null;
    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        incomingFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            incomingFields[prop.Name] = prop.Value.ToString();
        }
    }
    catch
    {
        return Results.BadRequest(new { error = "Invalid JSON body." });
    }

    // Extract required fields: phone number and guest name are mandatory
    if (!incomingFields.TryGetValue("phone", out var phone) && !incomingFields.TryGetValue("PhoneNumber", out phone))
        return Results.BadRequest(new { error = "A 'phone' or 'PhoneNumber' field is required in the request body." });

    phone = HotelAutomation.Core.Helpers.PhoneHelper.NormalizePhone(phone);

    if (!incomingFields.TryGetValue("name", out var guestName) && !incomingFields.TryGetValue("GuestName", out guestName))
        guestName = "Guest";

    // Upsert contact with payload data (only saving approved/existing columns)
    try
    {
        var existingColumns = new HashSet<string>(await contactRepo.GetDynamicColumnsAsync(), StringComparer.OrdinalIgnoreCase);
        
        var firstName = incomingFields.TryGetValue("first_name", out var fn) ? fn : 
                        incomingFields.TryGetValue("firstname", out var fn2) ? fn2 : 
                        incomingFields.TryGetValue("name", out var n) ? n.Split(' ')[0] : guestName.Split(' ')[0];
        
        var lastName = incomingFields.TryGetValue("last_name", out var ln) ? ln : 
                       incomingFields.TryGetValue("lastname", out var ln2) ? ln2 : 
                       incomingFields.TryGetValue("name", out var n2) && n2.Contains(' ') ? n2.Substring(n2.IndexOf(' ') + 1) : 
                       guestName.Contains(' ') ? guestName.Substring(guestName.IndexOf(' ') + 1) : "";

        var customFields = new Dictionary<string, object>();
        foreach (var kv in incomingFields)
        {
            if (kv.Key.Equals("phone", StringComparison.OrdinalIgnoreCase) || 
                kv.Key.Equals("PhoneNumber", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("name", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("first_name", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("last_name", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("firstname", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("lastname", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("GuestName", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (existingColumns.Contains(kv.Key))
            {
                customFields[kv.Key] = kv.Value;
            }
        }

        var existingContact = await contactRepo.GetByPhoneNumberAsync(phone);
        if (existingContact != null)
        {
            var existingCustomFields = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(existingContact.CustomFieldsJson ?? "{}") ?? new Dictionary<string, object>();
            foreach (var kv in customFields)
            {
                existingCustomFields[kv.Key] = kv.Value;
            }
            
            var updatedContact = new HotelAutomation.Core.Models.Contact
            {
                Id = existingContact.Id,
                PhoneNumber = existingContact.PhoneNumber,
                FirstName = !string.IsNullOrWhiteSpace(firstName) ? firstName : existingContact.FirstName,
                LastName = !string.IsNullOrWhiteSpace(lastName) ? lastName : existingContact.LastName,
                CategoryId = existingContact.CategoryId,
                CustomFieldsJson = System.Text.Json.JsonSerializer.Serialize(existingCustomFields),
                CreatedAt = existingContact.CreatedAt,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdateCount = existingContact.UpdateCount,
                CheckInCount = existingContact.CheckInCount,
                AverageStayDays = existingContact.AverageStayDays,
                IsVip = existingContact.IsVip
            };
            await contactRepo.UpdateAsync(existingContact.Id, updatedContact);
        }
        else
        {
            var newContact = new HotelAutomation.Core.Models.Contact
            {
                PhoneNumber = phone,
                FirstName = firstName,
                LastName = lastName,
                CustomFieldsJson = System.Text.Json.JsonSerializer.Serialize(customFields),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await contactRepo.InsertAsync(newContact);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to upsert contact for event-trigger payload.");
    }

    // Find all EVENT_API rules whose ConditionsJson contains this slug
    using var conn = await dbFactory.OpenAsync();
    var eventApiRules = await conn.QueryAsync<HotelAutomation.Core.Models.AutomationRule>(
        "SELECT * FROM AutomationRules WHERE TriggerEventType = 'EVENT_API' AND IsEnabled = 1");

    int firedCount = 0;
    foreach (var rule in eventApiRules)
    {
        if (string.IsNullOrWhiteSpace(rule.ConditionsJson)) continue;
        try
        {
            var cond = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(rule.ConditionsJson);
            if (!cond.TryGetProperty("EventApiSlug", out var slugProp)) continue;
            if (!string.Equals(slugProp.GetString(), slug, StringComparison.OrdinalIgnoreCase)) continue;

            // Apply optional variable mappings defined in the rule
            var renamedFields = new Dictionary<string, string>(incomingFields, StringComparer.OrdinalIgnoreCase);
            if (cond.TryGetProperty("VarMappings", out var varMappings) && varMappings.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var mapping in varMappings.EnumerateArray())
                {
                    if (mapping.TryGetProperty("jsonKey", out var jk) && mapping.TryGetProperty("templateParam", out var tp))
                    {
                        var jsonKey = jk.GetString() ?? "";
                        var templateParam = tp.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(jsonKey) && !string.IsNullOrWhiteSpace(templateParam)
                            && renamedFields.TryGetValue(jsonKey, out var val))
                        {
                            renamedFields[templateParam] = val;
                        }
                    }
                }
            }

            // Build raw payload from the (possibly remapped) fields
            var rawPayload = System.Text.Json.JsonSerializer.Serialize(renamedFields);

            // Fire the event through the existing PMS connector so HotelWorker picks it up
            pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
            {
                EventType = "EVENT_API",
                GuestName = guestName,
                PhoneNumber = phone,
                RoomNumber = renamedFields.TryGetValue("room", out var room) ? room :
                             renamedFields.TryGetValue("RoomNumber", out var roomNum) ? roomNum : "",
                HotelName = renamedFields.TryGetValue("hotelName", out var hn) ? hn :
                            renamedFields.TryGetValue("HotelName", out var hn2) ? hn2 : "Sankalp Retreat",
                ExtraNote = renamedFields.TryGetValue("note", out var note) ? note :
                            renamedFields.TryGetValue("ExtraNote", out var en) ? en : "",
                Timestamp = DateTimeOffset.UtcNow,
                RawPayload = rawPayload
            });
            firedCount++;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing EVENT_API rule {RuleId} for slug '{Slug}'", rule.Id, slug);
        }
    }

    if (firedCount == 0)
        return Results.NotFound(new { error = $"No active automation rule found for event slug '{slug}'." });

    return Results.Ok(new { status = "success", firedRules = firedCount, slug, message = $"Event '{slug}' triggered {firedCount} automation rule(s)." });
});

app.Lifetime.ApplicationStarted.Register(() => 
{
    try
    {
        var dbFactory = app.Services.GetRequiredService<SqliteConnectionFactory>();

        // Look for Schema.sql in order:
        // 1. Next to the running exe (installed via setup)
        // 2. Dev source layout: ../HotelAutomation.Data/Schema.sql
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schema.sql");
        if (!File.Exists(schemaPath))
            schemaPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "HotelAutomation.Data", "Schema.sql");

        if (!File.Exists(schemaPath))
        {
            app.Logger.LogWarning("Schema.sql not found at any expected path — skipping DB init (tables should already exist).");
        }
        else
        {
            var initScript = File.ReadAllText(schemaPath);
            using var conn = dbFactory.OpenAsync().GetAwaiter().GetResult();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = initScript;
            cmd.ExecuteNonQuery();
        }

        // ── Startup DB Backup (Power-cut failsafe) ────────────────────────────
        // Copy the live database to a timestamped .bak file on each clean startup.
        // If power is lost, the .bak from the previous startup can be used to recover.
        try
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HotelSankalpRetreat", "hotel_automation.db");
            if (File.Exists(dbPath))
            {
                var bakPath = Path.ChangeExtension(dbPath, ".db.bak");
                File.Copy(dbPath, bakPath, overwrite: true);
                app.Logger.LogInformation("Startup DB backup written to {BakPath}", bakPath);
            }
        }
        catch (Exception backupEx)
        {
            app.Logger.LogWarning(backupEx, "Startup DB backup failed (non-fatal).");
        }


        // ── Open browser automatically (non-fatal) ───────────────────────────
        if (Environment.UserInteractive)
        {
            try
            {
                var url = app.Urls.FirstOrDefault() ?? "http://localhost:8001";
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(processStartInfo);
            }
            catch
            {
                try
                {
                    var url = app.Urls.FirstOrDefault() ?? "http://localhost:8001";
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", url)
                    {
                        UseShellExecute = false
                    });
                }
                catch (Exception browserEx)
                {
                    app.Logger.LogWarning(browserEx, "Could not open browser automatically. Please open http://localhost:8001 manually.");
                }
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to initialize DB on startup");
    }
});

app.MapPost("/api/test-webhook", async (string wamid, string status, SqliteConnectionFactory dbFactory) => 
{
    using var conn = await dbFactory.OpenAsync();
    await conn.ExecuteAsync("UPDATE OutboxMessages SET Status = @Status WHERE ExternalMessageId = @Id OR Id = @Id", new { Status = status, Id = wamid });
    return Results.Ok(new { message = $"Status updated to {status}" });
});

app.MapGet("/api/categories", async (CategoryRepository repo) => await repo.GetAllAsync());
app.MapPost("/api/categories", async (HotelAutomation.Core.Models.ContactCategory cat, CategoryRepository repo) => 
{
    var id = await repo.AddAsync(cat);
    return Results.Ok(new { id });
});

app.MapDelete("/api/categories/{id}", async (int id, CategoryRepository repo) => 
{
    var success = await repo.DeleteAsync(id);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapGet("/api/contacts/search", async (string query, ContactRepository repo) => 
{
    if (string.IsNullOrWhiteSpace(query)) return Results.Ok(new List<HotelAutomation.Core.Models.Contact>());
    var results = await repo.SearchContactsAsync(query);
    return Results.Ok(results);
});

app.MapGet("/api/contacts", async (ContactRepository repo, int? categoryId, int page = 1, int limit = 50) => 
{
    var (list, total) = await repo.GetAllAsync(categoryId, page, limit);
    return Results.Ok(new { Contacts = list, TotalCount = total });
});

app.MapGet("/api/contacts/columns/all", async (ContactRepository repo) => 
{
    var columns = await repo.GetDynamicColumnsAsync();
    return Results.Ok(columns);
});

app.MapGet("/api/contacts/columns/dates", async (ContactRepository repo) => 
{
    var columns = await repo.GetDateColumnsAsync();
    return Results.Ok(columns);
});

app.MapDelete("/api/contacts", async (ContactRepository repo) => 
{
    var deletedCount = await repo.DeleteAllAsync();
    return Results.Ok(new { message = $"Deleted {deletedCount} contacts." });
});

app.MapDelete("/api/contacts/{id}", async (int id, ContactRepository repo) => 
{
    var success = await repo.DeleteByIdAsync(id);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapPost("/api/contacts", async (HotelAutomation.Core.Models.Contact contact, ContactRepository repo) => 
{
    var newId = await repo.InsertAsync(contact);
    return Results.Ok(new { id = newId });
});

app.MapPut("/api/contacts/{id}", async (int id, HotelAutomation.Core.Models.Contact contact, ContactRepository repo) => 
{
    var success = await repo.UpdateAsync(id, contact);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapDelete("/api/contacts/columns/{columnName}", async (string columnName, ContactRepository repo) => 
{
    var updatedRows = await repo.DeleteCustomFieldColumnAsync(columnName);
    return Results.Ok(new { message = $"Column '{columnName}' deleted from {updatedRows} contacts." });
});

app.MapGet("/api/pdf-file", (string path) =>
{
    if (string.IsNullOrWhiteSpace(path)) return Results.NotFound();
    var cleanPath = path.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
    
    var fullPath = Path.Combine(Directory.GetCurrentDirectory(), cleanPath);
    if (File.Exists(fullPath)) return Results.File(fullPath, "application/pdf");

    var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    var appDataPath = Path.Combine(appDataDir, "HotelSankalpRetreat", cleanPath);
    if (File.Exists(appDataPath)) return Results.File(appDataPath, "application/pdf");

    return Results.NotFound();
});

app.MapGet("/api/contacts/{id:int}/pdf-file", async (int id, string columnKey, ContactRepository repo) =>
{
    var (contacts, _) = await repo.GetAllAsync(limit: 5000);
    var contact = contacts.FirstOrDefault(c => c.Id == id);
    if (contact == null) return Results.NotFound();

    var fields = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(contact.CustomFieldsJson ?? "{}") ?? new();

    if (fields.TryGetValue(columnKey, out var relObj) && relObj != null)
    {
        var relPath = relObj.ToString()?.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        if (!string.IsNullOrWhiteSpace(relPath))
        {
            var p1 = Path.Combine(Directory.GetCurrentDirectory(), relPath);
            if (File.Exists(p1)) return Results.File(p1, "application/pdf");

            var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var p2 = Path.Combine(appDataDir, "HotelSankalpRetreat", relPath);
            if (File.Exists(p2)) return Results.File(p2, "application/pdf");
        }
    }

    // Dynamic fallback search in Processed directory
    var rawDate = fields.TryGetValue("Invoice Date", out var invObj) ? invObj?.ToString()
                : (fields.TryGetValue("Arrival Date", out var arrObj) ? arrObj?.ToString() : null);

    var dateFolder = !string.IsNullOrWhiteSpace(rawDate) 
        ? rawDate.Trim().Replace('/', '-').Replace('.', '-').Replace('\\', '-') 
        : DateTime.Now.ToString("dd-MM-yyyy");

    var regNo = fields.TryGetValue("Registration Number", out var regObj) ? regObj?.ToString() : null;
    var fullNameClean = $"{contact.FirstName}{contact.LastName}".Replace(" ", "");

    var searchDirs = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), "PDFs", "Processed", dateFolder),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HotelSankalpRetreat", "PDFs", "Processed", dateFolder)
    };

    bool isSigFolder = columnKey.Contains("Signature & Stamp", StringComparison.OrdinalIgnoreCase) && 
                      !columnKey.Contains("No Signature", StringComparison.OrdinalIgnoreCase);
    bool isSupplier = columnKey.StartsWith("Supplier", StringComparison.OrdinalIgnoreCase);

    foreach (var baseDir in searchDirs)
    {
        if (!Directory.Exists(baseDir)) continue;

        var subfolderNames = isSigFolder 
            ? new[] { "Signature and stamp", "2. Signature and stamp" }
            : new[] { "No signature and stamp", "1. No signature and stamp" };

        foreach (var sub in subfolderNames)
        {
            var subDir = Path.Combine(baseDir, sub);
            if (!Directory.Exists(subDir)) continue;

            var pdfFiles = Directory.GetFiles(subDir, "*.pdf");
            var match = pdfFiles.FirstOrDefault(f =>
            {
                var fname = Path.GetFileName(f);
                bool nameOrRegMatch = fname.Contains(fullNameClean, StringComparison.OrdinalIgnoreCase) ||
                                     (!string.IsNullOrWhiteSpace(regNo) && fname.Contains(regNo, StringComparison.OrdinalIgnoreCase));
                if (!nameOrRegMatch) return false;

                if (isSupplier) return fname.Contains("Supplier", StringComparison.OrdinalIgnoreCase);
                return fname.Contains("Recipient", StringComparison.OrdinalIgnoreCase) || !fname.Contains("Supplier", StringComparison.OrdinalIgnoreCase);
            });

            if (match != null && File.Exists(match))
            {
                return Results.File(match, "application/pdf");
            }
        }
    }

    return Results.NotFound();
});

app.MapPost("/api/contacts/import/chunk", async (List<HotelAutomation.Core.Models.Contact> contacts, ContactRepository repo) => 
{
    await repo.UpsertBulkAsync(contacts);
    return Results.Ok(new { importedCount = contacts.Count });
});

app.MapPost("/api/contacts/import", async (HttpContext ctx, [Microsoft.AspNetCore.Mvc.FromForm] int? categoryId, CsvImportService importService) => 
{
    if (!ctx.Request.HasFormContentType || ctx.Request.Form.Files.Count == 0) return Results.BadRequest("No file uploaded");
    var file = ctx.Request.Form.Files[0];
    using var stream = file.OpenReadStream();
    var count = await importService.ProcessCsvAsync(stream, categoryId);
    return Results.Ok(new { importedCount = count });
}).DisableAntiforgery();

app.MapPost("/api/integrations/pms-webhook", async (
    HttpContext ctx, 
    string pmsName, 
    PmsMappingRepository mappingRepo, 
    ContactRepository contactRepo, 
    EventLogRepository eventLog) => 
{
    var mapping = await mappingRepo.GetByNameAsync(pmsName);
    if (mapping == null) return Results.NotFound($"Mapping for PMS '{pmsName}' not found.");

    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    
    try
    {
        var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(body);
        if (jsonNode == null) return Results.BadRequest("Invalid JSON payload.");

        // Minimal dynamic extraction using JSON Path is complex without a full library like JsonPath.Net.
        // For MVP, we assume the mapping defines simple dot-separated paths for key fields:
        // {"PhoneNumber": "guest.phone", "FirstName": "guest.first_name", "LastName": "guest.last_name"}
        var fieldMappings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(mapping.JsonPathMapping);
        if (fieldMappings == null) return Results.BadRequest("Invalid JsonPathMapping config.");

        string? GetValueByPath(System.Text.Json.Nodes.JsonNode node, string path)
        {
            var parts = path.Split('.');
            var current = node;
            foreach (var part in parts)
            {
                if (current is System.Text.Json.Nodes.JsonObject obj && obj.ContainsKey(part))
                {
                    current = obj[part];
                }
                else return null;
            }
            return current?.ToString();
        }

        var phone = GetValueByPath(jsonNode, fieldMappings.GetValueOrDefault("PhoneNumber", "phone"));
        var firstName = GetValueByPath(jsonNode, fieldMappings.GetValueOrDefault("FirstName", "first_name"));
        var lastName = GetValueByPath(jsonNode, fieldMappings.GetValueOrDefault("LastName", "last_name"));

        if (string.IsNullOrWhiteSpace(phone))
        {
            return Results.BadRequest("Could not extract a valid phone number from the payload.");
        }

        var contact = new HotelAutomation.Core.Models.Contact
        {
            PhoneNumber = phone,
            FirstName = firstName,
            LastName = lastName,
            CategoryId = 1, // Default to a general category or could be mapped
            CustomFieldsJson = body, // Store the entire raw payload for advanced automation
            CreatedAt = DateTimeOffset.UtcNow
        };

        await contactRepo.UpsertAsync(contact);
        
        // Log the event
        await eventLog.LogAsync(new HotelAutomation.Core.Models.PmsEvent 
        { 
            EventType = "PmsWebhook", 
            GuestName = $"{firstName} {lastName}".Trim(), 
            PhoneNumber = phone, 
            RoomNumber = GetValueByPath(jsonNode, fieldMappings.GetValueOrDefault("RoomNumber", "room_number")) ?? "",
            RawPayload = body 
        });

        return Results.Ok(new { message = "Contact updated from PMS payload.", phone });
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Error parsing payload: {ex.Message}");
    }
});

app.MapGet("/api/integrations/pms-mappings", async (PmsMappingRepository repo) => 
{
    return Results.Ok(await repo.GetAllAsync());
});

app.MapPost("/api/integrations/pms-mappings", async (HotelAutomation.Core.Models.PmsMapping mapping, PmsMappingRepository repo) => 
{
    var newMapping = mapping with { CreatedAt = DateTimeOffset.UtcNow };
    await repo.AddAsync(newMapping);
    return Results.Ok();
});

app.MapDelete("/api/integrations/pms-mappings/{id}", async (int id, PmsMappingRepository repo) => 
{
    await repo.DeleteAsync(id);
});


app.MapGet("/api/settings/whatsapp", async (AppConfigRepository repo, Microsoft.Extensions.Options.IOptions<WhatsAppOptions> options) =>
{
    var accessToken = await repo.GetValueAsync("WhatsApp_AccessToken") ?? options.Value.AccessToken;
    var phoneNumberId = await repo.GetValueAsync("WhatsApp_PhoneNumberId") ?? options.Value.PhoneNumberId;
    var apiVersion = await repo.GetValueAsync("WhatsApp_ApiVersion") ?? options.Value.ApiVersion;
    var verifyToken = await repo.GetValueAsync("WhatsApp_VerifyToken") ?? options.Value.VerifyToken;
    var wabaId = await repo.GetValueAsync("WhatsApp_WhatsAppBusinessAccountId") ?? options.Value.WhatsAppBusinessAccountId;

    return Results.Ok(new
    {
        AccessToken = accessToken,
        PhoneNumberId = phoneNumberId,
        ApiVersion = apiVersion,
        VerifyToken = verifyToken,
        WhatsAppBusinessAccountId = wabaId
    });
});

app.MapPost("/api/settings/whatsapp", async (AppConfigDto dto, AppConfigRepository repo, Microsoft.Extensions.Options.IOptions<WhatsAppOptions> options) =>
{
    await repo.SetValueAsync("WhatsApp_AccessToken", dto.AccessToken ?? "");
    await repo.SetValueAsync("WhatsApp_PhoneNumberId", dto.PhoneNumberId ?? "");
    await repo.SetValueAsync("WhatsApp_ApiVersion", dto.ApiVersion ?? "");
    await repo.SetValueAsync("WhatsApp_VerifyToken", dto.VerifyToken ?? "");
    await repo.SetValueAsync("WhatsApp_WhatsAppBusinessAccountId", dto.WhatsAppBusinessAccountId ?? "");

    // Dynamically update the in-memory singleton options instance
    var opt = options.Value;
    opt.AccessToken = dto.AccessToken ?? "";
    opt.PhoneNumberId = dto.PhoneNumberId ?? "";
    opt.ApiVersion = dto.ApiVersion ?? "";
    opt.VerifyToken = dto.VerifyToken ?? "";
    opt.WhatsAppBusinessAccountId = dto.WhatsAppBusinessAccountId ?? "";

    // Synchronize to appsettings.json files so on-disk configs never stay on old credentials
    try
    {
        var searchRoots = new[]
        {
            AppContext.BaseDirectory,
            Path.Combine(AppContext.BaseDirectory, "..", "..", ".."),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Setup"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "publish_output")
        };

        foreach (var root in searchRoots)
        {
            var configPath = Path.GetFullPath(Path.Combine(root, "appsettings.json"));
            if (File.Exists(configPath))
            {
                var text = await File.ReadAllTextAsync(configPath);
                var jsonObj = System.Text.Json.Nodes.JsonNode.Parse(text);
                if (jsonObj != null && jsonObj["WhatsApp"] is System.Text.Json.Nodes.JsonObject waNode)
                {
                    waNode["WhatsAppBusinessAccountId"] = dto.WhatsAppBusinessAccountId ?? "";
                    waNode["PhoneNumberId"] = dto.PhoneNumberId ?? "";
                    waNode["AccessToken"] = dto.AccessToken ?? "";
                    waNode["ApiVersion"] = dto.ApiVersion ?? "v25.0";
                    var updatedJson = jsonObj.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(configPath, updatedJson);
                }
            }
        }
    }
    catch { /* Ignore non-fatal file write failures */ }

    return Results.Ok(new { message = "Settings saved and synchronized successfully across all configs" });
});

app.MapPost("/api/webhooks/pms-events", async (
    HttpContext ctx,
    [Microsoft.AspNetCore.Mvc.FromBody] System.Text.Json.JsonElement body,
    IPmsEventSource pmsSource,
    Microsoft.Extensions.Logging.ILogger<Program> logger) =>
{
    // 1. Check Authorization Token
    if (!ctx.Request.Headers.TryGetValue("Authorization", out var authHeader) || 
        !string.Equals(authHeader.ToString(), "Bearer sankalp_retreat_pms_secret_2026", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Unauthorized();
    }

    try
    {
        // 2. Parse Payload
        var rawEventType = body.TryGetProperty("eventType", out var etProp) ? etProp.GetString() : null;
        var guestName = body.TryGetProperty("guestName", out var gnProp) ? gnProp.GetString() : null;
        var phoneNumber = body.TryGetProperty("phoneNumber", out var pnProp) ? pnProp.GetString() : null;
        var roomNumber = body.TryGetProperty("roomNumber", out var rnProp) ? rnProp.GetString() : null;
        var amount = body.TryGetProperty("amount", out var amtProp) && amtProp.TryGetDecimal(out var d) ? d : 0m;
        
        if (string.IsNullOrWhiteSpace(rawEventType) || string.IsNullOrWhiteSpace(guestName) || string.IsNullOrWhiteSpace(phoneNumber))
        {
            return Results.BadRequest(new { status = "error", message = "Missing required fields (eventType, guestName, phoneNumber)" });
        }

        // Map API Event Types ("CheckIn"/"CheckOut") to DB conventions ("PMS_CHECKIN"/"PMS_CHECKOUT")
        var eventType = rawEventType.ToLower() switch
        {
            "checkin" => "PMS_CHECKIN",
            "checkout" => "PMS_CHECKOUT",
            _ => rawEventType
        };

        var pmsEvent = new HotelAutomation.Core.Models.PmsEvent
        {
            EventType = eventType ?? "",
            GuestName = guestName ?? "",
            PhoneNumber = phoneNumber ?? "",
            RoomNumber = roomNumber ?? "",
            Amount = amount,
            RawPayload = body.GetRawText() ?? ""
        };

        // 3. Inject event if using ManualPmsConnector (standard for webhook handling in this architecture)
        if (pmsSource is ManualPmsConnector manualConnector)
        {
            manualConnector.RaiseTestEvent(pmsEvent);
            return Results.Json(new { status = "success", message = $"Event {eventType} for room {roomNumber} processed successfully." }, statusCode: 202);
        }
        else
        {
            logger.LogWarning("Webhook received, but current IPmsEventSource is not ManualPmsConnector.");
            return Results.Json(new { status = "warning", message = "Event received but not processed (active connector does not support direct injection)." }, statusCode: 202);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing PMS Webhook");
        return Results.StatusCode(500);
    }
});

// ── PDF Bridge API Endpoints ─────────────────────────────────────────────────

// GET /api/pdf-bridge/status
// Returns statistics about the PDF print bridge (last file, count, errors).
app.MapGet("/api/pdf-bridge/status", (IPmsEventSource pmsSource, IConfiguration config) =>
{
    var pdfOpts = new HotelAutomation.Connectors.Pms.PdfBridgeOptions();
    config.GetSection(HotelAutomation.Connectors.Pms.PdfBridgeOptions.SectionName).Bind(pdfOpts);

    if (pmsSource is HotelAutomation.Connectors.Pms.PdfPmsConnector pdfConnector)
    {
        return Results.Ok(new
        {
            active           = true,
            printerName      = pdfOpts.VirtualPrinterName,
            printerOutputFile = pdfOpts.PrinterOutputFile,
            watchFolder      = pdfOpts.WatchFolder,
            filesProcessed   = pdfConnector.FilesProcessed,
            filesFailed      = pdfConnector.FilesFailedExtraction,
            lastFile         = pdfConnector.LastProcessedFile,
            lastProcessedAt  = pdfConnector.LastProcessedAt,
            lastError        = pdfConnector.LastError
        });
    }
    return Results.Ok(new { active = false, message = "PDF Bridge connector is not active. Set PmsConnectorType=Pdf in appsettings.json." });
});

// POST /api/pdf-bridge/scan
// Manually trigger a scan of all existing PDFs in the watch folder.
// Useful for processing files that were placed in the folder while the app was offline.
app.MapPost("/api/pdf-bridge/scan", (IPmsEventSource pmsSource, ILogger<Program> logger) =>
{
    if (pmsSource is HotelAutomation.Connectors.Pms.PdfPmsConnector pdfConnector)
    {
        logger.LogInformation("PDF Bridge: manual scan triggered via API.");
        pdfConnector.ScanNow();
        return Results.Ok(new { status = "success", message = "Scan triggered. Check logs for results." });
    }
    return Results.BadRequest(new { status = "error", message = "PDF Bridge connector is not active." });
});

// ── Database Backup Endpoints ────────────────────────────────────────────────
app.MapGet("/api/database/backup-status", (HotelAutomation.Worker.Services.DatabaseBackupService backupService) =>
{
    var istOffset = TimeSpan.FromHours(5.5);
    var nowIst = DateTimeOffset.UtcNow.ToOffset(istOffset);
    var backups = backupService.GetAllBackups();
    var lastBackup = backups.FirstOrDefault();
    var hasTodayBackup = backupService.HasBackupForDate(nowIst);

    return Results.Ok(new
    {
        backupFolder = backupService.BackupFolderPath,
        scheduleWindow = "Every day between 02:00 PM to 04:00 PM IST",
        currentTimeIst = nowIst.ToString("yyyy-MM-dd HH:mm:ss"),
        hasBackupToday = hasTodayBackup,
        totalBackups = backups.Count,
        lastBackupTimeIst = lastBackup?.CreatedAtIst.ToString("yyyy-MM-dd HH:mm:ss"),
        lastBackupFileName = lastBackup?.FileName
    });
});

app.MapGet("/api/database/backups", (HotelAutomation.Worker.Services.DatabaseBackupService backupService) =>
{
    var backups = backupService.GetAllBackups();
    return Results.Ok(backups);
});

app.MapPost("/api/database/backup-now", async (HotelAutomation.Worker.Services.DatabaseBackupService backupService, HttpContext context) =>
{
    var username = context.User.Identity?.Name ?? "Manual_Admin";
    var result = await backupService.CreateBackupAsync($"Manual_Trigger_by_{username}");
    if (result.Success)
    {
        return Results.Ok(new
        {
            success = true,
            message = result.Message,
            fileName = result.FileName,
            sizeBytes = result.SizeBytes
        });
    }
    return Results.BadRequest(new { success = false, message = result.Message });
});

app.MapGet("/api/database/backups/download/{fileName}", (string fileName, HotelAutomation.Worker.Services.DatabaseBackupService backupService) =>
{
    var safeName = Path.GetFileName(fileName);
    var filePath = Path.Combine(backupService.BackupFolderPath, safeName);
    if (!File.Exists(filePath))
    {
        return Results.NotFound(new { error = "Backup file not found." });
    }
    var fileBytes = File.ReadAllBytes(filePath);
    return Results.File(fileBytes, "application/x-sqlite3", safeName);
});

app.MapDelete("/api/database/backups/{fileName}", (string fileName, HotelAutomation.Worker.Services.DatabaseBackupService backupService) =>
{
    var deleted = backupService.DeleteBackup(fileName);
    if (deleted)
    {
        return Results.Ok(new { success = true, message = $"Backup {fileName} deleted successfully." });
    }
    return Results.NotFound(new { error = "Backup file not found or could not be deleted." });
});

app.MapGet("/ping", () => Results.Ok("live"));

try
{
    app.Run();
}
catch (System.IO.IOException ioEx) when (ioEx.InnerException is Microsoft.AspNetCore.Connections.AddressInUseException || ioEx.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
{
    CrashLogger.LogCrash("Program.PortConflict", ioEx, "Port 8000 was in use despite pre-flight checks. Retrying on port 8001...");
    Console.WriteLine("[Warning] Port 8000 was locked. Falling back to port 8001 so background automations continue...");

    try
    {
        // Try starting on 8001 or dynamic fallback so background automations NEVER die
        app.Run("http://localhost:8001");
    }
    catch (Exception fallbackEx)
    {
        CrashLogger.LogCrash("Program.FallbackRun", fallbackEx);
        Console.WriteLine($"[Fatal Error] Could not bind fallback web port: {fallbackEx.Message}");
    }
}
catch (Exception ex)
{
    CrashLogger.LogCrash("Program.AppRun", ex);
    Console.WriteLine($"[Fatal Error] Host terminated unexpectedly: {ex.Message}");
    if (Environment.UserInteractive)
    {
        Console.WriteLine("\nPress any key to exit...");
        try { Console.ReadKey(); } catch { }
    }
    throw;
}

public class WebhookPayload { public string From { get; set; } = ""; public string Body { get; set; } = ""; }

/// <summary>Request body for POST /api/broadcast</summary>
public record BroadcastRequest
{
    /// <summary>The ID of the MessageTemplate to send.</summary>
    public int TemplateId { get; init; }

    /// <summary>If set, only send to contacts in this category. Null = all contacts.</summary>
    public int? CategoryId { get; init; }

    /// <summary>Optional JSON with extra template parameters to include in every message.</summary>
    public string? CustomParametersJson { get; init; }
}

public record LoginRequest(string Username, string Password);
public record CreateUserRequest(string Username, string Password, string Role);
public record ToggleStatusRequest(bool IsActive);
public record ChangePasswordRequest(string OldPassword, string NewPassword);
public record UpdateRoleRequest(string Role);
public record ResetPasswordRequest(string Password);
public record SavePermissionRequest(string Role, string Module, bool IsEnabled);
public record AppConfigDto(string? AccessToken, string? PhoneNumberId, string? ApiVersion, string? VerifyToken, string? WhatsAppBusinessAccountId);
