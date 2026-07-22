using System.Collections.Generic;
using McServerGuard.Models;

namespace McServerGuard.Constants;

public static class CommonPorts
{
    public static readonly List<CommonPort> All =
    [
        new CommonPort { Port = 21, Name = "FTP", Description = "文件传输协议", Category = "网络服务" },
        new CommonPort { Port = 22, Name = "SSH", Description = "安全外壳协议", Category = "远程管理" },
        new CommonPort { Port = 23, Name = "Telnet", Description = "远程终端协议", Category = "远程管理" },
        new CommonPort { Port = 25, Name = "SMTP", Description = "简单邮件传输协议", Category = "邮件服务" },
        new CommonPort { Port = 53, Name = "DNS", Description = "域名系统", Category = "网络服务" },
        new CommonPort { Port = 67, Name = "DHCP", Description = "动态主机配置协议", Category = "网络服务" },
        new CommonPort { Port = 80, Name = "HTTP", Description = "超文本传输协议", Category = "Web服务" },
        new CommonPort { Port = 110, Name = "POP3", Description = "邮局协议", Category = "邮件服务" },
        new CommonPort { Port = 143, Name = "IMAP", Description = "互联网邮件访问协议", Category = "邮件服务" },
        new CommonPort { Port = 443, Name = "HTTPS", Description = "安全超文本传输协议", Category = "Web服务" },
        new CommonPort { Port = 3389, Name = "RDP", Description = "远程桌面协议", Category = "远程管理" },
        new CommonPort { Port = 25565, Name = "Minecraft", Description = "Minecraft Java版服务器", Category = "游戏" },
        new CommonPort { Port = 19132, Name = "Minecraft BE", Description = "Minecraft基岩版服务器", Category = "游戏" },
        new CommonPort { Port = 3306, Name = "MySQL", Description = "MySQL数据库", Category = "数据库" },
        new CommonPort { Port = 5432, Name = "PostgreSQL", Description = "PostgreSQL数据库", Category = "数据库" },
        new CommonPort { Port = 6379, Name = "Redis", Description = "Redis缓存", Category = "缓存" },
        new CommonPort { Port = 8080, Name = "HTTP Alt", Description = "备用HTTP端口", Category = "Web服务" },
        new CommonPort { Port = 8443, Name = "HTTPS Alt", Description = "备用HTTPS端口", Category = "Web服务" },
        new CommonPort { Port = 9092, Name = "Kafka", Description = "Apache Kafka", Category = "消息队列" },
        new CommonPort { Port = 27017, Name = "MongoDB", Description = "MongoDB数据库", Category = "数据库" },
        new CommonPort { Port = 5900, Name = "VNC", Description = "虚拟网络计算", Category = "远程管理" },
        new CommonPort { Port = 1433, Name = "SQL Server", Description = "Microsoft SQL Server", Category = "数据库" },
        new CommonPort { Port = 445, Name = "SMB", Description = "服务器消息块", Category = "文件共享" },
        new CommonPort { Port = 139, Name = "NetBIOS", Description = "网络基本输入输出系统", Category = "文件共享" },
        new CommonPort { Port = 5060, Name = "SIP", Description = "会话发起协议", Category = "VoIP" },
        new CommonPort { Port = 5004, Name = "RTP", Description = "实时传输协议", Category = "VoIP" },
        new CommonPort { Port = 123, Name = "NTP", Description = "网络时间协议", Category = "网络服务" },
        new CommonPort { Port = 49152, Name = "Dynamic", Description = "动态端口范围起始", Category = "系统" },
        new CommonPort { Port = 65535, Name = "Max Port", Description = "最大端口号", Category = "系统" },
    ];
}