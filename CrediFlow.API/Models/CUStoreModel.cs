using System.ComponentModel.DataAnnotations;

namespace CrediFlow.API.Models
{
    /// <summary>Model tạo mới / cập nhật chi nhánh.</summary>
    public class CUStoreModel
    {
        /// <summary>Id chi nhánh – null khi tạo mới, có giá trị khi cập nhật.</summary>
        public Guid? StoreId { get; set; }

        [Required]
        public string StoreCode { get; set; } = null!;

        [Required]
        public string StoreName { get; set; } = null!;

        public string? Address { get; set; }
        public string? Phone   { get; set; }
        public DateOnly? OpenedOn { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
