// =============================================================================
// 文件名: RegisterQuiltServerProperties.cs
// 功能描述: Quilt 启动器配置文件 quilt-server-launcher.properties 的描述符注册方法
// 配置文件: quilt-server-launcher.properties (Properties 格式，极简，仅 1 个键)
// 来源核心: Quilt Loader (https://github.com/QuiltMC/quilt)
// 适用版本: Quilt Loader 0.20+ / MC 1.14 ~ 1.21.x
// 数据来源: Quilt 官方文档 / Quilt 安装器源码
// 集成位置: 应粘贴到 ConfigDescriptorRegistry.cs 的 Register() 私有方法体中，
//           并在构造函数中调用 RegisterQuiltServerProperties();
// =============================================================================

private void RegisterQuiltServerProperties()
{
    const string file = "quilt-server-launcher.properties";

    // ==================== 启动器配置 ====================
    // Quilt 是 Fabric 的社区驱动分支，配置模式与 Fabric 完全一致。
    // 唯一的配置文件 quilt-server-launcher.properties 由 Quilt 安装器自动生成，
    // 与 quilt-server-launch.jar 同目录，仅含 1 个键 serverJar。
    // 其他所有服务器行为沿用原版 server.properties，请参阅 Vanilla 手册。
    //
    // ⚠️ 命名陷阱（历史遗留，请照抄）：
    //   - JAR 文件名为 quilt-server-launch.jar（无 er）
    //   - Properties 文件名为 quilt-server-launcher.properties（有 er）
    //
    // 启动入口是 quilt-server-launch.jar，不是 server.jar！
    // 启动命令示例：java -Xmx4G -Xms2G -jar quilt-server-launch.jar nogui

    Register(new ServerConfigDescriptor
    {
        Key = "serverJar",
        ConfigFileName = file,
        DisplayName = "原版服务端 JAR 路径",
        Description = "指向原版 Minecraft 服务端 JAR 文件的路径。Quilt 启动器会加载这个 JAR，并在其启动前注入 Quilt Loader（含 QSL）模组加载逻辑。\n默认值 server.jar 表示与启动器同目录下的 server.jar。\n\n何时需要修改：\n1) 若把原版 JAR 重命名为 vanilla.jar（如某些主机面板要求启动入口必须叫 server.jar），则改为 vanilla.jar，并把 quilt-server-launch.jar 重命名为 server.jar。\n2) 若原版 JAR 在其他目录，可填写相对路径（相对启动器 JAR 所在目录）或绝对路径。\n\n⚠️ 路径错误会导致启动失败，提示找不到主类 org.quiltmc.loader.impl.launch.server.QuiltServerLauncher 或找不到 JAR。",
        Category = "启动器配置",
        DefaultValue = "server.jar",
        ValueType = "string",
        RequiresRestart = true
    });
}
