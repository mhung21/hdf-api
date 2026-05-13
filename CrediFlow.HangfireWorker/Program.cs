using CrediFlow.API.Models;
using CrediFlow.API.Services;
using CrediFlow.Common.Common.Startup;
using CrediFlow.HangfireWorker.Jobs;
using Hangfire;
using Hangfire.PostgreSql;
using Serilog;

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog((context, _, config) =>
    {
        config
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service_name", "hdf-hangfire-worker")
            .Enrich.WithProperty("environment", context.HostingEnvironment.EnvironmentName)
            .WriteTo.Console();
    })
    .ConfigureServices((context, services) =>
    {
        context.Configuration.Bind(ConfigRoot.Config);

        services.AddHttpContextAccessor();
        services.AddStartupServices(context.Configuration);
        services.AddCustomService(context.Configuration);
        services.AddScoped<ILoanScheduleRecalculationJob, RecalculateAllSchedulesJob>();

        var hangfireConnection = context.Configuration.GetConnectionString("CrediFlowConnection");
        if (string.IsNullOrWhiteSpace(hangfireConnection))
            throw new InvalidOperationException("Missing ConnectionStrings:CrediFlowConnection for Hangfire worker.");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(storage => storage.UseNpgsqlConnection(hangfireConnection)));

        services.AddHangfireServer(options =>
        {
            options.ServerName = $"hdf-hangfire-worker-{Environment.MachineName}";
            options.WorkerCount = Math.Max(1, Environment.ProcessorCount / 2);
            options.Queues = new[] { "maintenance" };
        });
    })
    .Build();

using (var scope = host.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    // Manual-only mode: never schedule this job automatically.
    recurringJobManager.RemoveIfExists("recalculate-all-schedules");
}

host.Run();
