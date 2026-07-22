namespace McServerGuard.Services.Network;

using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

/// <summary>
/// 每日流量记录（按小时分桶）。
/// </summary>
public class DailyTrafficRecord
{
    public DateTime Date { get; set; }
    public long[] HourlyUpload { get; set; } = new long[24];
    public long[] HourlyDownload { get; set; } = new long[24];

    [JsonIgnore]
    public long TotalUpload => HourlyUpload.Sum();

    [JsonIgnore]
    public long TotalDownload => HourlyDownload.Sum();
}

/// <summary>
/// 网络流量监控服务。
/// 通过 System.Net.NetworkInformation 采样网卡收发字节数，
/// 计算实时上传/下载速度，按小时累积每日流量，持久化到 JSON 文件。
/// </summary>
public class NetworkTrafficService : IDisposable
{
    private readonly string _dataFilePath;
    private readonly List<DailyTrafficRecord> _history = [];

    private long _lastBytesSent;
    private long _lastBytesReceived;
    private DateTime _lastSampleTime;
    private bool _firstSample = true;

    private double _currentUploadSpeed;
    private double _currentDownloadSpeed;

    private DateTime _lastSaveTime;
    private readonly TimeSpan _saveInterval = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public double CurrentUploadSpeed => _currentUploadSpeed;
    public double CurrentDownloadSpeed => _currentDownloadSpeed;

    public DailyTrafficRecord TodayTraffic =>
        _history.FirstOrDefault(r => r.Date == DateTime.Today)
        ?? new DailyTrafficRecord { Date = DateTime.Today };

    public NetworkTrafficService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "McServerGuard");
        Directory.CreateDirectory(dir);
        _dataFilePath = Path.Combine(dir, "traffic.json");

        Load();
        EnsureTodayRecord();
    }

    private void EnsureTodayRecord()
    {
        if (!_history.Any(r => r.Date == DateTime.Today))
        {
            _history.Add(new DailyTrafficRecord { Date = DateTime.Today });
        }
    }

    /// <summary>
    /// 采样一次：读取网卡字节 → 计算速度 → 累积到当前小时桶。
    /// 应每秒调用一次。
    /// </summary>
    public void Sample()
    {
        try
        {
            var (bytesSent, bytesReceived) = GetTotalBytes();
            var now = DateTime.Now;

            if (_firstSample)
            {
                _lastBytesSent = bytesSent;
                _lastBytesReceived = bytesReceived;
                _lastSampleTime = now;
                _firstSample = false;
                return;
            }

            var elapsed = (now - _lastSampleTime).TotalSeconds;
            if (elapsed < 0.1)
                return;

            var sentDelta = bytesSent - _lastBytesSent;
            var receivedDelta = bytesReceived - _lastBytesReceived;

            // 网卡字节计数器可能溢出或重置，delta 为负时忽略
            if (sentDelta < 0) sentDelta = 0;
            if (receivedDelta < 0) receivedDelta = 0;

            _currentUploadSpeed = sentDelta / elapsed;
            _currentDownloadSpeed = receivedDelta / elapsed;

            // 累积到当前小时桶
            EnsureTodayRecord();
            var today = _history.First(r => r.Date == DateTime.Today);
            var hour = now.Hour;
            if (hour is >= 0 and < 24)
            {
                today.HourlyUpload[hour] += sentDelta;
                today.HourlyDownload[hour] += receivedDelta;
            }

            _lastBytesSent = bytesSent;
            _lastBytesReceived = bytesReceived;
            _lastSampleTime = now;

            // 定期保存
            if (now - _lastSaveTime > _saveInterval)
            {
                Save();
                _lastSaveTime = now;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "网络流量采样失败");
        }
    }

    /// <summary>
    /// 获取今日流量记录（含 24 小时数据）。
    /// </summary>
    public DailyTrafficRecord GetTodayTraffic()
    {
        EnsureTodayRecord();
        return _history.First(r => r.Date == DateTime.Today);
    }

    /// <summary>
    /// 获取最近 N 天的流量记录。
    /// </summary>
    public List<DailyTrafficRecord> GetRecentDays(int days)
    {
        var cutoff = DateTime.Today.AddDays(-days + 1);
        return _history.Where(r => r.Date >= cutoff).OrderBy(r => r.Date).ToList();
    }

    private static (long sent, long received) GetTotalBytes()
    {
        long totalSent = 0;
        long totalReceived = 0;

        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                      && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        foreach (var ni in interfaces)
        {
            try
            {
                var stats = ni.GetIPv4Statistics();
                totalSent += stats.BytesSent;
                totalReceived += stats.BytesReceived;
            }
            catch
            {
                // 部分虚拟网卡可能抛异常，忽略
            }
        }

        return (totalSent, totalReceived);
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_dataFilePath))
                return;

            var json = File.ReadAllText(_dataFilePath);
            var records = JsonSerializer.Deserialize<List<DailyTrafficRecord>>(json, JsonOpts);
            if (records is not null)
            {
                // 保留最近 30 天
                var cutoff = DateTime.Today.AddDays(-30);
                _history.AddRange(records.Where(r => r.Date >= cutoff));
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "加载流量历史数据失败");
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_history, JsonOpts);
            File.WriteAllText(_dataFilePath, json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "保存流量数据失败");
        }
    }

    public void Dispose()
    {
        Save();
    }
}
