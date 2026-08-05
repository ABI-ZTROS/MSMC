import { useWizardStore } from '@/stores/wizardStore'

export function Step4ServerBasics() {
  const {
    serverName,
    setServerName,
    port,
    setPort,
    eulaAccepted,
    setEulaAccepted,
    onlineMode,
    setOnlineMode,
  } = useWizardStore()

  return (
    <div className="w-full">
      <div className="mb-6">
        <h2 className="text-xl font-bold mb-2" style={{ color: 'var(--md-body)' }}>
          基础设置
        </h2>
        <p className="text-sm" style={{ color: 'var(--md-body-light)' }}>
          配置服务器的基本信息，这些设置之后都可以在「配置编辑」里随时修改。
        </p>
      </div>

      <div
        className="md-card p-6"
        style={{
          border: '1px solid var(--md-card-subtle-border)',
        }}
      >
        <div className="flex flex-col gap-5">
          <div>
            <label
              className="block text-sm font-semibold mb-2"
              style={{ color: 'var(--md-body)' }}
            >
              服务器显示名
            </label>
            <input
              type="text"
              value={serverName}
              onChange={(e) => setServerName(e.target.value || 'Minecraft Server')}
              placeholder="Minecraft Server"
              className="w-full px-4 py-2.5 rounded-lg text-sm outline-none transition-all"
              style={{
                backgroundColor: 'var(--md-card-hover)',
                color: 'var(--md-body)',
                border: '1px solid var(--md-card-subtle-border)',
              }}
              onFocus={(e) => {
                e.currentTarget.style.borderColor = 'var(--md-primary-hue-mid)'
                e.currentTarget.style.boxShadow =
                  '0 0 0 3px color-mix(in srgb, var(--md-primary-hue-mid) 15%, transparent)'
              }}
              onBlur={(e) => {
                e.currentTarget.style.borderColor = 'var(--md-card-subtle-border)'
                e.currentTarget.style.boxShadow = 'none'
              }}
            />
            <div
              className="mt-1.5 text-[11px]"
              style={{ color: 'var(--md-body-lighter)' }}
            >
              这个名字会在 MSMC 服务器列表和通知里显示，不会影响游戏内显示。
            </div>
          </div>

          <div>
            <label
              className="block text-sm font-semibold mb-2"
              style={{ color: 'var(--md-body)' }}
            >
              服务器端口
            </label>
            <input
              type="number"
              min={1}
              max={65535}
              value={port}
              onChange={(e) => {
                const val = Number(e.target.value)
                if (!isNaN(val) && val >= 1 && val <= 65535) {
                  setPort(val)
                }
              }}
              className="w-full px-4 py-2.5 rounded-lg text-sm outline-none transition-all"
              style={{
                backgroundColor: 'var(--md-card-hover)',
                color: 'var(--md-body)',
                border: '1px solid var(--md-card-subtle-border)',
                WebkitAppearance: 'textfield',
                appearance: 'textfield',
              }}
              onFocus={(e) => {
                e.currentTarget.style.borderColor = 'var(--md-primary-hue-mid)'
                e.currentTarget.style.boxShadow =
                  '0 0 0 3px color-mix(in srgb, var(--md-primary-hue-mid) 15%, transparent)'
              }}
              onBlur={(e) => {
                e.currentTarget.style.borderColor = 'var(--md-card-subtle-border)'
                e.currentTarget.style.boxShadow = 'none'
              }}
            />
            <div
              className="mt-1.5 text-[11px] flex items-center gap-2"
              style={{ color: 'var(--md-body-lighter)' }}
            >
              <span>默认 25565。如果修改为非默认端口，连接时需要加 <code style={{
                backgroundColor: 'var(--md-card-hover)',
                padding: '1px 6px',
                borderRadius: '4px',
                color: 'var(--md-body-light)',
              }}>:端口号</code></span>
            </div>
          </div>

          <div
            className="pt-4 my-1"
            style={{
              borderTop: '1px dashed var(--md-card-subtle-border)',
              borderBottom: '1px dashed var(--md-card-subtle-border)',
            }}
          >
            <div className="flex flex-col gap-4 py-3">
              <label
                className="flex items-start gap-3 cursor-pointer select-none"
              >
                <input
                  type="checkbox"
                  checked={eulaAccepted}
                  onChange={(e) => setEulaAccepted(e.target.checked)}
                  className="mt-0.5 w-4 h-4 rounded cursor-pointer"
                  style={{
                    accentColor: eulaAccepted
                      ? 'var(--md-success, #22c55e)'
                      : 'var(--md-primary-hue-mid)',
                  }}
                />
                <div className="flex-1 min-w-0">
                  <div
                    className="text-sm font-semibold"
                    style={{
                      color: eulaAccepted ? 'var(--md-success-light, #86efac)' : 'var(--md-body)',
                    }}
                  >
                    我已阅读并同意 Mojang EULA
                  </div>
                  <div
                    className="text-[11px] mt-0.5"
                    style={{ color: 'var(--md-body-lighter)' }}
                  >
                    EULA（最终用户许可协议）是运行 Minecraft 服务器的法定前提。
                    不勾选此项服务器会直接拒绝启动。
                  </div>
                </div>
              </label>

              <label
                className="flex items-start gap-3 cursor-pointer select-none"
              >
                <input
                  type="checkbox"
                  checked={onlineMode}
                  onChange={(e) => setOnlineMode(e.target.checked)}
                  className="mt-0.5 w-4 h-4 rounded cursor-pointer"
                  style={{
                    accentColor: 'var(--md-primary-hue-mid)',
                  }}
                />
                <div className="flex-1 min-w-0">
                  <div
                    className="text-sm font-semibold"
                    style={{ color: 'var(--md-body)' }}
                  >
                    启用正版验证 <span className="font-normal text-[11px]" style={{ color: 'var(--md-body-lighter)' }}>(online-mode=true)</span>
                  </div>
                  <div
                    className="text-[11px] mt-0.5"
                    style={{ color: 'var(--md-body-lighter)' }}
                  >
                    只有购买了 Minecraft 正版的玩家才能加入。
                    关了则任何人都能进（含离线玩家），但强烈建议同时开启白名单。
                  </div>
                </div>
              </label>

              <label
                className="flex items-start gap-3 cursor-pointer select-none opacity-70"
              >
                <input
                  type="checkbox"
                  disabled
                  className="mt-0.5 w-4 h-4 rounded cursor-not-allowed"
                />
                <div className="flex-1 min-w-0">
                  <div
                    className="text-sm font-semibold"
                    style={{ color: 'var(--md-body)' }}
                  >
                    启动完成后自动打开服务器文件夹
                    <span
                      className="ml-2 text-[10px] px-1.5 py-[2px] rounded"
                      style={{
                        backgroundColor: 'var(--md-card-hover)',
                        color: 'var(--md-body-lighter)',
                      }}
                    >
                      选做
                    </span>
                  </div>
                  <div
                    className="text-[11px] mt-0.5"
                    style={{ color: 'var(--md-body-lighter)' }}
                  >
                    方便你查看和修改服务器文件（world / plugins / server.properties）。
                  </div>
                </div>
              </label>
            </div>
          </div>
        </div>
      </div>

      <div
        className="mt-4 p-3 rounded-lg text-xs flex items-start gap-2"
        style={{
          backgroundColor:
            'color-mix(in srgb, var(--md-danger, #ef4444) 10%, transparent)',
          color: 'var(--md-body-light)',
          border: '1px solid color-mix(in srgb, var(--md-danger, #ef4444) 25%, transparent)',
        }}
      >
        <span className="mt-[1px]">⚠️</span>
        <div>
          勾选「同意 EULA」后，程序将在服务器启动时自动在服务器目录生成 <code style={{
            backgroundColor: 'var(--md-card-hover)',
            padding: '1px 6px',
            borderRadius: '4px',
            color: 'var(--md-danger-light, #fca5a5)',
          }}>eula.txt</code> 并设置 <b>eula=true</b>。
        </div>
      </div>
    </div>
  )
}
