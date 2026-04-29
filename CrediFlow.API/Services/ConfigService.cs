using CrediFlow.API.Interceptors;
using CrediFlow.API.Models;
using CrediFlow.API.Services;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public static class StartupExtensions
    {
        public static void AddCustomService(this IServiceCollection services, IConfiguration configuration)
        {
            // AuditInterceptor đăng ký singleton – IHttpContextAccessor là singleton an toàn
            services.AddSingleton<AuditInterceptor>();

            services.AddDbContext<CrediflowContext>((sp, options) =>
            {
                options.UseNpgsql(Config.ConnectionStrings.CrediFlowConnection);
                options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
            });

            // HttpClient cho việc gọi sang Identity server (đăng ký user, v.v.)
            services.AddHttpClient();
            // Register Redis Stream Service từ LASI.Common
            // services.AddRedisStreamService(configuration);

            #region Dependency Injection
            services.AddScoped<IDataAccessLogService,       DataAccessLogService>();
            services.AddScoped<ICustomerService,          CustomerService>();
            services.AddScoped<IStoreService,             StoreService>();
            services.AddScoped<IAppUserService,           AppUserService>();
            services.AddScoped<ILoanProductService,       LoanProductService>();
            services.AddScoped<ILoanContractService,      LoanContractService>();
            services.AddScoped<ICashVoucherService,       CashVoucherService>();
            services.AddScoped<ICustomerVisitService,     CustomerVisitService>();
            services.AddScoped<IBadDebtCaseService,       BadDebtCaseService>();
            services.AddScoped<ILoanChargeService,        LoanChargeService>();
            services.AddScoped<ILoanSettlementService,    LoanSettlementService>();
            services.AddScoped<IPolicySettingService,     PolicySettingService>();
            services.AddScoped<IInsuranceContractService,            InsuranceContractService>();
            services.AddScoped<ILoanContractAttachmentService, LoanContractAttachmentService>();
            services.AddScoped<ILoanCollateralService,         LoanCollateralService>();
            services.AddScoped<ILoanContractDocumentService,   LoanContractDocumentService>();
            services.AddScoped<IReportService,                 ReportService>();
            services.AddScoped<IRolePermissionService,         RolePermissionService>();
            services.AddScoped<ICustomRoleService,             CustomRoleService>();
            services.AddScoped<ICollaboratorService,           CollaboratorService>();
            services.AddScoped<ICustomerDocumentService,        CustomerDocumentService>();
            services.AddScoped<ICustomerSourceService,          CustomerSourceService>();
            #endregion Dependency Injection
        }

        //public static void AddDebugCustomService(this IServiceCollection services, IConfiguration configuration)
        //{
        //    services.AddSingleton<IDiscoveryClient, FakeDiscoveryClient>();
        //}

        //public static void SetConfiguration(this IServiceCollection services, IConfiguration configuration)
        //{
        //    // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-3.1

        //    Config.Key = configuration["Key"];
        //    Config.KeyUrl = configuration["KeyUrl"];

        //    Config.Domain.ContactId = configuration["Domain:ContactId"];
        //}
    }
}