using CrediFlow.Common.Models;

namespace CrediFlow.Common.Services;

/// <summary>
/// Service để làm việc với Redis Queue
/// </summary>
public interface IRedisQueueService
{
    /// <summary>
    /// Đẩy data vào Redis queue
    /// </summary>
    /// <typeparam name="T">Type của data</typeparam>
    /// <param name="data">Data cần đẩy vào queue</param>
    /// <param name="queueKey">Redis key của queue (nếu null sẽ dùng default WebhookQueue)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True nếu thành công</returns>
    Task<bool> EnqueueAsync<T>(T data, string? queueKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy số lượng items trong queue
    /// </summary>
    /// <param name="queueKey">Redis key của queue (nếu null sẽ dùng default WebhookQueue)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Số lượng items trong queue, -1 nếu có lỗi</returns>
    Task<long> GetQueueLengthAsync(string? queueKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra kết nối Redis
    /// </summary>
    /// <returns>True nếu connected</returns>
    Task<bool> IsConnectedAsync();
}
