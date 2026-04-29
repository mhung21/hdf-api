using Microsoft.Extensions.Caching.Distributed;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace CrediFlow.Common.Caching
{
    public interface ICachingHelper : IDisposable
    {
        T Get<T>(string key);

        T Get<T>(string key, Func<T> acquire, int? cacheTime = null);

        bool Set<T>(string key, T data, TimeSpan timeSpan = default(TimeSpan), bool IsSliding = false);

        bool IsSet(string key);

        void Remove(string key);

        void RemoveByPattern(string pattern);

        void Clear();
    }


    public class CachingHelper : ICachingHelper, IDisposable
    {
        private readonly IDistributedCache _distributedCache;

        private readonly IConfiguration _configuration;

        public CachingHelper(IDistributedCache distributedCache, IConfiguration configuration)
        {
            _distributedCache = distributedCache;
            _configuration = configuration;
        }

        public void RemoveCache(string cacheKey)
        {
            if (_configuration.GetValue("UseCache", defaultValue: false))
            {
                _distributedCache.Remove(cacheKey);
            }
        }

        public T Get<T>(string cacheKey)
        {
            if (!_configuration.GetValue("UseCache", defaultValue: false))
            {
                return default(T);
            }

            string @string = _distributedCache.GetString(cacheKey);
            if (string.IsNullOrWhiteSpace(@string))
            {
                return default(T);
            }

            if (typeof(T) == typeof(string))
            {
                return (T)Convert.ChangeType(@string, typeof(T));
            }

            return JsonConvert.DeserializeObject<T>(@string);
        }

        public T Get<T>(string key, Func<T> acquire, int? cacheTime = 5)
        {
            return default(T);
        }

        public bool Set<T>(string cacheKey, T data, TimeSpan timeSpan, bool IsSliding)
        {
            if (timeSpan == default(TimeSpan))
            {
                timeSpan = TimeSpan.FromMinutes(5.0);
            }

            try
            {
                if (!_configuration.GetValue("UseCache", defaultValue: false))
                {
                    return false;
                }

                if (typeof(T) == typeof(string))
                {
                    _distributedCache.SetString(cacheKey, data.ToString());
                }
                else
                {
                    string value = JsonConvert.SerializeObject(data, new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver(),
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    });
                    DistributedCacheEntryOptions options = new DistributedCacheEntryOptions().SetAbsoluteExpiration(timeSpan);
                    if (IsSliding)
                    {
                        options.SetSlidingExpiration(timeSpan);
                    }

                    _distributedCache.SetString(cacheKey, value, options);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Dispose()
        {
        }

        public bool IsSet(string key)
        {
            return true;
        }

        public void Remove(string key)
        {
            if (_configuration.GetValue("UseCache", defaultValue: false))
            {
                _distributedCache.Remove(key);
            }
        }

        public void RemoveByPattern(string pattern)
        {
            if (_configuration.GetValue("UseCache", defaultValue: false))
            {
                _distributedCache.Remove(pattern);
            }
        }

        public void Clear()
        {
        }
    }
}
