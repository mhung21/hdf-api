using System.Security.Claims;
using CrediFlow.Common.Common.Startup;
using CrediFlow.API.Models;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using CrediFlow.API.Services;
using Microsoft.OpenApi;
using System.Security.Cryptography;
using System.Text;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

var builder = WebApplication.CreateBuilder(args);

// =========================================================
// SERILOG LOGGING
// =========================================================
var lokiUrl = builder.Configuration["Loki:Url"] ?? "http://hdf-loki:3100";

builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("service_name", "hdf-api")
        .Enrich.WithProperty("environment", context.HostingEnvironment.EnvironmentName)
        .WriteTo.Console()
        .WriteTo.GrafanaLoki(
            uri: lokiUrl,
            labels: new List<LokiLabel>
            {
                new() { Key = "service_name", Value = "hdf-api" },
                new() { Key = "environment", Value = context.HostingEnvironment.EnvironmentName }
            },
            propertiesAsLabels: new[] { "level" }
        );
});

builder.Configuration.Bind(ConfigRoot.Config);

// Add services to the container.


// Read JWT config directly from IConfiguration (Config static properties are NOT
// populated by Bind() — .NET ConfigurationBinder skips static props silently)
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"];
var jwtAudience = builder.Configuration["JwtSettings:Audience"];
var identityServerUrl = builder.Configuration["Urls:IdentityServer"]?.TrimEnd('/');

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Identity service exposes /.well-known/openid-configuration và JWKS
        // Use MetadataAddress (instead of Authority) to avoid OIDC discovery issuer mismatch
        // between internal Docker URL (Urls.IdentityServer) and external Issuer domain
        if (!string.IsNullOrEmpty(identityServerUrl))
        {
            options.MetadataAddress = $"{identityServerUrl}/.well-known/openid-configuration";
        }
        else
        {
            // Local dev without Identity server — use Issuer as Authority fallback
            options.Authority = jwtIssuer;
        }
        options.Audience = jwtAudience;
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = "role_code"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("is_admin", "true", "True", "TRUE"));

    options.AddPolicy("StoreManagerOnly", policy =>
        policy.RequireClaim("is_store_manager", "true", "True", "TRUE"));

    options.AddPolicy("CanManageCustomers", policy =>
        policy.RequireClaim("permission", "customer.manage"));
});



builder.Services.AddHttpContextAccessor();
builder.Services.AddStartupServices(builder.Configuration);
builder.Services.AddCustomService(builder.Configuration);
builder.Services.AddSingleton<TelegramAlertService>();

var hangfireConnection = builder.Configuration.GetConnectionString("CrediFlowConnection");
if (string.IsNullOrWhiteSpace(hangfireConnection))
    throw new InvalidOperationException("Missing ConnectionStrings:CrediFlowConnection for Hangfire.");

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(storage => storage.UseNpgsqlConnection(hangfireConnection)));






builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// builder.Services.AddSwaggerGen(c =>
// {
//     c.SwaggerDoc("v1", new OpenApiInfo
//     {
//         Title = "CrediFlow Identity API",
//         Version = "v1",
//         Description = "Authentication and authorization service for CrediFlow loan management system"
//     });

//     // Add JWT Authentication to Swagger
//     var securityScheme = new OpenApiSecurityScheme
//     {
//         Name = "Authorization",
//         Type = SecuritySchemeType.Http,
//         Scheme = "bearer",
//         BearerFormat = "JWT",
//         In = ParameterLocation.Header,
//         Description = "Enter your JWT token in the format: Bearer {token}"
//     };
//     c.AddSecurityDefinition("Bearer", securityScheme);

//     c.AddSecurityRequirement(new OpenApiSecurityRequirement
//     {
//         { securityScheme, Array.Empty<string>() }
//     });
// });
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200",
                "https://quanly.hdfinanceco.vn",
                "https://quanly-dev.hdfinanceco.vn",    // Test domain
                "https://localhost:4200")    // Test Identity
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var dashboardUsername = builder.Configuration["HangfireDashboardAuth:Username"];
var dashboardPassword = builder.Configuration["HangfireDashboardAuth:Password"];
var dashboardRealm = builder.Configuration["HangfireDashboardAuth:Realm"];

if (string.IsNullOrWhiteSpace(dashboardUsername) || string.IsNullOrWhiteSpace(dashboardPassword))
    throw new InvalidOperationException("Missing HangfireDashboardAuth Username/Password in appsettings.");

dashboardUsername = dashboardUsername.Trim();
dashboardPassword = dashboardPassword.Trim();
dashboardRealm = string.IsNullOrWhiteSpace(dashboardRealm) ? "CrediFlow Hangfire" : dashboardRealm.Trim();

string BuildDashboardAuthCookieValue(string username, string password)
{
    var payload = $"{username}:{password}";
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
    return Convert.ToHexString(hash);
}

var dashboardCookieValue = BuildDashboardAuthCookieValue(dashboardUsername, dashboardPassword);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ── Middleware: Capture request body cho POST/PUT/PATCH ──
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    if (method is "POST" or "PUT" or "PATCH" && context.Request.ContentType?.Contains("json") == true)
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        // Mask sensitive fields (password)
        if (body.Length > 0)
        {
            body = System.Text.RegularExpressions.Regex.Replace(
                body, @"(?i)(""password""\s*:\s*"")[^""]*""", "$1***\"");
            body = System.Text.RegularExpressions.Regex.Replace(
                body, @"(?i)(""currentPassword""\s*:\s*"")[^""]*""", "$1***\"");
            body = System.Text.RegularExpressions.Regex.Replace(
                body, @"(?i)(""newPassword""\s*:\s*"")[^""]*""", "$1***\"");

            if (body.Length > 4096) body = body[..4096] + "...(truncated)";
            context.Items["RequestBody"] = body;
        }
    }
    await next();
});

// Serilog HTTP request logging — enrich with curl-like info
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        var req = httpContext.Request;
        diagnosticContext.Set("RequestMethod", req.Method);
        diagnosticContext.Set("RequestPath", req.Path.ToString());
        diagnosticContext.Set("QueryString", req.QueryString.ToString());
        diagnosticContext.Set("ContentType", req.ContentType ?? "");
        diagnosticContext.Set("UserAgent", req.Headers["User-Agent"].ToString());
        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString() ?? "");

        if (httpContext.Items.TryGetValue("RequestBody", out var body) && body is string bodyStr)
        {
            diagnosticContext.Set("RequestBody", bodyStr);
            // Build curl command
            var curl = $"curl -X {req.Method} '{req.Scheme}://{req.Host}{req.Path}{req.QueryString}' -H 'Content-Type: {req.ContentType}' -d '{bodyStr}'";
            diagnosticContext.Set("CurlCommand", curl);
        }
    };
});

// Log toàn bộ hệ thống (những chỗ không có try catch thì lỗi sẽ bắn vào đây) => Như vậy có thể bỏ hết try catch trên các controller đi
//https://stackoverflow.com/questions/38630076/asp-net-core-web-api-exception-handling

app.UseExceptionHandler(a => a.Run(async context =>
{
    var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
    var ex = exceptionHandlerPathFeature.Error;

    Log.Error(ex, "Unhandled exception at {Path}", exceptionHandlerPathFeature.Path);

    context.Response.StatusCode = 500;
    context.Response.ContentType = "application/json";
    var result = CrediFlow.Common.Models.ResultAPI.Error(null, "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.");
    await context.Response.WriteAsJsonAsync(result);

    // ── Telegram Alert ──
    try
    {
        var telegram = context.RequestServices.GetService<TelegramAlertService>();
        if (telegram?.IsEnabled == true)
        {
            var req = context.Request;
            var curlCmd = context.Items.TryGetValue("RequestBody", out var bodyObj) && bodyObj is string bodyStr
                ? $"curl -X {req.Method} '{req.Scheme}://{req.Host}{req.Path}{req.QueryString}' -H 'Content-Type: {req.ContentType}' -d '{bodyStr}'"
                : $"curl -X {req.Method} '{req.Scheme}://{req.Host}{req.Path}{req.QueryString}'";

            _ = telegram.SendErrorAlertAsync(
                httpMethod: req.Method,
                requestPath: req.Path.ToString(),
                statusCode: 500,
                errorMessage: ex?.Message + (ex?.InnerException != null ? $"\n→ {ex.InnerException.Message}" : ""),
                curlCommand: curlCmd,
                responseBody: System.Text.Json.JsonSerializer.Serialize(result),
                clientIp: context.Connection.RemoteIpAddress?.ToString());
        }
    }
    catch { /* Alert failure must not affect API response */ }
}));

// Disabled: Nginx xử lý SSL/TLS (reverse proxy), app chỉ nhận HTTP từ proxy.
// Middleware này sẽ cố redirect HTTP → HTTPS nhưng không biết port HTTPS → lỗi.
// Do not uncomment unless deployed as standalone HTTPS app.
// if (!app.Environment.IsDevelopment())
// {
//     app.UseHttpsRedirection();
// }

app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

string RenderDashboardLoginPage(string title, string? errorMessage, string returnUrl)
{
    var safeTitle = System.Net.WebUtility.HtmlEncode(title);
    var safeError = System.Net.WebUtility.HtmlEncode(errorMessage ?? string.Empty);
    var safeReturnUrl = System.Net.WebUtility.HtmlEncode(returnUrl);

    return $@"<!doctype html>
<html lang=""en""><head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>{safeTitle} Login</title>
  <style>
    body {{ font-family: Segoe UI, sans-serif; background: #f5f7fb; margin: 0; display: grid; place-items: center; min-height: 100vh; }}
    .card {{ background: #fff; width: min(92vw, 380px); border-radius: 12px; padding: 24px; box-shadow: 0 8px 30px rgba(0,0,0,.08); }}
    h1 {{ margin: 0 0 16px; font-size: 20px; }}
    label {{ display: block; margin: 10px 0 6px; color: #334155; font-size: 14px; }}
    input {{ width: 100%; padding: 10px 12px; border: 1px solid #cbd5e1; border-radius: 8px; box-sizing: border-box; }}
    button {{ margin-top: 14px; width: 100%; border: 0; border-radius: 8px; padding: 11px 14px; background: #0f766e; color: #fff; font-weight: 600; cursor: pointer; }}
    .err {{ margin-top: 8px; color: #dc2626; font-size: 13px; min-height: 18px; }}
  </style>
</head>
<body>
  <form class=""card"" method=""post"" action=""/hangfire/login"">
    <h1>{safeTitle}</h1>
    <label for=""username"">Username</label>
    <input id=""username"" name=""username"" autocomplete=""username"" required />
    <label for=""password"">Password</label>
    <input id=""password"" name=""password"" type=""password"" autocomplete=""current-password"" required />
    <input type=""hidden"" name=""returnUrl"" value=""{safeReturnUrl}"" />
    <button type=""submit"">Sign in</button>
    <div class=""err"">{safeError}</div>
  </form>
</body>
</html>";
}

app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/hangfire/login", StringComparison.OrdinalIgnoreCase))
    {
        if (HttpMethods.IsGet(context.Request.Method))
        {
            var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase))
                returnUrl = "/hangfire";

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(RenderDashboardLoginPage(dashboardRealm, null, returnUrl));
            return;
        }

        if (HttpMethods.IsPost(context.Request.Method))
        {
            var form = await context.Request.ReadFormAsync();
            var username = form["username"].FirstOrDefault()?.Trim() ?? string.Empty;
            var password = form["password"].FirstOrDefault() ?? string.Empty;
            var returnUrl = form["returnUrl"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase))
                returnUrl = "/hangfire";

            if (string.Equals(username, dashboardUsername, StringComparison.Ordinal)
                && string.Equals(password, dashboardPassword, StringComparison.Ordinal))
            {
                context.Response.Cookies.Append(HangfireDashboardAdminAuthorizationFilter.AuthCookieName, dashboardCookieValue, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddHours(8),
                    Path = "/hangfire"
                });

                context.Response.Redirect(returnUrl);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(RenderDashboardLoginPage(dashboardRealm, "Sai username hoặc password.", returnUrl));
            return;
        }

        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
        return;
    }

    if (context.Request.Path.Equals("/hangfire/logout", StringComparison.OrdinalIgnoreCase)
        && HttpMethods.IsGet(context.Request.Method))
    {
        context.Response.Cookies.Delete(HangfireDashboardAdminAuthorizationFilter.AuthCookieName, new CookieOptions
        {
            Path = "/hangfire"
        });

        context.Response.Redirect("/hangfire/login");
        return;
    }

    await next();
});

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/hangfire")
        && !context.Request.Path.StartsWithSegments("/hangfire/login")
        && !context.Request.Path.StartsWithSegments("/hangfire/logout"))
    {
        var cookieValue = context.Request.Cookies[HangfireDashboardAdminAuthorizationFilter.AuthCookieName];
        var isDashboardLoggedIn = !string.IsNullOrWhiteSpace(cookieValue)
            && string.Equals(cookieValue, dashboardCookieValue, StringComparison.Ordinal);

        if (!isDashboardLoggedIn)
        {
            var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
            context.Response.Redirect($"/hangfire/login?returnUrl={returnUrl}");
            return;
        }
    }

    await next();
});

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[]
    {
        new HangfireDashboardAdminAuthorizationFilter(dashboardCookieValue)
    }
});

app.MapControllers();

app.Run();
