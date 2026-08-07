// -----------------------------------------------------------------------------
// 文件名: PublicIpDetector.cs
// 命名空间: io.NET.ZTR_OS.Features.NetworkMonitor.Services
// 功能描述: 公网 IP 检测（HTTP JSON + STUN UDP 双策略，互为兜底）
// 依赖组件: System.Net.Http, System.Net.Sockets（纯 BCL，不引入第三方）
// 设计模式: 策略模式（HttpJson + StunUdp），责任链（失败自动降级）
// -----------------------------------------------------------------------------
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace io.NET.ZTR_OS.Features.NetworkMonitor.Services;

/// <summary>
/// 公网 IP 检测结果
/// </summary>
public record PublicIpDetectionResult(
    bool Success,
    string? IpAddress = null,
    string? Strategy = null,
    string? ErrorMessage = null,
    TimeSpan? Elapsed = null);

/// <summary>
/// 公网 IP 检测器
/// </summary>
/// <remarks>
/// 策略说明（与 README 声明的 HTTP+STUN 双策略对齐）：
/// <list type="bullet">
/// <item><b>HttpJson</b>：依次请求 ip-api.com / ipify.org / ifconfig.me，
///   兼容 {ip:'x'}、{query:'x'}、纯文本 三种响应格式</item>
/// <item><b>StunUdp</b>：通过 RFC 5389 STUN Binding Request 发往
///   stun.l.google.com:19302 / stun.syncthing.net:3478，
///   从 XOR-MAPPED-ADDRESS 属性中解析公网地址（应对纯 HTTP 被 NAPT 劫持的场景）</item>
/// </list>
/// </remarks>
public class PublicIpDetector
{
    /// <summary>
    /// 可用的检测策略名列表（用于 README 契约断言 & 前端 UI 展示使用了哪种策略）
    /// </summary>
    public string[] AvailableStrategies { get; } = { "HttpJson", "StunUdp" };

    // ─── HTTP 提供商列表（按优先级排序）───
    private static readonly (string Url, int TimeoutMs)[] _httpProviders = new (string, int)[]
    {
        ("https://api.ipify.org?format=json",            3000),
        ("http://ip-api.com/json/?fields=query,status",  4000),
        ("https://ifconfig.me/ip",                       4000),
    };

    // ─── STUN 服务器列表（按优先级排序）───
    private static readonly (string Host, int Port, int TimeoutMs)[] _stunServers = new (string, int, int)[]
    {
        ("stun.l.google.com",    19302, 3500),
        ("stun.syncthing.net",   3478,  4000),
        ("stun.qq.com",          3478,  4000),
    };

    // IPv4 正则（用于从 JSON / 纯文本中提取）
    private static readonly Regex _ipv4Regex = new(
        pattern: @"\b(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\." +
                 @"(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\." +
                 @"(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\." +
                 @"(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\b",
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 从任意 JSON / 纯文本响应中提取 IPv4
    /// </summary>
    /// <param name="responseBody">HTTP 响应内容（JSON 或纯文本都可以）</param>
    /// <returns>合法的 IPv4 字符串；解析失败 / 非法 IP 返回 null</returns>
    public static string? TryParseIPv4FromJson(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        var m = _ipv4Regex.Match(responseBody.Trim());
        if (!m.Success) return null;

        var candidate = m.Value;
        // 额外校验：四段 [0-255]
        var parts = candidate.Split('.');
        if (parts.Length != 4) return null;
        foreach (var p in parts)
        {
            if (!byte.TryParse(p, out var b)) return null;
        }
        return candidate;
    }

    /// <summary>
    /// 并发执行 HttpJson 和 StunUdp 两种策略，返回最快成功的一个
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检测结果</returns>
    public async Task<PublicIpDetectionResult> DetectFastestAsync(
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // 并发发起两个策略
        var httpTask = DetectByHttpJsonAsync(cancellationToken);
        var stunTask = DetectByStunAsync(cancellationToken);

        // 等任意一个成功
        var completed = await Task.WhenAny(httpTask, stunTask);
        var first = completed.Result;

        if (first.Success)
        {
            sw.Stop();
            return first with { Elapsed = sw.Elapsed };
        }

        // 第一个失败了，等另一个
        var second = completed == httpTask ? await stunTask : await httpTask;
        sw.Stop();
        if (second.Success)
            return second with { Elapsed = sw.Elapsed };

        // 两个都失败：把两个错误拼起来给用户看
        return new PublicIpDetectionResult(
            Success: false,
            Strategy: $"{first.Strategy}+{second.Strategy}",
            ErrorMessage: $"HTTP失败: {first.ErrorMessage}；STUN失败: {second.ErrorMessage}",
            Elapsed: sw.Elapsed);
    }

    /// <summary>
    /// HTTP JSON 策略检测
    /// </summary>
    public async Task<PublicIpDetectionResult> DetectByHttpJsonAsync(
        CancellationToken cancellationToken = default)
    {
        // 每个 Provider 单独发请求，不共用 HttpClient（不依赖 DI 注册）
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        foreach (var (url, timeoutMs) in _httpProviders)
        {
            try
            {
                cts.CancelAfter(timeoutMs);
                using var client = new HttpClient();
                var body = await client.GetStringAsync(url, cts.Token).ConfigureAwait(false);
                var ip = TryParseIPv4FromJson(body);
                if (ip != null)
                {
                    return new PublicIpDetectionResult(
                        Success: true,
                        IpAddress: ip,
                        Strategy: "HttpJson");
                }
                Log.Warning("[PubIP] HTTP {Url} 返回无法解析 IP: {Body}",
                    url, body.Length > 200 ? body[..200] : body);
            }
            catch (Exception ex)
            {
                Log.Debug("[PubIP] HTTP {Url} 失败: {Msg}", url, ex.Message);
            }
            finally
            {
                // 防止 CancelAfter 影响下一个循环
                try { cts.CancelAfter(Timeout.Infinite); } catch { }
            }
        }

        return new PublicIpDetectionResult(
            Success: false,
            Strategy: "HttpJson",
            ErrorMessage: "所有 HTTP 提供商均请求失败或返回非法响应");
    }

    /// <summary>
    /// STUN UDP 策略检测（RFC 5389 Binding Request）
    /// </summary>
    public async Task<PublicIpDetectionResult> DetectByStunAsync(
        CancellationToken cancellationToken = default)
    {
        foreach (var (host, port, timeoutMs) in _stunServers)
        {
            try
            {
                var ip = await StunBindingRequestAsync(host, port, timeoutMs, cancellationToken)
                    .ConfigureAwait(false);
                if (ip != null)
                {
                    return new PublicIpDetectionResult(
                        Success: true,
                        IpAddress: ip,
                        Strategy: "StunUdp");
                }
            }
            catch (Exception ex)
            {
                Log.Debug("[PubIP] STUN {Host}:{Port} 失败: {Msg}", host, port, ex.Message);
            }
        }

        return new PublicIpDetectionResult(
            Success: false,
            Strategy: "StunUdp",
            ErrorMessage: "所有 STUN 服务器均请求失败或无法解析 XOR-MAPPED-ADDRESS");
    }

    // ─────────── STUN Binding Request 最小实现（不引入 STUN 库）────────────

    // STUN 头：2B Type + 2B Length + 4B Cookie(0x2112A442) + 12B TransactionID
    private const int StunHeaderSize = 20;
    private const ushort StunBindingRequest = 0x0001;
    private const uint StunMagicCookie = 0x2112A442;

    // XOR-MAPPED-ADDRESS 属性类型（RFC 5389 §15.2）
    private const ushort AttrXorMappedAddress = 0x0020;

    private static async Task<string?> StunBindingRequestAsync(
        string host,
        int port,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        // 解析服务器 IP（支持域名）
        IPAddress[] addrs;
        try
        {
            addrs = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        }
        catch { return null; }

        if (addrs.Length == 0) return null;
        var serverEp = new IPEndPoint(addrs[0], port);

        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.SendTimeout = timeoutMs;
        udp.Client.ReceiveTimeout = timeoutMs;

        // 构造 Binding Request
        var txId = new byte[12];
        Random.Shared.NextBytes(txId);

        var request = new byte[StunHeaderSize];
        // Type (BE)
        request[0] = (byte)(StunBindingRequest >> 8);
        request[1] = (byte)(StunBindingRequest & 0xFF);
        // Length = 0（无属性）
        request[2] = 0; request[3] = 0;
        // Magic Cookie (BE)
        request[4] = unchecked((byte)(StunMagicCookie >> 24));
        request[5] = unchecked((byte)(StunMagicCookie >> 16));
        request[6] = unchecked((byte)(StunMagicCookie >> 8));
        request[7] = unchecked((byte)(StunMagicCookie & 0xFF));
        // TransactionID
        Buffer.BlockCopy(txId, 0, request, 8, 12);

        // 发请求
        await udp.SendAsync(request, request.Length, serverEp).ConfigureAwait(false);

        // 等响应（带超时）
        using var recvCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        recvCts.CancelAfter(timeoutMs);
        try
        {
            var result = await udp.ReceiveAsync(recvCts.Token).ConfigureAwait(false);
            return ParseStunXorMappedAddress(result.Buffer, txId);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static string? ParseStunXorMappedAddress(byte[] buffer, byte[] txId)
    {
        if (buffer.Length < StunHeaderSize) return null;

        // 校验响应类型 = 0x0101 (BindingSuccessResponse)
        if (buffer[0] != 0x01 || buffer[1] != 0x01) return null;

        int msgLen = (buffer[2] << 8) | buffer[3];
        // 校验 Cookie
        if (buffer[4] != 0x21 || buffer[5] != 0x12 || buffer[6] != 0xA4 || buffer[7] != 0x42)
            return null;
        // 校验 TransactionID（后 12B）
        for (int i = 0; i < 12; i++)
            if (buffer[8 + i] != txId[i]) return null;

        // 遍历属性，找 XOR-MAPPED-ADDRESS
        int pos = StunHeaderSize;
        int end = Math.Min(StunHeaderSize + msgLen, buffer.Length);
        while (pos + 4 <= end)
        {
            ushort attrType = (ushort)((buffer[pos] << 8) | buffer[pos + 1]);
            int attrLen = (buffer[pos + 2] << 8) | buffer[pos + 3];
            int valueOffset = pos + 4;
            if (attrType == AttrXorMappedAddress && valueOffset + attrLen <= end)
            {
                // RFC 5389 §15.2:
                // 0-1: Reserved + Family (0x01 = IPv4, 0x02 = IPv6)
                // 2-3: X-Port
                // 4+: X-Address (IPv4 = 4B, IPv6 = 16B)
                if (attrLen < 8) return null;
                byte family = buffer[valueOffset + 1];
                if (family != 0x01) return null; // 本实现只处理 IPv4

                ushort xPort = (ushort)((buffer[valueOffset + 2] << 8) | buffer[valueOffset + 3]);
                // X-Port ^ Cookie高16位 -> 实际端口（其实 IP 更重要，忽略端口）
                _ = (ushort)(xPort ^ (StunMagicCookie >> 16));

                // X-Address (4B) ^ Cookie (4B)
                uint xAddr = (uint)(
                    (buffer[valueOffset + 4] << 24) |
                    (buffer[valueOffset + 5] << 16) |
                    (buffer[valueOffset + 6] << 8) |
                    buffer[valueOffset + 7]);
                uint realAddr = xAddr ^ StunMagicCookie;
                var ipBytes = new[]
                {
                    unchecked((byte)(realAddr >> 24)),
                    unchecked((byte)(realAddr >> 16)),
                    unchecked((byte)(realAddr >> 8)),
                    (byte)(realAddr & 0xFF),
                };
                return new IPAddress(ipBytes).ToString();
            }
            // 跳过属性：按 4B 对齐
            pos += 4 + ((attrLen + 3) & ~3);
        }
        return null;
    }
}
