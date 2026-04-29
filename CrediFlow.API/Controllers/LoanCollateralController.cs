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
    public class LoanCollateralController : ControllerBase
    {
        private readonly ILoanCollateralService _service;

        public LoanCollateralController(ILoanCollateralService service)
        {
            _service = service;
        }

        /// <summary>Lấy danh sách tài sản đảm bảo theo khoản vay.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetByLoanContract([FromBody] Guid loanContractId)
        {
            try
            {
                var rs = await _service.GetByLoanContract(loanContractId);
                return Ok(ResultAPI.Success(rs));
            }
            catch (KeyNotFoundException ex)     { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
            catch (UnauthorizedAccessException) { return Ok(ResultAPI.ResultWithAccessDenined()); }
        }

        /// <summary>Tạo mới hoặc cập nhật tài sản đảm bảo. Chỉ cho phép khi khoản vay ở trạng thái DRAFT.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CULoanCollateralModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));
            try
            {
                var rs = await _service.Save(model);
                return Ok(ResultAPI.Success(rs));
            }
            catch (KeyNotFoundException ex)      { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
            catch (InvalidOperationException ex) { return Ok(ResultAPI.Error(null, ex.Message, 400)); }
            catch (UnauthorizedAccessException)  { return Ok(ResultAPI.ResultWithAccessDenined()); }
        }

        /// <summary>Xóa tài sản đảm bảo. Chỉ cho phép khi khoản vay ở trạng thái DRAFT.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Delete([FromBody] Guid collateralId)
        {
            try
            {
                await _service.Delete(collateralId);
                return Ok(ResultAPI.Success(null, "Đã xóa tài sản đảm bảo."));
            }
            catch (KeyNotFoundException ex)      { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
            catch (InvalidOperationException ex) { return Ok(ResultAPI.Error(null, ex.Message, 400)); }
            catch (UnauthorizedAccessException)  { return Ok(ResultAPI.ResultWithAccessDenined()); }
        }
    }
}
