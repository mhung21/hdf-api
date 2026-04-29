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
    public class LoanContractDocumentController : ControllerBase
    {
        private readonly ILoanContractDocumentService _service;

        public LoanContractDocumentController(ILoanContractDocumentService service)
        {
            _service = service;
        }

        /// <summary>Lấy danh sách giấy tờ / ảnh đính kèm theo khoản vay.</summary>
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

        /// <summary>Upload ảnh hoặc PDF giấy tờ cho khoản vay (JPEG, PNG, WebP, PDF, tối đa theo cấu hình).</summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ResultAPI>> Upload([FromForm] UploadDocumentRequest request)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));
            try
            {
                var rs = await _service.Upload(request.LoanContractId, request.File, request.DocumentType, request.Note);
                return Ok(ResultAPI.Success(rs, "Tải lên giấy tờ thành công."));
            }
            catch (KeyNotFoundException ex)      { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
            catch (ArgumentException ex)         { return Ok(ResultAPI.Error(null, ex.Message, 400)); }
            catch (UnauthorizedAccessException)  { return Ok(ResultAPI.ResultWithAccessDenined()); }
        }

        /// <summary>Xem file inline trong trình duyệt (ảnh hoặc PDF).</summary>
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

        /// <summary>Tải file về máy.</summary>
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

        /// <summary>Xóa giấy tờ. Chỉ cho phép khi khoản vay ở trạng thái DRAFT.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Delete([FromBody] Guid documentId)
        {
            try
            {
                await _service.Delete(documentId);
                return Ok(ResultAPI.Success(null, "Đã xóa giấy tờ."));
            }
            catch (KeyNotFoundException ex)      { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
            catch (InvalidOperationException ex) { return Ok(ResultAPI.Error(null, ex.Message, 400)); }
            catch (UnauthorizedAccessException)  { return Ok(ResultAPI.ResultWithAccessDenined()); }
        }
    }

    public class UploadDocumentRequest
    {
        [Required] public Guid LoanContractId { get; set; }
        [Required] public IFormFile File      { get; set; } = null!;
        [Required] public string DocumentType  { get; set; } = "OTHER";
        public string? Note                   { get; set; }
    }
}
