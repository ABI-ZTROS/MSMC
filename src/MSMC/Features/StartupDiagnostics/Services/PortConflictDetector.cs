using io.NET.ZTR_OS.Features.StartupDiagnostics.Models;

namespace io.NET.ZTR_OS.Features.StartupDiagnostics.Services;

public class PortConflictDetector
{
    private readonly Func<string, string>? _netstatRunner;

    public PortConflictDetector()
    {
        _netstatRunner = null;
    }

    public PortConflictDetector(Func<string, string> netstatRunner)
    {
        _netstatRunner = netstatRunner;
    }

    public virtual async Task<StartupDiagnosis?> CheckPortAsync(int port = 25565)
    {
        if (port <= 0)
            return null;

        bool inUse = await IsPortInUseAsync(port);
        if (!inUse)
            return null;

        string processName = await GetOwningProcessNameAsync(port);
        string description = string.IsNullOrEmpty(processName)
            ? $"端口{port}被占用"
            : $"端口{port}被 {processName} 占用";

        return new StartupDiagnosis(
            Severity: DiagnosisSeverity.Critical,
            Description: description,
            SuggestedAction: "关闭占用端口的进程后重试",
            OneClickFixCommandId: $"kill-port-{port}");
    }

    protected virtual async Task<bool> IsPortInUseAsync(int port)
    {
        if (_netstatRunner != null)
        {
            var output = _netstatRunner($"port-check:{port}");
            await Task.CompletedTask;
            return output.Contains($"LISTENING", StringComparison.OrdinalIgnoreCase)
                   || output.Contains($":{port} ", StringComparison.OrdinalIgnoreCase);
        }
        await Task.CompletedTask;
        return false;
    }

    protected virtual async Task<string> GetOwningProcessNameAsync(int port)
    {
        if (_netstatRunner != null)
        {
            await Task.CompletedTask;
            return _netstatRunner($"process-name:{port}");
        }
        await Task.CompletedTask;
        return string.Empty;
    }
}
