// -----------------------------------------------------------------------------
// 文件名: RegisterHelpYml.cs
// 功能描述: 注册 Bukkit help.yml（帮助页配置）的描述符
//           控制 /help 命令的显示：分页大小、主题格式、自定义主题、命令描述修订
// 数据来源: Bukkit Wiki - help.yml / org.bukkit.command.defaults.HelpCommand
// 适用版本: Bukkit 1.13+ / Spigot / Paper / Purpur 等所有 Bukkit 衍生核心
// 集成位置: 应粘贴到 ConfigDescriptorRegistry.cs 的 Register() 私有方法体中，
//           并在构造函数中调用 RegisterHelpYml();
// -----------------------------------------------------------------------------

private void RegisterHelpYml()
{
    const string file = "help.yml";

    // ==================== general（通用设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "general.command-prefix",
        ConfigFileName = file,
        DisplayName = "命令前缀",
        Description = "帮助页中命令的前缀字符\n一般保持 / (玩家输入命令的标准前缀)\n修改为其他字符仅影响显示，不影响实际命令执行",
        Category = "通用",
        DefaultValue = "/",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "general.console-command-prefix",
        ConfigFileName = file,
        DisplayName = "控制台命令前缀",
        Description = "控制台中命令的前缀\n留空则与 command-prefix 相同\n控制台输入命令无需 /，此值仅影响帮助页显示",
        Category = "通用",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "general.default-topic-format",
        ConfigFileName = file,
        DisplayName = "默认主题格式",
        Description = "默认帮助主题的输出格式模板\n可用占位符：\n  <description> = 命令描述\n  <usage> = 命令用法\n  <aliases> = 命令别名\n  <permission> = 所需权限\n默认值含两个换行（\\n）分隔描述、用法、别名三段",
        Category = "通用",
        DefaultValue = " <description>\\n\\n<usage>\\n\\n<aliases>\\n",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "general.search-index-listed",
        ConfigFileName = file,
        DisplayName = "搜索时列出索引",
        Description = "/help <关键词> 搜索时是否在结果中列出索引\true = 显示完整索引（信息全）\nfalse = 仅显示匹配项（更简洁）",
        Category = "通用",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "general.max-help-page-size",
        ConfigFileName = file,
        DisplayName = "每页最大帮助数",
        Description = "/help 每页显示多少条命令\n值越大单页内容越多（玩家翻页少）\n值越小分页越多（单页更清爽）\n建议 7-10 之间",
        Category = "通用",
        DefaultValue = "7",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "general.list-of-headers",
        ConfigFileName = file,
        DisplayName = "帮助页标题列表",
        Description = "各类帮助页的标题文本列表，按顺序对应：\n[0] 索引页标题\n[1] 搜索页标题\n[2] 主题页标题（<topic> 会被替换为主题名）\n[3] 主题列表页标题\n[4] 上一页按钮文本\n[5] 下一页按钮文本\n支持 § 颜色码",
        Category = "通用",
        DefaultValue = "[Help - Index, Help - Search, Help - <topic>, Help - Topics, Help - Previous, Help - Next]",
        ValueType = "list",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "general.default-topic-permission",
        ConfigFileName = file,
        DisplayName = "默认主题权限",
        Description = "查看默认帮助主题所需的权限节点\n留空 = 所有人可见\n填写权限节点（如 bukkit.command.help）= 仅拥有此权限的玩家可见",
        Category = "通用",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "general.topics-on-first-page",
        ConfigFileName = file,
        DisplayName = "首页显示主题列表",
        Description = "/help 第一页是否显示自定义主题列表\ntrue = 首页显示主题索引\nfalse = 首页直接显示命令列表",
        Category = "通用",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== amendments（命令修订） ====================
    // 对已注册命令的描述进行补充修改，不影响命令实际行为，仅影响帮助页显示。

    Register(new ServerConfigDescriptor
    {
        Key = "general.amendments",
        ConfigFileName = file,
        DisplayName = "命令修订列表",
        Description = "对已注册命令的描述进行覆盖修改\n键为命令名（不含 /），值为包含 short-description/full-description/usage/permission/aliases 的 map\n仅影响帮助页显示，不影响命令实际行为\n例：\namendments:\n  stop:\n    short-description: 关闭服务器\n    permission: bukkit.command.stop",
        Category = "命令修订",
        DefaultValue = "{}",
        ValueType = "map",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "general.amendments.<cmd>.short-description",
        ConfigFileName = file,
        DisplayName = "命令短描述",
        Description = "覆盖命令在帮助列表中的短描述（单行）\n仅影响显示，不影响实际命令\n例：\"关闭服务器\"",
        Category = "命令修订",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "general.amendments.<cmd>.full-description",
        ConfigFileName = file,
        DisplayName = "命令完整描述",
        Description = "覆盖命令的完整描述（多行）\n仅影响 /help <命令> 详情页\n例：\"关闭服务器并踢出所有玩家，需要 OP 权限\"",
        Category = "命令修订",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "general.amendments.<cmd>.usage",
        ConfigFileName = file,
        DisplayName = "命令用法",
        Description = "覆盖命令的用法说明\n例：\"/stop [确认]\"",
        Category = "命令修订",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "general.amendments.<cmd>.permission",
        ConfigFileName = file,
        DisplayName = "命令权限",
        Description = "覆盖命令所需的权限节点\n⚠️ 仅影响帮助页显示，不影响实际权限检查\n例：\"bukkit.command.stop\"\n要让玩家真正无法使用命令，需在 permissions.yml 或权限插件中设置",
        Category = "命令修订",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "general.amendments.<cmd>.aliases",
        ConfigFileName = file,
        DisplayName = "命令别名",
        Description = "覆盖命令的别名列表\n仅影响帮助页显示，不影响实际别名（实际别名在 plugin.yml 或 commands.yml 中定义）",
        Category = "命令修订",
        DefaultValue = "[]",
        ValueType = "list",
        RequiresRestart = false
    });

    // ==================== topics（自定义主题） ====================
    // 自定义帮助主题，玩家可通过 /help <主题名> 查看。
    // 常用于显示服务器规则、玩法说明等自定义内容。

    Register(new ServerConfigDescriptor
    {
        Key = "topics",
        ConfigFileName = file,
        DisplayName = "自定义主题列表",
        Description = "自定义帮助主题映射\n键为主题名（玩家通过 /help <主题名> 查看，主题名前的 / 可省略）\n值为包含 short-description/full-description/permission 的 map\n例：\ntopics:\n  /rules:\n    short-description: 服务器规则\n    full-description: |\n      1. 禁止作弊\n      2. 禁止恶意破坏\n    permission: ''",
        Category = "自定义主题",
        DefaultValue = "{}",
        ValueType = "map",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "topics.<topic>.short-description",
        ConfigFileName = file,
        DisplayName = "主题短描述",
        Description = "自定义主题在主题列表中的短描述（单行）\n例：\"服务器规则\"",
        Category = "自定义主题",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "topics.<topic>.full-description",
        ConfigFileName = file,
        DisplayName = "主题完整描述",
        Description = "自定义主题的完整描述（多行）\n支持 \\n 换行或 YAML 的 | 块字符串\n玩家执行 /help <主题名> 时显示此内容\n例：\nfull-description: |\n  1. 禁止作弊\n  2. 禁止恶意破坏\n  3. 禁止骚扰他人",
        Category = "自定义主题",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "topics.<topic>.permission",
        ConfigFileName = file,
        DisplayName = "主题查看权限",
        Description = "查看此主题所需的权限节点\n留空 = 所有人可见\n填写权限节点 = 仅拥有此权限的玩家可见\n例：\"server.rules.vip\" 仅 VIP 可见某主题",
        Category = "自定义主题",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    // ==================== index（索引页额外项） ====================
    // 在 /help 索引页中追加额外分类项，玩家可点击查看详情。

    Register(new ServerConfigDescriptor
    {
        Key = "index",
        ConfigFileName = file,
        DisplayName = "索引页额外项",
        Description = "在 /help 索引页中追加的分类项\n键为分类名，值为包含 short-description/full-description 的 map\n玩家可通过 /help <分类名> 查看详情\n例：\nindex:\n  basics:\n    short-description: 基础命令\n    full-description: 查看基础服务器命令",
        Category = "索引页",
        DefaultValue = "{}",
        ValueType = "map",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "index.<name>.short-description",
        ConfigFileName = file,
        DisplayName = "索引项短描述",
        Description = "索引页中某个分类项的短描述\n例：\"基础命令\"",
        Category = "索引页",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "index.<name>.full-description",
        ConfigFileName = file,
        DisplayName = "索引项完整描述",
        Description = "索引页中某个分类项的完整描述\n玩家通过 /help <名称> 查看此内容\n支持 \\n 换行或 YAML 块字符串",
        Category = "索引页",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    // ==================== 常用自定义主题示例（参考项） ====================
    // 以下注册几个常用自定义主题示例，供管理员参考。默认配置中 topics 为空，需手动添加。

    Register(new ServerConfigDescriptor
    {
        Key = "topics./rules.short-description",
        ConfigFileName = file,
        DisplayName = "示例：服务器规则主题",
        Description = "示例主题：玩家执行 /help rules 查看服务器规则\n配置：\ntopics:\n  /rules:\n    short-description: 服务器规则\n    full-description: |\n      1. 禁止作弊\n      2. 禁止恶意破坏\n      3. 禁止骚扰他人\n    permission: ''\n效果：所有人可 /help rules 查看规则",
        Category = "常用示例",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "topics./vip.short-description",
        ConfigFileName = file,
        DisplayName = "示例：VIP 权限主题",
        Description = "示例主题：仅 VIP 可查看的特权说明\n配置：\ntopics:\n  /vip:\n    short-description: VIP 特权\n    full-description: |\n      VIP 专属特权：\n      - /fly 飞行\n      - /heal 治疗\n      - /feed 充饥\n    permission: 'group.vip'\n效果：仅 VIP 组玩家可 /help vip 查看",
        Category = "常用示例",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "topics./menu.short-description",
        ConfigFileName = file,
        DisplayName = "示例：菜单导航主题",
        Description = "示例主题：列出服务器所有自定义主题导航\n配置：\ntopics:\n  /menu:\n    short-description: 服务器菜单\n    full-description: |\n      服务器帮助主题导航：\n      /help rules - 服务器规则\n      /help vip - VIP 特权\n      /help basics - 基础命令",
        Category = "常用示例",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });
}
