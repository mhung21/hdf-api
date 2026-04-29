using CrediFlow.Common.Models;
using StackExchange.Redis;

namespace CrediFlow.Common.Services;

/// <summary>
/// Service để làm việc với Redis Stream (thay thế Queue)
/// Hỗ trợ Consumer Groups, ACK mechanism, horizontal scaling
/// </summary>
public interface IRedisStreamService
{
    /// <summary>
    /// Publish message vào Redis Stream
    /// </summary>
    /// <param name="streamKey">Stream key (nếu null dùng default WebhookRawStream)</param>
    /// <param name="data">Data cần publish</param>
    /// <param name="maxLength">Max stream length (auto-trim), null = dùng config</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Message ID nếu thành công, null nếu failed</returns>
    Task<string?> PublishAsync<T>(
        T data, 
        string? streamKey = null, 
        int? maxLength = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read messages từ stream với consumer group
    /// </summary>
    /// <param name="streamKey">Stream key</param>
    /// <param name="consumerGroup">Consumer group name</param>
    /// <param name="consumerName">Consumer name (unique per instance)</param>
    /// <param name="count">Số lượng messages tối đa</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List messages</returns>
    Task<StreamEntry[]> ReadMessagesAsync(
        string streamKey,
        string consumerGroup,
        string consumerName,
        int count = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledge message đã xử lý thành công
    /// </summary>
    Task<bool> AcknowledgeAsync(
        string streamKey,
        string consumerGroup,
        string messageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledge nhiều messages
    /// </summary>
    Task<long> AcknowledgeMultipleAsync(
        string streamKey,
        string consumerGroup,
        string[] messageIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pending messages (đã read nhưng chưa ACK)
    /// </summary>
    Task<StreamPendingInfo> GetPendingInfoAsync(
        string streamKey,
        string consumerGroup,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Claim pending messages (retry timeout messages)
    /// </summary>
    Task<StreamEntry[]> ClaimPendingMessagesAsync(
        string streamKey,
        string consumerGroup,
        string consumerName,
        long minIdleTimeMs,
        int count = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo consumer group nếu chưa tồn tại
    /// </summary>
    Task<bool> EnsureConsumerGroupAsync(
        string streamKey,
        string consumerGroup,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get stream length
    /// </summary>
    Task<long> GetStreamLengthAsync(
        string streamKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check Redis connection
    /// </summary>
    Task<bool> IsConnectedAsync();
}
