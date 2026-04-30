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
    public class InsuranceContractController : ControllerBase
    {
        private readonly IInsuranceContractService _insuranceContractService;
        private readonly IUserInfoService          _userInfoService;

        public InsuranceContractController(IInsuranceContractService insuranceContractService, IUserInfoService userInfoService)
        {
            _insuranceContractService = insuranceContractService;
            _userInfoService          = userInfoService;
        }

        // GET api/InsuranceContract/GetAll
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAll()
        {
            var rs = await _insuranceContractService.GetAlls();
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/InsuranceContract/GetById
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetById([FromBody] Guid id)
        {
            var rs = await _insuranceContractService.GetAsync(id);
            if (rs == null)
                return Ok(ResultAPI.Error(null, "Không tìm thấy hợp đồng bảo hiểm.", 404));

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/InsuranceContract/Search
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Search([FromBody] SearchInsuranceContractRequest request)
        {
            var rs = await _insuranceContractService.SearchInsuranceContract(
                request.Keyword   ?? string.Empty,
                request.PageIndex,
                request.PageSize,
                request.SortBy,
                request.SortDesc);

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/InsuranceContract/Save
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CUInsuranceContractModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            bool isUpdate = model.InsuranceContractId.HasValue && model.InsuranceContractId != Guid.Empty;
            try
            {
                var rs = await _insuranceContractService.Save(model);
                return Ok(ResultAPI.Success(rs, $"{(isUpdate ? "Cập nhật" : "Thêm mới")} hợp đồng bảo hiểm thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
        }
    }

    public class SearchInsuranceContractRequest
    {
        public string? Keyword   { get; set; }
        public int     PageIndex { get; set; } = 1;
        public int     PageSize  { get; set; } = 1000;
        /// <summary>ProviderName | StatusCode | EffectiveFrom | PremiumAmount | CreatedAt</summary>
        public string? SortBy    { get; set; } = "CreatedAt";
        public bool    SortDesc  { get; set; } = true;
    }
}
