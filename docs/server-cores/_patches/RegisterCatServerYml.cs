// -----------------------------------------------------------------------------
// 文件名: RegisterCatServerYml.cs
// 功能描述: 注册 CatServer（混合端）配置文件的描述符
//           对应 catserver.yml
// 数据来源: Luohuayu/CatServer src/main/java/catserver/server/CatServerConfig.java
// 适用版本: CatServer 1.16.5（长期支持版本）
// -----------------------------------------------------------------------------

private void RegisterCatServerYml()
{
    const string file = "catserver.yml";

    // ==================== 世界设置 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world.keepSpawnInMemory",
        ConfigFileName = file,
        DisplayName = "出生点常驻内存",
        Description = "是否始终将出生点区域区块加载到内存中\n开启可避免新玩家进入时卡顿，但占用内存\n小型服建议 true，内存紧张的大型服可考虑 false",
        Category = "世界设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world.forceSaveOnWatchdog",
        ConfigFileName = file,
        DisplayName = "看门狗触发时强制保存",
        Description = "当服务器因 watchdog 超时崩溃时是否强制保存世界数据\n强烈建议 true 防止数据丢失\n注意：可能延长崩溃恢复时间",
        Category = "世界设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world.worldGenMaxTickTime",
        ConfigFileName = file,
        DisplayName = "世界生成最大 tick 时间",
        Description = "单次 tick 内世界生成的最大耗时（毫秒）\n降低此值可减少世界生成卡顿，但会延长生成完成时间\n玩家频繁飞行（鞘翅）时建议调高",
        Category = "世界设置",
        DefaultValue = "15",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = false
    });

    // ==================== 假人设置 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "fakePlayer.permissions",
        ConfigFileName = file,
        DisplayName = "假人默认权限列表",
        Description = "为服务器假人（如模组机器触发的虚拟玩家）添加的默认权限节点列表\n配合 Essentials 等插件实现假人自动建造、交互等功能\n每行一个权限节点",
        Category = "假人设置",
        DefaultValue = "essentials.build",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "fakePlayer.eventPass",
        ConfigFileName = file,
        DisplayName = "假人事件传递",
        Description = "是否让假人触发玩家事件（如方块破坏、实体交互）\n设为 false 减少服务器负载（推荐）\n设为 true 可实现更真实的假人行为（部分插件可能误判为真人玩家）",
        Category = "假人设置",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== 插件兼容性补丁 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "plugin.patcher.enableDynmapCompatible",
        ConfigFileName = file,
        DisplayName = "Dynmap 兼容补丁",
        Description = "修复 Dynmap 地图插件与 Forge 模组的兼容性问题\n使用 Dynmap 生成 3D 地图时必须开启",
        Category = "插件兼容补丁",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "plugin.patcher.enableWorldEditCompatible",
        ConfigFileName = file,
        DisplayName = "WorldEdit 兼容补丁",
        Description = "解决 WorldEdit 与部分 Forge 模组的方块操作冲突（如模组自定义方块无法被编辑）\n建议始终开启，除非确认不使用 WorldEdit",
        Category = "插件兼容补丁",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "plugin.patcher.enableEssentialsNewVersionCompatible",
        ConfigFileName = file,
        DisplayName = "Essentials 新版兼容补丁",
        Description = "支持 EssentialsX 等新版本 Essentials 插件\n修复指令冲突、权限管理等兼容性问题\n使用 EssentialsX 时必须开启",
        Category = "插件兼容补丁",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== 性能优化 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "optimization.async-chunk-loading",
        ConfigFileName = file,
        DisplayName = "异步区块加载",
        Description = "是否启用异步区块加载\n开启可减少主线程阻塞，提升玩家飞行/传送时的流畅度\n⚠️ 与部分老式模组可能冲突",
        Category = "性能优化",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "optimization.reduce-lag",
        ConfigFileName = file,
        DisplayName = "启用防卡顿优化",
        Description = "启用 CatServer 的综合防卡顿优化（实体激活范围、AI 节流等）\n建议保持 true",
        Category = "性能优化",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "optimization.fast-operations",
        ConfigFileName = file,
        DisplayName = "快速操作优化",
        Description = "启用快速方块/实体操作优化\n可提升约 10% TPS\n⚠️ 与依赖精确事件触发的红石插件可能冲突",
        Category = "性能优化",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== 村民与红石 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "villager.atFix",
        ConfigFileName = file,
        DisplayName = "村民 AI 修复",
        Description = "修复部分 Forge 模组导致的村民 AI 异常（村民不工作/卡住）\n建议保持 true",
        Category = "村民与红石",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== 通用设置 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "versionCheck",
        ConfigFileName = file,
        DisplayName = "版本检查",
        Description = "启动时自动检查 CatServer 更新\n建议 true 以及时获取安全更新",
        Category = "通用设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "disableAsyncCatchWarn",
        ConfigFileName = file,
        DisplayName = "禁用异步捕获警告",
        Description = "是否禁用插件异步操作警告\n插件调试时可设 true，生产环境建议 false 以便发现插件异步调用主线程 API 的问题",
        Category = "通用设置",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });
}
