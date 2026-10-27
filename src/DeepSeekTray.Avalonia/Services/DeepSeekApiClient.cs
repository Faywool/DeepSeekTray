using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeepSeekTray.Localization;
using DeepSeekTray.Models;

namespace DeepSeekTray.Services;

/// <summary>API Key 被服务端拒绝（401/403）。</summary>
public sealed class ApiKeyRejectedException : Exception
{
    public ApiKeyRejectedException(string message) : base(message)
    {
    }
}

/// <summary>网络 / 服务端异常，不代表 Key 失效。</summary>
public sealed class BalanceRequestException : Exception
{
    public BalanceRequestException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>
/// DeepSeek 官方余额接口客户端。
/// 文档：GET https://api.deepseek.com/user/balance
/// 只返回「当前余额构成」，没有累计消费字段。
/// </summary>
public sealed class DeepSeekApiClient
{
    public const string BaseUrl = "https://api.deepseek.com";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(15),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DeepSeekTray/1.0");
        return client;
    }

    public async Task<BalanceSnapshot> GetBalanceAsync(string apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiKeyRejectedException(Loc.T("Api.EmptyKey"));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "user/balance");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BalanceRequestException(Loc.T("Api.Timeout"), ex);
        }
        catch (HttpRequestException ex)
        {
            throw new BalanceRequestException(Loc.F("Api.Network", ex.Message), ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new ApiKeyRejectedException(Loc.T("Api.InvalidKey"));
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new BalanceRequestException(Loc.F("Api.Status", (int)response.StatusCode, Truncate(body, 200)));
            }

            try
            {
                return Parse(body);
            }
            catch (JsonException ex)
            {
                throw new BalanceRequestException(Loc.F("Api.BadJson", Truncate(body, 200)), ex);
            }
        }
    }

    /// <summary>解析 /user/balance 响应；多币种时优先 CNY。</summary>
    internal static BalanceSnapshot Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var available = root.TryGetProperty("is_available", out var availableElement)
                        && availableElement.ValueKind == JsonValueKind.True;

        var currency = "CNY";
        decimal total = 0m;
        decimal granted = 0m;
        decimal toppedUp = 0m;

        if (root.TryGetProperty("balance_infos", out var infos) && infos.ValueKind == JsonValueKind.Array)
        {
            JsonElement chosen = default;
            var found = false;

            foreach (var info in infos.EnumerateArray())
            {
                var current = ReadString(info, "currency") ?? currency;
                var preferCny = string.Equals(current, "CNY", StringComparison.OrdinalIgnoreCase);
                var currentIsCny = string.Equals(currency, "CNY", StringComparison.OrdinalIgnoreCase);

                if (!found || (preferCny && !currentIsCny))
                {
                    chosen = info;
                    currency = current;
                    found = true;
                }
            }

            if (found)
            {
                total = ReadDecimal(chosen, "total_balance");
                granted = ReadDecimal(chosen, "granted_balance");
                toppedUp = ReadDecimal(chosen, "topped_up_balance");
            }
            else
            {
                // 没有余额条目时不要伪造成 0 元（会误报“余额耗尽”）
                throw new BalanceRequestException(Loc.T("Api.NoInfos"));
            }
        }
        else
        {
            throw new BalanceRequestException(Loc.T("Api.NoInfos"));
        }

        return new BalanceSnapshot
        {
            IsAvailable = available,
            Currency = currency,
            Total = total,
            Granted = granted,
            ToppedUp = toppedUp,
            FetchedAt = DateTimeOffset.Now,
        };
    }

    private static string? ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>官方字段是字符串金额，统一按 InvariantCulture 解析为 decimal。</summary>
    private static decimal ReadDecimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return 0m;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0m;
    }

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var single = text.Replace("\r", " ").Replace("\n", " ");
        return single.Length <= max ? single : single.Substring(0, max) + "…";
    }
}
