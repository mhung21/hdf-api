using CrediFlow.API.Services;
using CrediFlow.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace CrediFlow.API.Controllers
{
    [Authorize]
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class CustomerDocumentController : ControllerBase
    {
        private readonly ICustomerDocumentService _service;

        public CustomerDocumentController(ICustomerDocumentService service)
        {
            _service = service;
        }

        /// <summary>Lấy danh sách ảnh CCCD / giấy tờ theo khách hàng. Chỉ Admin/Manager.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetByCustomer([FromBody] Guid customerId)
        {
            try
            {
                var rs = await _service.GetByCustomer(customerId);
                return Ok(ResultAPI.Success(rs));
            }
            catch (KeyNotFoundException ex)     { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
            catch (UnauthorizedAccessException) { return Ok(ResultAPI.ResultWithAccessDenined()); }
        }

        /// <summary>Upload ảnh CCCD cho khách hàng (JPEG, PNG, WebP).</summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ResultAPI>> Upload([FromForm] UploadCustomerDocumentRequest request)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));
            try
            {
                var rs = await _service.Upload(request.CustomerId, request.File, request.DocumentType, request.Note);
                return Ok(ResultAPI.Success(rs, "Tải lên ảnh CCCD thành công."));
            }
            catch (KeyNotFoundException ex)      { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
            catch (ArgumentException ex)         { return Ok(ResultAPI.Error(null, ex.Message, 400)); }
            catch (UnauthorizedAccessException)  { return Ok(ResultAPI.ResultWithAccessDenined()); }
        }

        /// <summary>Xem ảnh CCCD inline trong trình duyệt. Chỉ Admin/Manager.</summary>
        [HttpGet("{documentId:guid}")]
        public async Task<IActionResult> View(Guid documentId)
        {
            try
            {
                var (stream, meta) = await _service.GetFileForStream(documentId);
                Response.Headers["Cache-Control"]       = "private, max-age=3600";
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{Uri.EscapeDataString(meta.FileName)}\"";
                return File(stream, meta.ContentType);
            }
            catch (KeyNotFoundException ex)     { return NotFound(ex.Message); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (FileNotFoundException ex)    { return NotFound(ex.Message); }
        }

        /// <summary>Tải ảnh CCCD về máy. Chỉ Admin/Manager.</summary>
        [HttpGet("{documentId:guid}")]
        public async Task<IActionResult> Download(Guid documentId)
        {
            try
            {
                var (stream, meta) = await _service.GetFileForStream(documentId);
                return File(stream, meta.ContentType, meta.FileName);
            }
            catch (KeyNotFoundException ex)     { return NotFound(ex.Message); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (FileNotFoundException ex)    { return NotFound(ex.Message); }
        }
    }

    public class UploadCustomerDocumentRequest
    {
        [Required] public Guid CustomerId    { get; set; }
        [Required] public IFormFile File     { get; set; } = null!;
        [Required] public string DocumentType { get; set; } = "ID_FRONT";
        public string? Note { get; set; }
    }
}
