import java.io.*;
import java.nio.file.*;
import java.util.*;

/**
 * 最小化 Minecraft 服务器核心 —— 模拟真实 MC 服务器的首次启动行为
 * 生成标准的配置文件：server.properties, spigot.yml, bukkit.yml,
 * config/paper-global.yml, ops.json, whitelist.json 等
 */
public class MiniServer {
    static final String SERVER_DIR = "test-server";

    public static void main(String[] args) throws Exception {
        System.out.println("[MiniServer] 启动最小化 Minecraft 服务器核心...");
        System.out.println("[MiniServer] 工作目录: " + Paths.get(SERVER_DIR).toAbsolutePath());

        // 1. 生成 EULA
        writeFile("eula.txt",
            "# By changing the setting below to TRUE you are indicating your agreement to our EULA (https://aka.ms/MinecraftEULA).\n" +
            "# Mon Jul 14 12:00:00 CST 2026\n" +
            "eula=true\n"
        );

        // 2. 生成 server.properties（Vanilla 标准格式）
        writeFile("server.properties",
            "#Minecraft server properties\n" +
            "#Mon Jul 14 12:00:00 CST 2026\n" +
            "enable-jmx-monitoring=false\n" +
            "rcon.port=25575\n" +
            "level-seed=\n" +
            "gamemode=survival\n" +
            "enable-command-block=false\n" +
            "level-name=world\n" +
            "motd=\\u00a7aMy Minecraft Server\\n\\u00a77Welcome!\n" +
            "query.port=25565\n" +
            "pvp=true\n" +
            "generate-structures=true\n" +
            "difficulty=easy\n" +
            "network-compression-threshold=256\n" +
            "max-players=20\n" +
            "use-native-transport=true\n" +
            "max-tick-time=60000\n" +
            "enable-status=true\n" +
            "online-mode=true\n" +
            "allow-flight=false\n" +
            "initial-disabled-packs=\n" +
            "broadcast-rcon-to-ops=true\n" +
            "view-distance=10\n" +
            "server-ip=\n" +
            "resource-pack-prompt=\n" +
            "allow-nether=true\n" +
            "server-port=25565\n" +
            "enable-rcon=false\n" +
            "sync-chunk-writes=true\n" +
            "enable-query=false\n" +
            "op-permission-level=4\n" +
            "prevent-proxy-connections=false\n" +
            "use-native-transport=true\n" +
            "entity-broadcast-range-percentage=100\n" +
            "simulation-distance=10\n" +
            "rcon.password=\n" +
            "player-idle-timeout=0\n" +
            "force-gamemode=false\n" +
            "rate-limit=0\n" +
            "hardcore=false\n" +
            "white-list=false\n" +
            "broadcast-console-to-ops=true\n" +
            "spawn-npcs=true\n" +
            "spawn-animals=true\n" +
            "function-permission-level=2\n" +
            "initial-enabled-packs=vanilla\n" +
            "level-type=minecraft\\:normal\n" +
            "text-filtering-config=\n" +
            "spawn-monsters=true\n" +
            "enforce-whitelist=false\n" +
            "spawn-protection=16\n" +
            "resource-pack-sha1=\n" +
            "max-world-size=29999984\n"
        );

        // 3. 生成 spigot.yml（Spigot/Paper 标准配置）
        writeFile("spigot.yml",
            "# This is the main configuration file for Spigot.\n" +
            "# As you can see, there's tons to configure. Some options may impact gameplay, so use\n" +
            "# with caution, and make sure you know what each option does before configuring it.\n" +
            "# For a reference for any variable inside this file, check out the Spigot wiki at\n" +
            "# http://www.spigotmc.org/wiki/spigot-configuration/\n" +
            "# \n" +
            "# If you need help with the configuration or have any questions related to Spigot,\n" +
            "# join us at the IRC or drop by our forums and leave a post.\n" +
            "# \n" +
            "# IRC: #spigot @ irc.esper.net ( http://webchat.esper.net/?channel=spigot )\n" +
            "# Forums: http://www.spigotmc.org/forum/forum/9-spigot-help/\n" +
            "\n" +
            "settings:\n" +
            "  timeout-time: 60\n" +
            "  restart-on-crash: true\n" +
            "  restart-script: ./start.sh\n" +
            "  netty-threads: 4\n" +
            "  log-villager-deaths: true\n" +
            "  log-named-deaths: true\n" +
            "  sample-count: 12\n" +
            "  bungeecord: false\n" +
            "  player-shuffle: 0\n" +
            "  moved-wrongly-threshold: 0.0625\n" +
            "  moved-too-quickly-multiplier: 10.0\n" +
            "  timeout-time: 60\n" +
            "  attribute:\n" +
            "    maxHealth:\n" +
            "      max: 2048.0\n" +
            "    movementSpeed:\n" +
            "      max: 2048.0\n" +
            "    attackDamage:\n" +
            "      max: 2048.0\n" +
            "messages:\n" +
            "  whitelist: You are not whitelisted on this server!\n" +
            "  unknown-command: Unknown command. Type \"/help\" for help.\n" +
            "  server-full: The server is full!\n" +
            "  outdated-client: Outdated client! Please use {0}\n" +
            "  outdated-server: Outdated server! I'm still on {0}\n" +
            "  restart: Server is restarting\n" +
            "commands:\n" +
            "  replace-commands:\n" +
            "  - setblock\n" +
            "  - summon\n" +
            "  - testforblock\n" +
            "  - testforblocks\n" +
            "  log: true\n" +
            "  tab-complete: 0\n" +
            "  send-namespaces: false\n" +
            "world-settings:\n" +
            "  default:\n" +
            "    verbose: false\n" +
            "    mob-spawn-range: 8\n" +
            "    entity-activation-range:\n" +
            "      animals: 32\n" +
            "      monsters: 32\n" +
            "      raiders: 64\n" +
            "      misc: 16\n" +
            "      water: 16\n" +
            "    entity-tracking-range:\n" +
            "      players: 48\n" +
            "      animals: 48\n" +
            "      monsters: 48\n" +
            "      misc: 32\n" +
            "      other: 64\n" +
            "    tick-rates:\n" +
            "      sensor:\n" +
            "        villager_secondary_poi: 40\n" +
            "      behavior:\n" +
            "        valid_position: 60\n" +
            "    merge-radius:\n" +
            "      item: 2.5\n" +
            "      exp: 3.0\n" +
            "    item-despawn-rate: 6000\n" +
            "    arrow-despawn-rate: 1200\n" +
            "    trident-despawn-rate: 1200\n" +
            "    zombie-aggressive-towards-villager: true\n" +
            "    nerf-spawner-mobs: false\n" +
            "    enable-zombie-pigmen-portal-spawns: true\n" +
            "    wither-spawn-sound-radius: 0\n" +
            "    end-portal-sound-radius: 0\n" +
            "    hanging-tick-frequency: 100\n" +
            "    max-entity-collisions: 8\n" +
            "    max-tick-time:\n" +
            "      tile: 50\n" +
            "      entity: 50\n" +
            "    growth:\n" +
            "      cactus-modifier: 100\n" +
            "      cane-modifier: 100\n" +
            "      melon-modifier: 100\n" +
            "      pumpkin-modifier: 100\n" +
            "      sapling-modifier: 100\n" +
            "      beetroot-modifier: 100\n" +
            "      carrot-modifier: 100\n" +
            "      potato-modifier: 100\n" +
            "      wheat-modifier: 100\n" +
            "      netherwart-modifier: 100\n" +
            "      vine-modifier: 100\n" +
            "      cocoa-modifier: 100\n" +
            "      bamboo-modifier: 100\n" +
            "      sweetberry-modifier: 100\n" +
            "      kelp-modifier: 100\n" +
            "      twistingvines-modifier: 100\n" +
            "      weepingvines-modifier: 100\n" +
            "      cavevines-modifier: 100\n" +
            "      glowberry-modifier: 100\n" +
            "    drop:\n" +
            "      leaves: 0\n" +
            "    hunger:\n" +
            "      jump-walk-exhaustion: 0.05\n" +
            "      jump-sprint-exhaustion: 0.2\n" +
            "      combat-exhaustion: 0.1\n" +
            "      regen-exhaustion: 6.0\n" +
            "      swim-multiplier: 0.01\n" +
            "      sprint-multiplier: 0.1\n" +
            "      other-multiplier: 0.0\n" +
            "    max-tnt-per-tick: 100\n" +
            "    simulation-distance: 10\n" +
            "    view-distance: 10\n" +
            "    dragon-death-sound-radius: 0\n" +
            "    seed-structure:\n" +
            "      ancient_city: 20083232\n" +
            "      buried_treasure: 10387320\n" +
            "      stronghold: default\n" +
            "      trail_ruins: 834697059\n" +
            "    seed-village: 10387312\n" +
            "    seed-desert: 14357617\n" +
            "    seed-igloo: 14357618\n" +
            "    seed-jungle: 14357619\n" +
            "    seed-swamp: 14357620\n" +
            "    seed-monument: 10387313\n" +
            "    seed-shipwreck: 165745295\n" +
            "    seed-ocean: 14357621\n" +
            "    seed-outpost: 165745296\n" +
            "    seed-endcity: 10387313\n" +
            "    seed-slime: 987234911\n" +
            "    seed-nether: 30084232\n" +
            "    seed-mansion: 10387319\n" +
            "    seed-fossil: 14357921\n" +
            "    seed-portal: 34222645\n" +
            "ticks-per:\n" +
            "  hopper-transfer: 8\n" +
            "  hopper-check: 1\n" +
            "advancements:\n" +
            "  disable-saving: false\n" +
            "  disabled:\n" +
            "  - minecraft:story/disabled\n" +
            "players:\n" +
            "  disable-saving: false\n"
        );

        // 4. 生成 bukkit.yml
        writeFile("bukkit.yml",
            "# This is the main configuration file for Bukkit.\n" +
            "# As you can see, there's tons to configure. Some options may impact gameplay, so use\n" +
            "# with caution, and make sure you know what each option does before configuring it.\n" +
            "\n" +
            "settings:\n" +
            "  allow-end: true\n" +
            "  warn-on-overload: true\n" +
            "  permissions-file: permissions.yml\n" +
            "  update-folder: update\n" +
            "  plugin-profiling: false\n" +
            "  connection-throttle: 4000\n" +
            "  query-plugins: true\n" +
            "  deprecated-verbose: default\n" +
            "  shutdown-message: Server closed\n" +
            "  minimum-api: none\n" +
            "  use-map-color-cache: true\n" +
            "spawn-limits:\n" +
            "  monsters: 70\n" +
            "  animals: 10\n" +
            "  water-animals: 5\n" +
            "  water-ambient: 20\n" +
            "  ambient: 15\n" +
            "chunk-gc:\n" +
            "  period-in-ticks: 600\n" +
            "ticks-per:\n" +
            "  animal-spawns: 400\n" +
            "  monster-spawns: 1\n" +
            "  water-spawns: 1\n" +
            "  water-ambient-spawns: 1\n" +
            "  ambient-spawns: 1\n" +
            "  autosave: 6000\n"
        );

        // 5. 生成 config/paper-global.yml（Paper 1.20+ 全局配置）
        writeFile("config/paper-global.yml",
            "# This is the global configuration file for Paper.\n" +
            "# \n" +
            "# Site: https://papermc.io\n" +
            "# IRC: #paper @ irc.esper.net ( https://webchat.esper.net/?channels=paper )\n" +
            "# Forum: https://forums.papermc.io/\n" +
            "\n" +
            "_version: 2\n" +
            "chunk-loading-basic:\n" +
            "  autoconfig-send-distance: 10\n" +
            "  player-max-concurrent-chunk-loads: 0.0\n" +
            "chunk-system:\n" +
            "  gen_parallelism: default\n" +
            "  io_parallelism: default\n" +
            "  worker_threads: default\n" +
            "collisions:\n" +
            "  enable-player-collisions: true\n" +
            "  send-packet-on-update: true\n" +
            "commands:\n" +
            "  fix-target-selector-tag-completion: true\n" +
            "  suggest-player-names-when-null-tab-completions: true\n" +
            "  time-command-affects-all-worlds: false\n" +
            "console:\n" +
            "  enable-brigadier-highlighting: true\n" +
            "  enable-brigadier-completions: true\n" +
            "  has-permission:\n" +
            "    all: op\n" +
            "    colors:\n" +
            "      - 'mcqr'\n" +
            "item-validation:\n" +
            "  book:\n" +
            "    author: 8192\n" +
            "    page: 16384\n" +
            "    title: 8192\n" +
            "  book-size:\n" +
            "    page-max: 2560\n" +
            "    total-multiplier: 0.98\n" +
            "logging:\n" +
            "  deobfuscate-stacktraces: true\n" +
            "misc:\n" +
            "  chat-threads:\n" +
            "    chat-executor-core-size: -1\n" +
            "    chat-executor-max-size: -1\n" +
            "  max-joins-per-second: 5\n" +
            "  lag-compensate-block-breaking: true\n" +
            "  load-permissions-yml-before-plugins: true\n" +
            "  strict-advancement-dimension-check: false\n" +
            "  use-alternative-luck-formula: false\n" +
            "  use-name-tag-visibility-scores: false\n" +
            "packet-limiter:\n" +
            "  all-packets:\n" +
            "    action: KICK\n" +
            "    interval: 7.0\n" +
            "    max-packet-rate: 500.0\n" +
            "  kick-message: '<red><lang:disconnect.exceeded_packet_rate></red>'\n" +
            "  overrides:\n" +
            "    ServerboundPlaceRecipePacket:\n" +
            "      action: DROP\n" +
            "      interval: 4.0\n" +
            "      max-packet-rate: 5.0\n" +
            "player-auto-save:\n" +
            "  max-players: 100\n" +
            "  rate: -1\n" +
            "proxies:\n" +
            "  bungee-cord:\n" +
            "    online-mode: true\n" +
            "  proxy-protocol: false\n" +
            "  velocity:\n" +
            "    enabled: false\n" +
            "    online-mode: false\n" +
            "    secret: ''\n" +
            "scoreboards:\n" +
            "  save-empty-scoreboard-teams: true\n" +
            "  track-plugin-scoreboards: false\n" +
            "spam-limiter:\n" +
            "  incoming-packet-threshold: 300\n" +
            "  recipe-spam-increment: 1\n" +
            "  recipe-spam-limit: 20\n" +
            "  tab-spam-increment: 1\n" +
            "  tab-spam-limit: 500\n" +
            "timings:\n" +
            "  enabled: false\n" +
            "  hidden-config-entries:\n" +
            "    - 'database'\n" +
            "    - 'proxies.velocity.secret'\n" +
            "  history-interval: 300\n" +
            "  history-length: 3600\n" +
            "  server-name: 'Unknown Server'\n" +
            "  server-name-privacy: false\n" +
            "  verbose: true\n" +
            "unsupported-settings:\n" +
            "  allow-grindstone-overstacking: false\n" +
            "  allow-headless-pistons: false\n" +
            "  allow-permanent-block-break-exploits: false\n" +
            "  allow-piston-duplication: false\n" +
            "  perform-username-validation: true\n" +
            "watchdog:\n" +
            "  early-warning-delay: 10000\n" +
            "  early-warning-every: 5000\n" +
            "log-ips: true\n" +
            "use-display-name-in-quit-message: false\n"
        );

        // 6. 生成 config/paper-world-defaults.yml
        writeFile("config/paper-world-defaults.yml",
            "# This is the world defaults configuration file for Paper.\n" +
            "_version: 2\n" +
            "anticheat:\n" +
            "  anti-xray:\n" +
            "    enabled: false\n" +
            "    engine-mode: 1\n" +
            "    hidden-blocks:\n" +
            "      - copper_ore\n" +
            "      - deepslate_copper_ore\n" +
            "    max-block-height: 64\n" +
            "    replacement-block: stone\n" +
            "    use-permission: false\n" +
            "chunks:\n" +
            "  auto-save-interval: 6000\n" +
            "  delay-chunk-unloads-by: 10s\n" +
            "  entity-activation-range:\n" +
            "    animals: 32\n" +
            "    monsters: 32\n" +
            "    misc: 16\n" +
            "collisions:\n" +
            "  max-entity-collisions: 8\n" +
            "entities:\n" +
            "  armor-stands:\n" +
            "    tick: true\n" +
            "  spawning:\n" +
            "    count-all-mobs-for-spawning: false\n" +
            "    despawn-ranges:\n" +
            "      ambient: 32\n" +
            "      axolotls: 32\n" +
            "      creature: 32\n" +
            "      misc: 32\n" +
            "      monster: 32\n" +
            "      underground_water_creature: 32\n" +
            "      water_ambient: 32\n" +
            "      water_creature: 32\n" +
            "fixes:\n" +
            "  disable-unloaded-chunk-entity-portal-teleportation: false\n" +
            "  fix-curing-zombie-villager-discount-exploit: true\n" +
            "  fix-items-merging-when-worlds-are-different: false\n" +
            "hopper:\n" +
            "  cooldown-when-full: true\n" +
            "  disable-move-event: false\n" +
            "  ignore-occluding-blocks: false\n"
        );

        // 7. 生成 JSON 配置文件
        writeFile("ops.json", "[]\n");
        writeFile("whitelist.json", "[]\n");
        writeFile("banned-ips.json", "[]\n");
        writeFile("banned-players.json", "[]\n");
        writeFile("usercache.json",
            "[{\"name\":\"Steve\",\"uuid\":\"069a79f4-44e9-4726-a5be-fca90e38aaf5\",\"expiresOn\":\"2026-07-14T12:00:00.000+0800\"}]\n"
        );

        // 8. 生成 commands.yml（Bukkit 命令配置）
        writeFile("commands.yml",
            "# This is the commands configuration file for Bukkit.\n" +
            "# For a reference for any variable inside this file, check out the Bukkit wiki at\n" +
            "# https://bukkit.fandom.com/wiki/Commands.yml\n" +
            "\n" +
            "command-block-overrides: []\n" +
            "ignore-vanilla-permissions: false\n" +
            "aliases:\n" +
            "  icanhasbukkit:\n" +
            "  - version\n"
        );

        // 9. 生成 permissions.yml
        writeFile("permissions.yml",
            "# This is the permissions configuration file for Bukkit.\n" +
            "# For a reference for any variable inside this file, check out the Bukkit wiki at\n" +
            "# https://bukkit.fandom.com/wiki/Permissions.yml\n" +
            "\n" +
            "default: op\n" +
            "groups: {}\n" +
            "users: {}\n"
        );

        // 10. 生成 help.yml
        writeFile("help.yml",
            "# This is the help configuration file for Bukkit.\n" +
            "\n" +
            "general:\n" +
            "  command-prefix: ''\n" +
            "  admin-topic: 'Server Commands'\n" +
            "  user-topic: 'Commands'\n" +
            "  list-of-commands-at-spawn: true\n" +
            "  display-aliases-on-command-match: true\n" +
            "default-topic-name: 'General'\n" +
            "amendments:\n" +
            "  /version:\n" +
            "    short-description: 'Shows the version of the server'\n" +
            "    full-description: 'Shows the version of the server you are running.'\n"
        );

        // 11. 生成一些常见的插件配置
        writeFile("plugins/Essentials/config.yml",
            "# Essentials Configuration\n" +
            "\n" +
            "teleport-cooldown: 0\n" +
            "teleport-delay: 0\n" +
            "heal-cooldown: 60\n" +
            "starting-balance: 0.0\n" +
            "currency-symbol: '$'\n" +
            "max-money: 10000000000000\n" +
            "min-money: -10000000000000\n" +
            "commands:\n" +
            "  rest:\n" +
            "    aliases: [rest, relax]\n" +
            "  feed:\n" +
            "    aliases: [feed, eat]\n"
        );

        System.out.println("[MiniServer] 所有配置文件已生成完毕！");
        System.out.println("[MiniServer] 文件列表：");

        // 列出所有生成的文件
        var serverPath = Paths.get(SERVER_DIR);
        Files.walk(serverPath)
            .filter(Files::isRegularFile)
            .forEach(p -> {
                try {
                    var size = Files.size(p);
                    System.out.println("  " + serverPath.relativize(p) + " (" + size + " bytes)");
                } catch (IOException e) {
                    System.out.println("  " + p + " (error reading size)");
                }
            });

        System.out.println("[MiniServer] 服务器核心启动完成（模拟）");
    }

    static void writeFile(String relativePath, String content) throws IOException {
        var path = Paths.get(SERVER_DIR, relativePath);
        Files.createDirectories(path.getParent());
        Files.writeString(path, content);
        System.out.println("[MiniServer] 生成: " + relativePath);
    }
}
