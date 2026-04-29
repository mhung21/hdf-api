using CrediFlow.API.Services;
using CrediFlow.Common.Models;
using CrediFlow.Common.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace CrediFlow.API.Controllers
{
    [Authorize]
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class LoanContractAttachmentController : ControllerBase
    {
        private readonly ILoanContractAttachmentService _service;
        private readonly IDataAccessLogService          _dataAccessLog;

        public LoanContractAttachmentController(ILoanContractAttachmentService service,
                                               IDataAccessLogService dataAccessLog)
        {
            _service       = service;
            _dataAccessLog = dataAccessLog;
        }

        // POST api/LoanContractAttachment/Upload
        // Nhận multipart/form-data với field "file" (PDF) và "loanContractId"
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ResultAPI>> Upload([FromForm] UploadContractAttachmentRequest request)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            try
            {
                var rs = await _service.Upload(request.LoanContractId, request.File, request.Note);
                return Ok(ResultAPI.Success(rs, "Tải lên hợp đồng thành công."));
            }
            catch (KeyNotFoundException ex)        { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
            catch (InvalidOperationException ex)   { return Ok(ResultAPI.Error(null, ex.Message, 400)); }
            catch (ArgumentException ex)           { return Ok(ResultAPI.Error(null, ex.Message, 400)); }
            catch (UnauthorizedAccessException)    { return Ok(ResultAPI.ResultWithAccessDenined()); }
        }

        // POST api/LoanContractAttachment/GetByLoanContract
        // Lấy metadata của file đính kèm theo khoản vay
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetByLoanContract([FromBody] Guid loanContractId)
        {
            var rs = await _service.GetByLoanContractId(loanContractId);
            return Ok(ResultAPI.Success(rs));
        }

        // GET api/LoanContractAttachment/ViewPdf/{attachmentId}
        // ─────────────────────────────────────────────────────────
        // Frontend dùng URL này trong <iframe> hoặc mở tab mới.
        // Browser nhận Content-Disposition: inline → tự render PDF.
        // Cache-Control: private, max-age=3600 → browser cache 1 giờ, không qua CDN.
        [HttpGet("{attachmentId:guid}")]
        public async Task<IActionResult> ViewPdf(Guid attachmentId)
        {
            try
            {
                var (stream, meta) = await _service.GetFileForStream(attachmentId);
                await _dataAccessLog.LogAsync("CONTRACT_PDF", attachmentId, "VIEW");

                // Cache phía browser (1 giờ), không cache phía proxy/CDN (private)
                Response.Headers["Cache-Control"] = "private, max-age=3600";
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{Uri.EscapeDataString(meta.FileName)}\"";

                // ASP.NET Core sẽ tự đóng stream sau khi streaming xong
                return File(stream, "application/pdf");
            }
            catch (KeyNotFoundException ex)       { return NotFound(ex.Message); }
            catch (UnauthorizedAccessException)   { return Forbid(); }
            catch (FileNotFoundException ex)       { return NotFound(ex.Message); }
        }

        // GET api/LoanContractAttachment/Download/{attachmentId}
        // ─────────────────────────────────────────────────────────
        // Tải file về máy (Content-Disposition: attachment).
        [HttpGet("{attachmentId:guid}")]
        public async Task<IActionResult> Download(Guid attachmentId)
        {
            try
            {
                var (stream, meta) = await _service.GetFileForStream(attachmentId);
                await _dataAccessLog.LogAsync("CONTRACT_PDF", attachmentId, "DOWNLOAD");
                return File(stream, "application/pdf", meta.FileName);
            }
            catch (KeyNotFoundException ex)       { return NotFound(ex.Message); }
            catch (UnauthorizedAccessException)   { return Forbid(); }
            catch (FileNotFoundException ex)       { return NotFound(ex.Message); }
        }
    }

    /// <summary>Request body cho upload hợp đồng PDF.</summary>
    public class UploadContractAttachmentRequest
    {
        [Required]
        public Guid LoanContractId { get; set; }

        [Required]
        public IFormFile File { get; set; } = null!;

        public string? Note { get; set; }
    }
}
