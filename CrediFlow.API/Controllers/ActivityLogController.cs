using CrediFlow.API.Models;
using CrediFlow.API.Services;
using CrediFlow.Common.Models;
using CrediFlow.Common.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrediFlow.API.Controllers
{
    [Authorize]
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ActivityLogController : ControllerBase
    {
        private readonly IActivityLogService _activityLogService;
        private readonly IUserInfoService _userInfoService;

        public ActivityLogController(IActivityLogService activityLogService, IUserInfoService userInfoService)
        {
            _activityLogService = activityLogService;
            _userInfoService = userInfoService;
        }

        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Search([FromBody] ActivityLogSearchRequest request)
        {
            if (!_userInfoService.IsAdmin)
                return Ok(ResultAPI.ResultWithAccessDenined());

            var result = await _activityLogService.SearchAsync(request);
            return Ok(ResultAPI.Success(result));
        }
    }
}