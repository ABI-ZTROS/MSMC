using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace McServerGuard.Services;

public class TimeService
{
    private static readonly ILogger Log = Serilog.Log.ForContext<TimeService>();

    private static readonly string[] NtpServers =
    {
        "ntp.ntsc.ac.cn",
        "cn.ntp.org.cn",
        "ntp.aliyun.com",
        "time.windows.com",
    };

    private const int NtpPort = 123;
    private const int NtpTimeoutMs = 3000;
    private static readonly TimeSpan ResyncInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan LargeClockOffsetThreshold = TimeSpan.FromSeconds(5);

    private readonly object _lock = new();
    private long _clockOffsetMs;
    private bool _isSynchronized;
    private DateTime _lastSyncTime = DateTime.MinValue;
    private Timer? _resyncTimer;

    public bool IsSynchronized
    {
        get { lock (_lock) return _isSynchronized; }
    }

    public TimeSpan ClockOffset
    {
        get { lock (_lock) return TimeSpan.FromMilliseconds(_clockOffsetMs); }
    }

    public DateTime Now => DateTime.Now.AddMilliseconds(Volatile.Read(ref _clockOffsetMs));

    public long NowUnixMilliseconds => new DateTimeOffset(Now).ToUnixTimeMilliseconds();

    public event EventHandler? SynchronizationCompleted;

    public async Task<bool> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var offsets = new List<long>();
        var successful = 0;

        foreach (var server in NtpServers)
        {
            try
            {
                var offset = await QueryNtpOffsetAsync(server, cancellationToken);
                offsets.Add(offset);
                successful++;
                Log.Debug("NTP 服务器 {Server} 偏移: {Offset}ms", server, offset);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "NTP 服务器 {Server} 查询失败", server);
            }

            if (successful >= 2)
                break;
        }

        if (offsets.Count == 0)
        {
            Log.Warning("所有 NTP 服务器均不可达，使用本地时间");
            lock (_lock)
            {
                _isSynchronized = false;
                _clockOffsetMs = 0;
                _lastSyncTime = DateTime.Now;
            }
            OnSynchronizationCompleted();
            return false;
        }

        offsets.Sort();
        var medianOffset = offsets[offsets.Count / 2];

        lock (_lock)
        {
            _clockOffsetMs = medianOffset;
            _isSynchronized = true;
            _lastSyncTime = DateTime.Now;
        }

        if (Math.Abs(medianOffset) > LargeClockOffsetThreshold.TotalMilliseconds)
        {
            Log.Warning("系统时钟与 NTP 标准时间偏差较大: {Offset}ms，请检查系统时间设置", medianOffset);
        }
        else
        {
            Log.Information("⏰ NTP 时间同步完成，偏差 {Offset}ms（成功查询 {Count} 个服务器）", medianOffset, offsets.Count);
        }

        OnSynchronizationCompleted();
        StartResyncTimer();
        return true;
    }

    private static async Task<long> QueryNtpOffsetAsync(string server, CancellationToken cancellationToken)
    {
        var ntpData = new byte[48];
        ntpData[0] = 0x1B;

        using var udpClient = new UdpClient();
        udpClient.Client.ReceiveTimeout = NtpTimeoutMs;

        var sendTime = DateTime.UtcNow;
        await udpClient.SendAsync(ntpData, ntpData.Length, server, NtpPort);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(NtpTimeoutMs);

        var receiveResult = await udpClient.ReceiveAsync(cts.Token);
        var receiveTime = DateTime.UtcNow;

        var buffer = receiveResult.Buffer;

        var transmitTimestamp = ParseNtpTimestamp(buffer, 40);

        var roundTrip = (receiveTime - sendTime).TotalMilliseconds;
        var offset = (transmitTimestamp - sendTime).TotalMilliseconds - roundTrip / 2;

        return (long)offset;
    }

    private static DateTime ParseNtpTimestamp(byte[] buffer, int offset)
    {
        var seconds = (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
        var fraction = (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset + 4));

        var milliseconds = seconds * 1000 + (double)fraction * 1000 / 0x100000000L;

        return new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(milliseconds);
    }

    private void StartResyncTimer()
    {
        if (_resyncTimer != null)
            return;

        _resyncTimer = new Timer(
            async _ =>
            {
                try
                {
                    await SynchronizeAsync();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "NTP 重新同步失败");
                }
            },
            null,
            ResyncInterval,
            ResyncInterval);
    }

    private void OnSynchronizationCompleted()
    {
        SynchronizationCompleted?.Invoke(this, EventArgs.Empty);
    }

    public DateTime ToBeijingTime(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Utc)
            return dateTime.AddHours(8);

        if (dateTime.Kind == DateTimeKind.Local)
            return dateTime.ToUniversalTime().AddHours(8);

        return dateTime;
    }

    public long ToUnixTimeMilliseconds(DateTime beijingTime)
    {
        var utcTime = beijingTime.AddHours(-8);
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (long)(utcTime - epoch).TotalMilliseconds;
    }

    public DateTime FromUnixTimeMilliseconds(long unixMs)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var utcTime = epoch.AddMilliseconds(unixMs);
        return utcTime.AddHours(8);
    }

    public DateOnly Today => DateOnly.FromDateTime(Now);
}
