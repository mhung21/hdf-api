using System.Security.Claims;
using CrediFlow.Common.Common.Startup;
using CrediFlow.API.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using CrediFlow.API.Services;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

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
                "https://localhost:4200",
                "http://103.176.179.103:8080",    // Test frontend
                "http://103.176.179.103:8883",    // Test API
                "http://103.176.179.103:8884")    // Test Identity
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// Log toàn bộ hệ thống (những chỗ không có try catch thì lỗi sẽ bắn vào đây) => Như vậy có thể bỏ hết try catch trên các controller đi
//https://stackoverflow.com/questions/38630076/asp-net-core-web-api-exception-handling

app.UseExceptionHandler(a => a.Run(async context =>
{
    var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
    var ex = exceptionHandlerPathFeature.Error;

    Console.WriteLine($"Lỗi: {exceptionHandlerPathFeature.Path} - Message: {ex.Message} - InnerException: {ex.InnerException} - StackTrace: {ex.StackTrace}");

    context.Response.StatusCode = 500;
    context.Response.ContentType = "application/json";
    var errorResponse = new { success = false, message = ex.Message, path = exceptionHandlerPathFeature.Path };
    await context.Response.WriteAsJsonAsync(errorResponse);
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

app.MapControllers();

app.Run();
