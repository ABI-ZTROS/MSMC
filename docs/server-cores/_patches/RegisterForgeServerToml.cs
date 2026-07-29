// =============================================================================
// 文件名: RegisterForgeServerToml.cs
// 功能描述: Forge 服务端配置文件 forge-server.toml 的描述符注册方法
// 配置文件: <世界名>/serverconfig/forge-server.toml (TOML 格式)
// 来源核心: Minecraft Forge (https://github.com/MinecraftForge/MinecraftForge)
// 适用版本: Forge 1.18 ~ 1.21.x (自 1.14 起配置体系基本一致)
// 数据来源: Forge 1.21.x 源码 ForgeConfig.java
// 集成位置: 应粘贴到 ConfigDescriptorRegistry.cs 的 Register() 私有方法体中，
//           并在构造函数中调用 RegisterForgeServerToml();
// =============================================================================

private void RegisterForgeServerToml()
{
    const string file = "forge-server.toml";

    // ==================== [server] 节 —— 服务端配置 ====================
    // 注意：Forge 的 forge-server.toml 位于 <世界名>/serverconfig/ 下，不在根目录 config/ 下。
    // 所有配置项位于 [server] 节，TOML 路径形式为 server.<键名>。

    Register(new ServerConfigDescriptor
    {
        Key = "server.removeErroringBlockEntities",
        ConfigFileName = file,
        DisplayName = "删除报错方块实体",
        Description = "设为 true 时，当某个方块实体（BlockEntity，即 TileEntity，如箱子/熔炉/模组机器）在其更新方法中抛出异常，Forge 会直接删除该方块实体，而不是关闭服务器并打印崩溃日志。\n⚠️ 危险选项：可能导致机器内物品丢失、方块状态错乱。\n仅作为排查「Ticking Block Entity」崩溃的应急手段临时开启，处理完务必改回 false！\nForge 官方明确声明对此造成的损失不负责。",
        Category = "服务端 / 故障修复",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "server.removeErroringEntities",
        ConfigFileName = file,
        DisplayName = "删除报错实体",
        Description = "设为 true 时，当某个实体（Entity，如僵尸、掉落物、矿车等，不包括方块实体）在其 tick 方法中抛出异常，Forge 会直接删除该实体，而不是关闭服务器并打印崩溃日志。\n⚠️ 危险选项：可能导致玩家丢失骑乘的坐骑、农场中的关键生物等。\n仅作为排查「Ticking Entity」崩溃的应急手段临时开启，处理完务必改回 false！",
        Category = "服务端 / 故障修复",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "server.fullBoundingBoxLadders",
        ConfigFileName = file,
        DisplayName = "完整碰撞盒爬梯检测",
        Description = "设为 true 时，检测实体是否在爬梯子会检查整个实体碰撞盒所覆盖的方块，而不仅限于实体当前所在的方块。\n会带来明显的机制差异（更高的爬梯判定范围），默认保持原版行为。\n仅在你确知某些模组需要此特性时才开启。",
        Category = "服务端 / 游戏机制",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "server.permissionHandler",
        ConfigFileName = file,
        DisplayName = "权限处理器",
        Description = "服务器使用的权限处理器 ID。默认为 forge:default_handler（Forge 内置的默认权限处理器）。\n仅当服务器中安装了提供自定义权限系统的模组时才需要修改。\n普通开服玩家保持默认即可。错误的值会导致服务器启动失败。",
        Category = "服务端 / 权限",
        DefaultValue = "forge:default_handler",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "server.advertiseDedicatedServerToLan",
        ConfigFileName = file,
        DisplayName = "向局域网广播服务器",
        Description = "设为 true 时，专用服务端会向本地局域网广播自身存在，使同局域网下的客户端能在「多人游戏」界面自动看到这台服务器。\n公网/VPS 部署时无实际意义；本地测试时不希望他人自动看到可关闭。",
        Category = "服务端 / 网络",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });
}
