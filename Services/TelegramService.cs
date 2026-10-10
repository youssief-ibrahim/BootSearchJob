
using System.Net.Http.Json;

namespace DotNetJobHunter.Services;

public class TelegramService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramService> _logger;

    public TelegramService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TelegramService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendMessageAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        var token = _configuration["Telegram:BotToken"];
        var chatId = _configuration["Telegram:ChatId"];

        if (string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(chatId))
        {
            throw new InvalidOperationException(
                "Telegram settings are missing.");
        }

        var url = $"https://api.telegram.org/bot{token}/sendMessage";

        var response = await _httpClient.PostAsJsonAsync(
            url,
            new
            {
                chat_id = chatId,
                text = message,
                disable_web_page_preview = false
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Telegram message sent successfully.");
    }
}
