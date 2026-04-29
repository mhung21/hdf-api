using CrediFlow.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace CrediFlow.Common.Services;

/// <summary>
/// Redis Stream Service implementation
/// Hỗ trợ Consumer Groups, ACK mechanism, horizontal scaling
/// </summary>
public class RedisStreamService : IRedisStreamService, IDisposable
{
    private readonly ILogger<RedisStreamService> _logger;
    private readonly RedisConfiguration _config;
    private readonly HashSet<string> _ensuredConsumerGroups = new();
    private readonly object _lock = new();
    private readonly Lazy<ConnectionMultiplexer> _lazyConnection;
    private bool _disposed = false;

    public RedisStreamService(
        ILogger<RedisStreamService> logger,
        IOptions<RedisConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;

        _lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
        {
            try
            {
                var options = ConfigurationOptions.Parse(_config.ConnectionString);

                options.AbortOnConnectFail = false;
                options.ConnectRetry = 3;
                options.ConnectTimeout = 5000;
                options.SyncTimeout = 5000;
                options.AsyncTimeout = 5000;
                options.KeepAlive = 60;
                options.AllowAdmin = false;

                var connection = ConnectionMultiplexer.Connect(options);

                connection.ConnectionFailed += (sender, args) =>
                {
                    _logger.LogError($"Redis connection failed: {args.EndPoint} - {args.FailureType}");
                };

                connection.ConnectionRestored += (sender, args) =>
                {
                    _logger.LogInformation($"Redis connection restored: {args.EndPoint}");
                };

                connection.ErrorMessage += (sender, args) =>
                {
                    // BUSYGROUP is not an error - it means consumer group already exists
                    if (args.Message.Contains("BUSYGROUP"))
                    {
                        _logger.LogDebug($"Redis info: {args.Message}");
                    }
                    else
                    {
                        _logger.LogError($"Redis error: {args.Message}");
                    }
                };

                _logger.LogInformation($"Redis Stream connection established to {_config.ConnectionString}");

                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Redis at {Endpoint}",
                    _config.ConnectionString);
                throw;
            }
        });
    }

    private IDatabase GetDatabase()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RedisStreamService));

        return _lazyConnection.Value.GetDatabase();
    }

    /// <summary>
    /// Publish message vào Redis Stream với XADD
    /// </summary>
    public async Task<string?> PublishAsync<T>(
        T data,
        string? streamKey = null,
        int? maxLength = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = streamKey ?? _config.StreamKey.WebhookRawStream;
            var db = GetDatabase();

            var json = JsonConvert.SerializeObject(data, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                DateTimeZoneHandling = DateTimeZoneHandling.Local
            });

            // Create name-value pairs for stream entry
            var fields = new NameValueEntry[]
            {
                new NameValueEntry("data", json),
                new NameValueEntry("timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds())
            };

            // XADD with auto-generated ID and optional MAXLEN trimming
            var messageId = await db.StreamAddAsync(
                key,
                fields,
                maxLength: maxLength ?? _config.MaxStreamLength,
                useApproximateMaxLength: true // Use ~ for better performance
            );

            _logger.LogDebug("Published to stream {StreamKey}, MessageId: {MessageId}",
                key, messageId);

            return messageId.ToString();
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis error publishing to stream {StreamKey}", streamKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing to stream {StreamKey}", streamKey);
            return null;
        }
    }

    /// <summary>
    /// Read messages từ stream using consumer group (XREADGROUP)
    /// </summary>
    public async Task<StreamEntry[]> ReadMessagesAsync(
        string streamKey,
        string consumerGroup,
        string consumerName,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = GetDatabase();

            // XREADGROUP GROUP group consumer [COUNT count] STREAMS key 0
            // "0" means get all unACKed messages from the beginning (including old pending messages)
            // ">" means get only NEW messages not yet delivered to any consumer
            var results = await db.StreamReadGroupAsync(
                streamKey,
                consumerGroup,
                consumerName,
                "0", // Get all unACKed messages (including backlog)
                count: count
            );

            _logger.LogDebug("Read {Count} messages from stream {StreamKey} by {Consumer}",
                results?.Length ?? 0, streamKey, consumerName);

            return results ?? Array.Empty<StreamEntry>();
        }
        catch (RedisServerException ex) when (ex.Message.Contains("NOGROUP"))
        {
            _logger.LogWarning("Consumer group {Group} doesn't exist for stream {Stream}. Creating...",
                consumerGroup, streamKey);

            await EnsureConsumerGroupAsync(streamKey, consumerGroup, cancellationToken);
            return Array.Empty<StreamEntry>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from stream {StreamKey}", streamKey);
            return Array.Empty<StreamEntry>();
        }
    }

    /// <summary>
    /// Acknowledge message (XACK)
    /// </summary>
    public async Task<bool> AcknowledgeAsync(
        string streamKey,
        string consumerGroup,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = GetDatabase();
            var acked = await db.StreamAcknowledgeAsync(streamKey, consumerGroup, messageId);

            if (acked > 0)
            {
                _logger.LogDebug("Acknowledged message {MessageId} from {StreamKey}",
                    messageId, streamKey);
            }

            return acked > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging message {MessageId}", messageId);
            return false;
        }
    }

    /// <summary>
    /// Acknowledge multiple messages
    /// </summary>
    public async Task<long> AcknowledgeMultipleAsync(
        string streamKey,
        string consumerGroup,
        string[] messageIds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = GetDatabase();
            var redisIds = messageIds.Select(id => (RedisValue)id).ToArray();
            var acked = await db.StreamAcknowledgeAsync(streamKey, consumerGroup, redisIds);

            _logger.LogDebug("Acknowledged {Count}/{Total} messages from {StreamKey}",
                acked, messageIds.Length, streamKey);

            return acked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging multiple messages");
            return 0;
        }
    }

    /// <summary>
    /// Get pending messages info (XPENDING)
    /// </summary>
    public async Task<StreamPendingInfo> GetPendingInfoAsync(
        string streamKey,
        string consumerGroup,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = GetDatabase();
            var pending = await db.StreamPendingAsync(streamKey, consumerGroup);
            return pending;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending info for {StreamKey}", streamKey);
            return new StreamPendingInfo();
        }
    }

    /// <summary>
    /// Claim pending messages that exceeded idle time (XCLAIM)
    /// Used for retry mechanism
    /// </summary>
    public async Task<StreamEntry[]> ClaimPendingMessagesAsync(
        string streamKey,
        string consumerGroup,
        string consumerName,
        long minIdleTimeMs,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = GetDatabase();

            // Get pending messages
            var pendingMessages = await db.StreamPendingMessagesAsync(
                streamKey,
                consumerGroup,
                count,
                RedisValue.Null // Any consumer
            );

            if (pendingMessages == null || pendingMessages.Length == 0)
                return Array.Empty<StreamEntry>();

            // Filter messages by idle time
            var messageIdsToClaim = pendingMessages
                .Where(p => p.IdleTimeInMilliseconds >= minIdleTimeMs)
                .Select(p => p.MessageId)
                .ToArray();

            if (messageIdsToClaim.Length == 0)
                return Array.Empty<StreamEntry>();

            // XCLAIM to take ownership
            var claimed = await db.StreamClaimAsync(
                streamKey,
                consumerGroup,
                consumerName,
                minIdleTimeMs,
                messageIdsToClaim
            );

            _logger.LogInformation("Claimed {Count} pending messages from {StreamKey}",
                claimed?.Length ?? 0, streamKey);

            return claimed ?? Array.Empty<StreamEntry>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error claiming pending messages from {StreamKey}", streamKey);
            return Array.Empty<StreamEntry>();
        }
    }

    /// <summary>
    /// Ensure consumer group exists (XGROUP CREATE)
    /// Cached to avoid repeated checks
    /// </summary>
    public async Task<bool> EnsureConsumerGroupAsync(
        string streamKey,
        string consumerGroup,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{streamKey}:{consumerGroup}";

        // Check cache first to avoid repeated Redis calls
        lock (_lock)
        {
            if (_ensuredConsumerGroups.Contains(cacheKey))
            {
                return true;
            }
        }

        try
        {
            var db = GetDatabase();

            // Create consumer group starting from beginning (0) or end ($)
            // Use $ for new streams to process only new messages
            // MKSTREAM option creates the stream if it doesn't exist
            await db.StreamCreateConsumerGroupAsync(streamKey, consumerGroup, "0", createStream: true);

            _logger.LogInformation("Created consumer group {Group} for stream {Stream}",
                consumerGroup, streamKey);

            // Cache successful creation
            lock (_lock)
            {
                _ensuredConsumerGroups.Add(cacheKey);
            }

            return true;
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists
            _logger.LogDebug("Consumer group {Group} already exists for {Stream}",
                consumerGroup, streamKey);

            lock (_lock)
            {
                _ensuredConsumerGroups.Add(cacheKey);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating consumer group {Group} for {Stream}",
                consumerGroup, streamKey);
            return false;
        }
    }

    /// <summary>
    /// Get stream length (XLEN)
    /// </summary>
    public async Task<long> GetStreamLengthAsync(
        string streamKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = GetDatabase();
            return await db.StreamLengthAsync(streamKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stream length for {StreamKey}", streamKey);
            return -1;
        }
    }

    public async Task<bool> IsConnectedAsync()
    {
        try
        {
            if (!_lazyConnection.IsValueCreated)
                return false;

            var db = GetDatabase();
            await db.PingAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public RedisConfiguration GetConfiguration()
    {
        return _config;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_lazyConnection.IsValueCreated)
        {
            _lazyConnection.Value?.Dispose();
            _logger.LogInformation("Redis Stream connection disposed");
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
