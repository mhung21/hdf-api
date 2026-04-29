namespace CrediFlow.API.Models
{
    public class ConfigRoot
    {
        public static Config Config { get; set; } = new Config();
    }

    public class Config
    {
        public static ConnectionStrings ConnectionStrings { get; set; } = new();
        public static Urls Urls { get; set; } = new();
        public static JwtSettings JwtSettings { get; set; } = new();
        public static FileStorageSettings FileStorage { get; set; } = new();
        public static EncryptionSettings Encryption { get; set; } = new();
    }

    public class Urls
    {
        public string IdentityServer { get; set; }
    }

    public class ConnectionStrings
    {
        public string CrediFlowConnection { get; set; }
        public string CrediFlowIdentityDB { get; set; }
        public string HangfireDB { get; set; }
        public string RedisDB { get; set; }
    }

    public class JwtSettings
    {
        public string Audience { get; set; }
        public string ClientId { get; set; }
        public string Issuer { get; set; }
        public string Key { get; set; }
    }

    /// <summary>Cấu hình lưu trữ file hợp đồng PDF (section "FileStorage" trong appsettings.json).</summary>
    public class FileStorageSettings
    {
        /// <summary>Thư mục gốc lưu file, ngoài wwwroot. Ví dụ: D:\\CrediFlowStorage\\contracts</summary>
        public string BasePath { get; set; } = string.Empty;

        /// <summary>Kích thước file tối đa tính bằng bytes. Mặc định 20 MB.</summary>
        public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024;
    }

    /// <summary>Cấu hình mã hóa dữ liệu cá nhân nhạy cảm (section "Encryption" trong appsettings.json).</summary>
    public class EncryptionSettings
    {
        /// <summary>AES-256 key dạng Base64 (32 bytes). Sinh bằng: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))</summary>
        public string NationalIdKey { get; set; } = string.Empty;
    }

}