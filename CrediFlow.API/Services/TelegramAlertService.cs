using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace CrediFlow.API.Services;

/// <summary>
/// Gửi thông báo lỗi API về Telegram — format đơn giản, dễ đọc
/// </summary>
public class TelegramAlertService
{
    private readonly HttpClient _http;
    private readonly string? _botToken;
    private readonly string? _chatId;
    private readonly bool _enabled;
    private readonly string _serviceName;

    public TelegramAlertService(IConfiguration config)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _botToken = config["Telegram:BotToken"];
        _chatId = config["Telegram:ChatId"];
        _enabled = config.GetValue("Telegram:Enabled", false)
                   && !string.IsNullOrWhiteSpace(_botToken)
                   && !string.IsNullOrWhiteSpace(_chatId);
        _serviceName = config["Serilog:Properties:service_name"] ?? "hdf-api";
    }

    public bool IsEnabled => _enabled;

    /// <summary>
    /// Gửi alert khi API gặp lỗi — text đơn giản, hiển thị API lỗi, curl, response
    /// </summary>
    public async Task SendErrorAlertAsync(
        string httpMethod,
        string requestPath,
        int statusCode,
        string? errorMessage,
        string? curlCommand,
        string? responseBody,
        string? clientIp = null)
    {
        if (!_enabled) return;

        try
        {
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            var sb = new StringBuilder();
            sb.AppendLine($"🔴 *API ERROR — {EscapeMd(_serviceName)}*");
            sb.AppendLine();
            sb.AppendLine($"🔗 `{httpMethod} {requestPath}`");
            sb.AppendLine($"📊 Status: *{statusCode}*");
            sb.AppendLine($"🕐 {now}");

            if (!string.IsNullOrWhiteSpace(clientIp))
                sb.AppendLine($"🌐 IP: `{EscapeMd(clientIp)}`");

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                var msg = Truncate(errorMessage, 500);
                sb.AppendLine();
                sb.AppendLine($"❌ *Error:*");
                sb.AppendLine($"```\n{msg}\n```");
            }

            if (!string.IsNullOrWhiteSpace(curlCommand))
            {
                var curl = Truncate(curlCommand, 800);
                sb.AppendLine();
                sb.AppendLine("🔄 *Curl:*");
                sb.AppendLine($"```\n{curl}\n```");
            }

            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                var resp = Truncate(responseBody, 500);
                sb.AppendLine();
                sb.AppendLine("📤 *Response:*");
                sb.AppendLine($"```json\n{resp}\n```");
            }

            var text = sb.ToString();

            // Truncate toàn bộ message nếu quá dài (Telegram limit 4096)
            if (text.Length > 4000) text = text[..4000] + "\n...(truncated)";

            var payload = JsonSerializer.Serialize(new
            {
                chat_id = _chatId,
                text,
                parse_mode = "Markdown",
                disable_web_page_preview = true
            });

            var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await _http.PostAsync(url, content);
        }
        catch
        {
            // Không throw — alert failure không được ảnh hưởng đến API
        }
    }

    private static string Truncate(string s, int maxLen)
        => s.Length <= maxLen ? s : s[..maxLen] + "...";

    private static string EscapeMd(string s)
        => s.Replace("_", "\\_").Replace("*", "\\*").Replace("[", "\\[").Replace("`", "\\`");
}
