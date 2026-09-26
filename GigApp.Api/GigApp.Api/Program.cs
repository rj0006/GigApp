using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using GigApp.Api.Configuration;
using GigApp.Api.Data;
using GigApp.Api.Hubs;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Addresses;
using GigApp.Api.Services.Banking;
using GigApp.Api.Services.Bidding;
using GigApp.Api.Services.Booking;
using GigApp.Api.Services.Earnings;
using GigApp.Api.Services.Errors;
using GigApp.Api.Services.Files;
using GigApp.Api.Services.Geo;
using GigApp.Api.Services.Kyc;
using GigApp.Api.Services.Masters;
using GigApp.Api.Services.Menus;
using GigApp.Api.Services.Notifications;
using GigApp.Api.Services.Orders;
using GigApp.Api.Services.Otp;
using GigApp.Api.Services.Pricing;
using GigApp.Api.Services.Ratings;
using GigApp.Api.Services.Realtime;
using GigApp.Api.Services.Storefront;
using GigApp.Api.Services.Support;
using GigApp.Api.Services.Profile;
using GigApp.Api.Services.Tracking;
using GigApp.Api.Services.UserAdmin;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.UseNetTopologySuite()));

var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
builder.Services.Configure<JwtSettings>(jwtSection);

var jwtSettings = jwtSection.Get<JwtSettings>() ?? new JwtSettings();

// A short or missing key silently weakens every token, so refuse to start.
if (string.IsNullOrWhiteSpace(jwtSettings.Key) || Encoding.UTF8.GetByteCount(jwtSettings.Key) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 bytes. Set it via configuration " +
        "(appsettings.Development.json in development, or the Jwt__Key environment variable).");
}

// Emit and read the same short claim names instead of the legacy SOAP-style URIs.
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            NameClaimType = ClaimNames.Name,
            RoleClaimType = ClaimNames.Role,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        // Razor pages carry the token in an HttpOnly cookie rather than a header;
        // mobile clients keep using Authorization. One scheme serves both.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // A SignalR WebSocket upgrade cannot carry a custom Authorization
                // header, so the client (web or a future mobile app, both using
                // the same Microsoft SignalR client) puts the token in the query
                // string instead — this is that client's own well-known
                // convention, read here only for the hub's own path.
                var accessToken = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                else if (string.IsNullOrEmpty(context.Token) &&
                    context.Request.Cookies.TryGetValue(AuthCookie.Name, out var cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            },

            // Tokens last seven days, so deactivating an account would otherwise
            // do nothing until the token expired. Checking here costs one small
            // lookup per authenticated request and makes deactivation immediate.
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.GetUserId();
                if (userId is null)
                {
                    context.Fail("No user id on the token.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

                var account = await db.Users
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.IsActive, u.Role })
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                // Null means the account is gone; false means it was deactivated.
                if (account is null || !account.IsActive)
                {
                    context.Fail("Account is not active.");
                    return;
                }

                // The role is baked into the token at sign-in, so promoting or
                // demoting someone would otherwise do nothing until their token
                // expired — up to seven days of the wrong permissions. Replace
                // the claim with what the database says right now.
                if (context.Principal!.Identity is System.Security.Claims.ClaimsIdentity identity)
                {
                    var stale = identity.FindFirst(ClaimNames.Role);
                    if (stale?.Value != account.Role)
                    {
                        if (stale is not null) identity.RemoveClaim(stale);
                        identity.AddClaim(new System.Security.Claims.Claim(ClaimNames.Role, account.Role));
                    }
                }
            },

            // A bare 401/403 is right for API clients but renders as a blank page
            // in a browser, so send page requests to the matching portal login.
            OnChallenge = context =>
            {
                if (!PortalRedirects.WantsHtml(context.Request)) return Task.CompletedTask;

                context.HandleResponse();
                var returnUrl = context.Request.Path + context.Request.QueryString;
                context.Response.Redirect(
                    $"{PortalRedirects.LoginPathFor(context.Request.Path)}?returnUrl={Uri.EscapeDataString(returnUrl)}");

                return Task.CompletedTask;
            },

            OnForbidden = context =>
            {
                if (!PortalRedirects.WantsHtml(context.Request)) return Task.CompletedTask;

                context.Response.Redirect(
                    $"{PortalRedirects.LoginPathFor(context.Request.Path)}?denied=1");

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization(options =>
{
    // A super admin is an admin plus more, so AdminOnly accepts both. Only
    // SuperAdminOnly is exclusive — that is what a future team member with the
    // plain admin role will not be able to reach.
    options.AddPolicy(Policies.AdminOnly, p => p.RequireRole(UserRoles.AdminRoles));
    options.AddPolicy(Policies.SuperAdminOnly, p => p.RequireRole(UserRoles.SuperAdmin));
    options.AddPolicy(Policies.PartnerOnly, p => p.RequireRole(UserRoles.Partner));
    options.AddPolicy(Policies.CustomerOnly, p => p.RequireRole(UserRoles.Customer));
});

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IOtpSender, LoggingOtpSender>();
builder.Services.AddScoped<IAuthSettingsService, AuthSettingsService>();
builder.Services.AddScoped<ICategoryLookup, CategoryLookup>();
builder.Services.AddScoped<IServiceItemLookup, ServiceItemLookup>();
builder.Services.AddScoped<IPriceInsightService, PriceInsightService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
builder.Services.AddScoped<IKycHistoryService, KycHistoryService>();
builder.Services.AddScoped<IBankAccountService, BankAccountService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IErrorLogService, ErrorLogService>();
builder.Services.AddScoped<IEarningsService, EarningsService>();
builder.Services.AddScoped<IPlanService, PlanService>();
builder.Services.AddScoped<ITaxService, TaxService>();
builder.Services.Configure<PlatformOptions>(builder.Configuration.GetSection(PlatformOptions.SectionName));
builder.Services.AddHttpContextAccessor();

// Masters: add a new one by implementing IMasterSource and registering it here.
// The /api/masters/{key} endpoint and global.js pick it up automatically.
builder.Services.AddScoped<IMasterSource, SkillCategoryMasterSource>();
builder.Services.AddScoped<IMasterSource, ServiceItemMasterSource>();
builder.Services.AddScoped<IMasterSource, PartnerMasterSource>();
builder.Services.AddScoped<IMasterRegistry, MasterRegistry>();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicies.Clients, policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod();

        if (builder.Environment.IsDevelopment())
        {
            // Flutter's web dev server picks a random port on every run, so trust
            // any loopback origin here instead of chasing it in configuration.
            policy.SetIsOriginAllowed(origin =>
                allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)
                || (Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback));
        }
        else
        {
            // Outside development the allow-list is the only way in. An empty
            // list means no cross-origin browser client is permitted.
            policy.WithOrigins(allowedOrigins);
        }
    });
});

builder.Services.AddScoped<ITrackingLogService, TrackingLogService>();

builder.Services.Configure<FileStorageOptions>(
    builder.Configuration.GetSection(FileStorageOptions.SectionName));
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IBidService, BidService>();
builder.Services.AddScoped<ITaskClaimService, TaskClaimService>();
builder.Services.AddScoped<IRatingService, RatingService>();
builder.Services.AddScoped<IOrderHistoryService, OrderHistoryService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "gigapp_cart";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromDays(7);
});
builder.Services.AddSignalR();
builder.Services.AddScoped<IRealtimeNotifier, RealtimeNotifier>();
builder.Services.AddScoped<INotificationChannel, SignalRNotificationChannel>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IOfferService, OfferService>();
builder.Services.AddHostedService<OfferExpiryWorker>();
builder.Services.AddScoped<ISupportService, SupportService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        return new ValueTask(context.HttpContext.Response.WriteAsync(
            "{\"title\":\"Too many attempts. Please wait a few minutes and try again.\",\"status\":429}", ct));
    };

    // General auth actions: login, verify, register. Keyed by IP — a device
    // behind the same NAT shares the bucket, which is an acceptable trade-off
    // for a first line of defense against brute force and scripted signups.
    options.AddPolicy(RateLimiterPolicies.Auth, httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    // Requesting an OTP costs a real SMS once a provider is wired in, so this
    // stays tighter than the general auth policy.
    options.AddPolicy(RateLimiterPolicies.OtpRequest, httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
        }));
});

builder.Services.AddControllersWithViews(options =>
{
    // Global so every new write endpoint is audited without anyone
    // remembering to add it. Opt out with [SkipTracking].
    options.Filters.Add<TrackingActionFilter>();
    options.Filters.Add<GlobalExceptionFilter>();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "GigApp API", Version = "v1" });

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token from /api/auth/login — no \"Bearer \" prefix needed.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };

    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });

    // Document the API only. The Razor portals are HTML pages, not endpoints,
    // and Swashbuckle throws on any action without an explicit verb.
    options.DocInclusionPredicate((_, api) =>
        api.RelativePath?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DbSeeder.SeedAsync(context, logger);
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

// Session carries the guest cart, so it has to be in place before anything
// that reads it — and before auth, since a visitor shops without signing in.
app.UseSession();

app.UseCors(CorsPolicies.Clients);

// Must come after UseCors — a static file response bypasses everything after
// it in the pipeline, so an uploaded image never got a CORS header when this
// ran first, and Flutter web's cross-origin dev server could not load one.
app.UseStaticFiles();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<AppHub>("/hubs/app");

app.Run();

public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string SuperAdminOnly = "SuperAdminOnly";
    public const string PartnerOnly = "PartnerOnly";
    public const string CustomerOnly = "CustomerOnly";
}

public static class CorsPolicies
{
    public const string Clients = "GigAppClients";
}

public static class RateLimiterPolicies
{
    public const string Auth = "auth";
    public const string OtpRequest = "otpRequest";
}

public static class AuthCookie
{
    public const string Name = "gigapp_token";
    public const string RefreshName = "gigapp_refresh";
}

public static class PortalRedirects
{
    /// <summary>
    /// True for the server-rendered portal routes, which are only ever reached
    /// by a browser and so should redirect rather than return a bare status.
    /// Keyed off the path, not the Accept header — clients are inconsistent
    /// about sending text/html, and /api must always get status codes back.
    /// </summary>
    public static bool WantsHtml(HttpRequest request) =>
        request.Path.StartsWithSegments("/customer")
        || request.Path.StartsWithSegments("/provider")
        || request.Path.StartsWithSegments("/admin");

    public static string LoginPathFor(PathString path) =>
        path.StartsWithSegments("/admin") ? "/admin/login"
        : path.StartsWithSegments("/provider") ? "/provider/login"
        : "/customer/login";
}
