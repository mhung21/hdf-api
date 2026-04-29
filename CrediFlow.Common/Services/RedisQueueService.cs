using CrediFlow.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace CrediFlow.Common.Services;

/// <summary>
/// Implementation của IRedisQueueService sử dụng StackExchange.Redis
/// Thread-safe và sử dụng connection pooling
/// </summary>
public class RedisQueueService : IRedisQueueService, IDisposable
{
    private readonly ILogger<RedisQueueService> _logger;
    private readonly RedisConfiguration _config;
    private readonly Lazy<ConnectionMultiplexer> _lazyConnection;
    private bool _disposed = false;

    public RedisQueueService(
        ILogger<RedisQueueService> logger,
        IOptions<RedisConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;

        _lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
        {
            try
            {
                var options = ConfigurationOptions.Parse(_config.ConnectionString);
                
                // Đảm bảo các settings quan trọng
                options.AbortOnConnectFail = false; // Không abort khi connect fail, sẽ retry
                options.ConnectRetry = 3;
                options.ConnectTimeout = 5000;
                options.SyncTimeout = 5000;
                options.AsyncTimeout = 5000;
                options.KeepAlive = 60;
                options.AllowAdmin = false; // Security best practice

                var connection = ConnectionMultiplexer.Connect(options);

                // Event handlers để logging
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
                    _logger.LogError($"Redis error message: {args.Message}");
                };

                _logger.LogInformation($"Redis connection established successfully to {_config.ConnectionString}");
                    
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Redis at {Endpoint}", _config.ConnectionString);
                throw;
            }
        });
    }

    private IDatabase GetDatabase()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RedisQueueService));

        return _lazyConnection.Value.GetDatabase();
    }

    public async Task<bool> EnqueueAsync<T>(T data, string queueKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = queueKey;
            var db = GetDatabase();
            
            var json = JsonConvert.SerializeObject(data, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                DateTimeZoneHandling = DateTimeZoneHandling.Local
            });

            // LPUSH để push vào đầu list, worker sẽ RPOP từ cuối (FIFO)
            var queueLength = await db.ListLeftPushAsync(key, json);

            _logger.LogDebug($"Enqueued data to Redis queue {key}. Queue length: {queueLength}");

            return true;
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, $"Redis error while enqueueing data to {queueKey}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error while enqueueing data to {queueKey}");
            return false;
        }
    }

    public async Task<long> GetQueueLengthAsync(string queueKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = queueKey;
            var db = GetDatabase();
            return await db.ListLengthAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting queue length for {queueKey}");
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
            _logger.LogInformation("Redis connection disposed");
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
