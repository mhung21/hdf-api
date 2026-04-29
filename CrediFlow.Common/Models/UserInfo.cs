namespace CrediFlow.Common.Models
{
    public class UserInfo
    {
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string? Phone { get; set; }
        public string? RoleCode { get; set; }
        public Guid? StoreId { get; set; }
        public bool IsActive { get; set; }
        public bool? IsAdmin { get; set; }
        public bool? IsStoreManager { get; set; }
        public bool? IsRegionalManager { get; set; }
        public List<Guid>? AssignedStoreIds { get; set; }
        public bool? Status { get; set; }
        public List<string> Permission { get; set; }
    }
}
