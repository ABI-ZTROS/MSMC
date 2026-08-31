namespace io.NET.ZTR_OS.Features.ServerDetection.Services;

/// <summary>Supervisor 绑进程树所需的脚本启动信息 DTO</summary>
public class ScriptSupervisorInfo
{
    /// <summary>脚本绝对路径（日志用）</summary>
    public string ScriptPath { get; set; } = string.Empty;

    /// <summary>脚本是否包含 while(true) 自动重启循环 — Supervisor 据此互斥禁用崩溃自动重启</summary>
    public bool HasAutoRestart { get; set; }
}
