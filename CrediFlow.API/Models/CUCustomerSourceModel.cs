using System.ComponentModel.DataAnnotations;

namespace CrediFlow.API.Models
{
    /// <summary>Model tạo mới / cập nhật luồng khách.</summary>
    public class CUCustomerSourceModel
    {
        /// <summary>Id – null khi tạo mới, có giá trị khi cập nhật.</summary>
        public Guid? SourceId { get; set; }

        [Required]
        [MaxLength(100)]
        public string SourceName { get; set; } = null!;

        public bool IsActive  { get; set; } = true;
        public int  SortOrder { get; set; } = 0;
    }
}
