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
    public class CollaboratorController : ControllerBase
    {
        private readonly ICollaboratorService _collaboratorService;
        private readonly IUserInfoService _userInfoService;

        public CollaboratorController(ICollaboratorService collaboratorService, IUserInfoService userInfoService)
        {
            _collaboratorService = collaboratorService;
            _userInfoService = userInfoService;
        }

        // GET api/Collaborator/GetAll
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAll()
        {
            var rs = await _collaboratorService.GetAlls();
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Collaborator/GetById
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetById([FromBody] Guid id)
        {
            var rs = await _collaboratorService.GetAsync(id);
            if (rs == null)
                return Ok(ResultAPI.Error(null, "Không tìm thấy CTV.", 404));

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Collaborator/Search
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Search([FromBody] SearchCollaboratorRequest request)
        {
            var rs = await _collaboratorService.SearchCollaborator(
                request.Keyword ?? string.Empty,
                request.PageIndex,
                request.PageSize);

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Collaborator/Save
        [HttpPost]
        [Authorize(Roles = "ADMIN,REGIONAL_MANAGER,STORE_MANAGER")]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CUCollaboratorModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(null, "Dữ liệu không hợp lệ.", 400));

            try
            {
                var rs = await _collaboratorService.Save(model);
                return Ok(ResultAPI.Success(rs));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
            catch (Exception ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 500));
            }
        }

        // POST api/Collaborator/CommissionReport
        /// <summary>Báo cáo hoa hồng CTV: tổng hợp hợp đồng giải ngân và tiền hoa hồng cần trả theo kỳ.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> CommissionReport([FromBody] CommissionReportRequest request)
        {
            var rs = await _collaboratorService.GetCommissionReport(
                request.FromDate,
                request.ToDate,
                request.StoreId,
                request.PageIndex,
                request.PageSize);
            return Ok(ResultAPI.Success(rs));
        }
    }

    /// <summary>Request model cho tìm kiếm CTV.</summary>
    public class SearchCollaboratorRequest
    {
        public string? Keyword { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>Request model cho báo cáo hoa hồng CTV.</summary>
    public class CommissionReportRequest
    {
        public DateOnly? FromDate  { get; set; }
        public DateOnly? ToDate    { get; set; }
        public Guid?     StoreId   { get; set; }
        public int       PageIndex { get; set; } = 1;
        public int       PageSize  { get; set; } = 50;
    }
}
