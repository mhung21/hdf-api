using System.Text.Json;
using CrediFlow.API.Models;
using CrediFlow.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CrediFlow.API.Interceptors
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ActivityLogReadAttribute : TypeFilterAttribute
    {
        public ActivityLogReadAttribute(string moduleCode, string entityType)
            : base(typeof(ActivityLogReadFilter))
        {
            Arguments = new object[] { moduleCode, entityType };
        }
    }

    public class ActivityLogReadFilter : IAsyncActionFilter
    {
        private readonly IActivityLogService _activityLogService;
        private readonly string _moduleCode;
        private readonly string _entityType;

        public ActivityLogReadFilter(IActivityLogService activityLogService, string moduleCode, string entityType)
        {
            _activityLogService = activityLogService;
            _moduleCode = moduleCode;
            _entityType = entityType;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executed = await next();
            if (executed.Exception != null && !executed.ExceptionHandled)
                return;

            var request = context.HttpContext.Request;
            var method = request.Method.ToUpperInvariant();
            var actionCode = ResolveActionCode(context.ActionDescriptor.DisplayName, method);

            var entityId = TryGetGuidArg(context.ActionArguments, "id")
                           ?? TryGetGuidArg(context.ActionArguments, "loanContractId")
                           ?? TryGetGuidArg(context.ActionArguments, "customerId");

            var loanContractId = TryGetGuidArg(context.ActionArguments, "loanContractId");
            var customerId = TryGetGuidArg(context.ActionArguments, "customerId");
            if (_moduleCode.Equals("CUSTOMER", StringComparison.OrdinalIgnoreCase) && customerId == null)
                customerId = entityId;
            if (_moduleCode.Equals("LOAN_CONTRACT", StringComparison.OrdinalIgnoreCase) && loanContractId == null)
                loanContractId = entityId;

            var metadata = JsonSerializer.Serialize(new
            {
                method,
                path = request.Path.Value,
                query = request.QueryString.Value,
            });

            await _activityLogService.LogAsync(new ActivityLogWriteModel
            {
                ModuleCode = _moduleCode,
                ActionCode = actionCode,
                EntityType = _entityType,
                EntityId = entityId,
                CustomerId = customerId,
                LoanContractId = loanContractId,
                Summary = $"{method} {request.Path.Value}",
                Metadata = metadata,
                RequestPath = request.Path.Value,
            });
        }

        private static string ResolveActionCode(string? displayName, string method)
        {
            var text = (displayName ?? string.Empty).ToLowerInvariant();
            if (text.Contains("search")) return "SEARCH";
            if (text.Contains("export")) return "EXPORT";
            if (method == "GET") return "VIEW";
            return "VIEW";
        }

        private static Guid? TryGetGuidArg(IDictionary<string, object?> args, string key)
        {
            if (args.TryGetValue(key, out var direct) && direct is Guid g)
                return g;

            foreach (var arg in args.Values)
            {
                if (arg == null) continue;
                var prop = arg.GetType().GetProperty(key, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop?.GetValue(arg) is Guid value)
                    return value;
            }

            return null;
        }
    }
}