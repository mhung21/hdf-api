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
    public class CustomerSourceController : ControllerBase
    {
        private readonly ICustomerSourceService _customerSourceService;
        private readonly IUserInfoService       _userInfoService;

        public CustomerSourceController(ICustomerSourceService customerSourceService, IUserInfoService userInfoService)
        {
            _customerSourceService = customerSourceService;
            _userInfoService       = userInfoService;
        }

        // GET api/CustomerSource/GetAll
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAll()
        {
            var rs = await _customerSourceService.GetAlls();
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/CustomerSource/Save
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CUCustomerSourceModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            if (!_userInfoService.IsAdmin && !_userInfoService.IsStoreManager)
                return Ok(ResultAPI.ResultWithAccessDenined());

            bool isUpdate = model.SourceId.HasValue && model.SourceId != Guid.Empty;
            try
            {
                var rs = await _customerSourceService.Save(model);
                return Ok(ResultAPI.Success(rs, $"{(isUpdate ? "Cập nhật" : "Thêm mới")} luồng khách '{model.SourceName}' thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
            catch (InvalidOperationException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 400));
            }
        }

        // POST api/CustomerSource/Delete
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Delete([FromBody] Guid id)
        {
            if (!_userInfoService.IsAdmin && !_userInfoService.IsStoreManager)
                return Ok(ResultAPI.ResultWithAccessDenined());

            try
            {
                await _customerSourceService.Delete(id);
                return Ok(ResultAPI.Success(null, "Xóa luồng khách thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
            catch (InvalidOperationException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 400));
            }
        }
    }
}
