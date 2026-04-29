using System.ComponentModel.DataAnnotations;

namespace CrediFlow.API.Models
{
    /// <summary>Model tạo mới / cập nhật lượt đến của khách hàng.</summary>
    public class CUCustomerVisitModel
    {
        /// <summary>Id lượt đến – null khi tạo mới, có giá trị khi cập nhật.</summary>
        public Guid? VisitId { get; set; }

        [Required]
        public Guid StoreId { get; set; }

        [Required]
        public Guid CustomerId { get; set; }

        public DateOnly VisitDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        /// <summary>Loại lượt: NEW | OLD</summary>
        [Required]
        public string VisitType { get; set; } = null!;

        /// <summary>Nguồn: CTV | VANG_LAI (bắt buộc khi VisitType = NEW).</summary>
        public string? SourceType { get; set; }

        public Guid?   HandledBy { get; set; }
        public string? Note      { get; set; }
    }
}
