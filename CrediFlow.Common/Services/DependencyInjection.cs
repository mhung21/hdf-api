using CrediFlow.Common.Caching;
using CrediFlow.Common.Models;
using CrediFlow.Common.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace CrediFlow.Common.Common.Startup
{
    public static class DependencyInjection
    {
        /// <summary>
        /// Register common services
        /// </summary>
        public static void AddStartupServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IUserInfoService, UserInfoService>();
            services.AddScoped<IResultAPI, ResultAPI>();

            // Add caching - CachingHelper requires IDistributedCache
            services.AddDistributedMemoryCache(); // Fallback to in-memory cache
            services.AddSingleton<ICachingHelper, CachingHelper>();
            
            // Add Station Data Cache Service

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddCors();
            services.AddResponseCaching();

            services.AddControllers().AddNewtonsoftJson(delegate (MvcNewtonsoftJsonOptions options)
            {
                options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                if (configuration.GetValue("UseUTCTimeZone", defaultValue: false))
                {
                    options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
                }
            });
        }

        public static IServiceCollection AddRedisStreamService(this IServiceCollection services, IConfiguration configuration)
        {
            // Bind Redis configuration từ appsettings.json
            services.Configure<RedisConfiguration>(configuration.GetSection("Redis"));

            // Register Redis Stream Service as Singleton (để reuse connection)
            services.AddSingleton<IRedisStreamService, RedisStreamService>();

            return services;
        }

        /// <summary>
        /// [DEPRECATED] Dùng AddRedisStreamService thay thế
        /// </summary>
        [Obsolete("Use AddRedisStreamService instead. Queue-based approach is deprecated.")]
        public static IServiceCollection AddRedisQueueService(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RedisConfiguration>(configuration.GetSection("Redis"));
            services.AddSingleton<IRedisQueueService, RedisQueueService>();
            return services;
        }

        //public static void AddProductionStartupService(this IServiceCollection services, IConfiguration configuration)
        //{
        //    if (configuration.GetValue("UseEureka", defaultValue: false))
        //    {
        //        RetryPolicy retryPolicy = Policy.Handle<Exception>().WaitAndRetryForever((int retryAttempt) => TimeSpan.FromSeconds(Math.Pow(2.0, retryAttempt)));
        //        retryPolicy.Execute(delegate
        //        {
        //            Console.WriteLine("Try to connect eureka");
        //            services.ConfigureCloudFoundryOptions(configuration);
        //            services.AddDiscoveryClient(configuration);
        //        });
        //    }
        //}

        //public static void UseStartupService(this IApplicationBuilder app, IConfiguration configuration = null, ILoggerFactory loggerFactory = null)
        //{
        //    CultureInfo defaultThreadCurrentUICulture = (CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("vi-VN"));
        //    CultureInfo.DefaultThreadCurrentUICulture = defaultThreadCurrentUICulture;
        //    loggerFactory?.AddSerilog();
        //    app.UseCors(delegate (CorsPolicyBuilder builder)
        //    {
        //        builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        //    });
        //    if (configuration.GetValue("UseAuthentication", defaultValue: false))
        //    {
        //        app.UseAuthentication();
        //    }

        //    if (configuration.GetValue("UseAuthorization", defaultValue: false))
        //    {
        //        app.UseAuthorizationServer(loggerFactory);
        //    }

        //    if (configuration.GetValue("UseSwagger", defaultValue: false))
        //    {
        //        app.UseSwagger();
        //        app.UseSwaggerUI(delegate (SwaggerUIOptions c)
        //        {
        //            c.SwaggerEndpoint(configuration.GetValue("Swagger:Url", ""), "My API V1");
        //        });
        //    }

        //    app.UseResponseCaching();
        //}

        //public static void UseProductionStartupService(this IApplicationBuilder app, IConfiguration configuration)
        //{
        //    app.UseDeveloperExceptionPage();
        //    if (configuration.GetValue("UseEureka", defaultValue: false))
        //    {
        //        RetryPolicy retryPolicy = Policy.Handle<Exception>().WaitAndRetryForever((int retryAttempt) => TimeSpan.FromSeconds(Math.Pow(2.0, retryAttempt)));
        //        retryPolicy.Execute(delegate
        //        {
        //            Console.WriteLine("Try to connect eureka");
        //            app.UseDiscoveryClient();
        //        });
        //    }
        //}

        //public static IWebHostBuilder BuildEUniversityWebHost(this IWebHostBuilder webHostBuilder)
        //{
        //    return webHostBuilder.LogException().UseKestrel(delegate (KestrelServerOptions options)
        //    {
        //        options.AddServerHeader = false;
        //        options.Listen(IPAddress.Any);
        //    });
        //}
    }
}
