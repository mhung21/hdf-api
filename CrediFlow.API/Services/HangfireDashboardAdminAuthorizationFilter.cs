using Hangfire.Dashboard;

namespace CrediFlow.API.Services
{
    public class HangfireDashboardAdminAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public const string AuthCookieName = "hf_dashboard_auth";

        private readonly string _authCookieValue;

        public HangfireDashboardAdminAuthorizationFilter(string authCookieValue)
        {
            _authCookieValue = authCookieValue;
        }

        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();
            var cookieValue = httpContext.Request.Cookies[AuthCookieName];
            if (!string.IsNullOrWhiteSpace(cookieValue)
                && string.Equals(cookieValue, _authCookieValue, StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }
    }
}
