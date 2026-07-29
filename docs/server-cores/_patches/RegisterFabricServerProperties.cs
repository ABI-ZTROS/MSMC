// =============================================================================
// 文件名: RegisterFabricServerProperties.cs
// 功能描述: Fabric 启动器配置文件 fabric-server-launcher.properties 的描述符注册方法
// 配置文件: fabric-server-launcher.properties (Properties 格式，极简，仅 1 个键)
// 来源核心: Fabric Loader (https://github.com/FabricMC/fabric)
// 适用版本: Fabric Loader 0.4+ / MC 1.14 ~ 1.21.x
// 数据来源: Fabric 官方 Wiki / Fabric 安装器源码
// 集成位置: 应粘贴到 ConfigDescriptorRegistry.cs 的 Register() 私有方法体中，
//           并在构造函数中调用 RegisterFabricServerProperties();
// =============================================================================

private void RegisterFabricServerProperties()
{
    const string file = "fabric-server-launcher.properties";

    // ==================== 启动器配置 ====================
    // Fabric 是模组加载器，不是完整的服务端实现。其唯一的配置文件 fabric-server-launcher.properties
    // 由 Fabric 安装器自动生成，与 fabric-server-launch.jar 同目录，仅含 1 个键 serverJar。
    // 其他所有服务器行为（端口、视距、白名单等）沿用原版 server.properties，请参阅 Vanilla 手册。
    //
    // 启动入口是 fabric-server-launch.jar，不是 server.jar！
    // 启动命令示例：java -Xmx4G -Xms2G -jar fabric-server-launch.jar nogui

    Register(new ServerConfigDescriptor
    {
        Key = "serverJar",
        ConfigFileName = file,
        DisplayName = "原版服务端 JAR 路径",
        Description = "指向原版 Minecraft 服务端 JAR 文件的路径。Fabric 启动器会加载这个 JAR，并在其启动前注入 Fabric Loader 模组加载逻辑。\n默认值 server.jar 表示与启动器同目录下的 server.jar。\n\n何时需要修改：\n1) 若把原版 JAR 重命名为 vanilla.jar（如某些主机面板要求启动入口必须叫 server.jar），则改为 vanilla.jar，并把 fabric-server-launch.jar 重命名为 server.jar。\n2) 若原版 JAR 在其他目录，可填写相对路径（相对启动器 JAR 所在目录）或绝对路径。\n\n⚠️ 路径错误会导致启动失败，提示找不到主类 net.fabricmc.loader.impl.launch.server.FabricServerLauncher 或找不到 JAR。",
        Category = "启动器配置",
        DefaultValue = "server.jar",
        ValueType = "string",
        RequiresRestart = true
    });
}
