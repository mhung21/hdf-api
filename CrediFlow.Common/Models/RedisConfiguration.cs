namespace CrediFlow.Common.Models;

/// <summary>
/// Cấu hình Redis
/// Map với section "Redis" trong appsettings.json
/// </summary>
public class RedisConfiguration
{
    /// <summary>
    /// Connection string cho Redis
    /// Format: "host:port,abortConnect=false,connectTimeout=5000,..."
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// Các stream keys
    /// </summary>
    public RedisStreamKeys StreamKey { get; set; } = new();

    /// <summary>
    /// Consumer group names
    /// </summary>
    public RedisConsumerGroups ConsumerGroup { get; set; } = new();

    /// <summary>
    /// Số lượng items trong batch để trigger processing
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Thời gian timeout (seconds) để trigger processing batch
    /// </summary>
    public int BatchTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Thời gian tối đa message được pending trước khi auto-claim (milliseconds)
    /// </summary>
    public int MessageMaxIdleTimeMs { get; set; } = 60000; // 1 minute

    /// <summary>
    /// Số lượng message tối đa trong stream (auto-trim)
    /// </summary>
    public int MaxStreamLength { get; set; } = 100000;
}

/// <summary>
/// Định nghĩa các Redis Stream keys
/// </summary>
public class RedisStreamKeys
{
    /// <summary>
    /// Stream cho raw webhook data (input từ API)
    /// </summary>
    public string WebhookRawStream { get; set; } = "";

    /// <summary>
    /// Stream cho processed data (sau khi processing workers xử lý)
    /// </summary>
    public string WebhookProcessedStream { get; set; } = "";
}

/// <summary>
/// Consumer group names
/// </summary>
public class RedisConsumerGroups
{
    /// <summary>
    /// Consumer group cho processing workers (parse, validate, compute)
    /// </summary>
    public string ProcessingWorkers { get; set; } = "";

    /// <summary>
    /// Consumer group cho DB writer workers (batch insert)
    /// </summary>
    public string DbWriterWorkers { get; set; } = "";
}
