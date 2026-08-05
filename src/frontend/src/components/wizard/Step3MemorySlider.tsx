import { useWizardStore } from '@/stores/wizardStore'

function formatMB(mb: number): string {
  if (mb >= 1024) {
    const gb = mb / 1024
    return `${gb % 1 === 0 ? gb.toFixed(0) : gb.toFixed(1)}G`
  }
  return `${mb}M`
}

export function Step3MemorySlider() {
  const { memoryMB, setMemoryMB } = useWizardStore()

  const deviceMemory = (navigator as any).deviceMemory as number | undefined
  const hasDeviceMemory = typeof deviceMemory === 'number' && deviceMemory > 0

  const maxMB = hasDeviceMemory
    ? Math.floor(deviceMemory * 1024 * 0.75)
    : 16384
  const minMB = 512
  const stepMB = 256

  const xmsMB = Math.floor(memoryMB / 2)
  const systemReserveHint = hasDeviceMemory
    ? `建议预留 ${formatMB(Math.max(2048, Math.floor(deviceMemory * 1024 - memoryMB)))} 给系统和浏览器`
    : '建议预留 2~4G 给系统和浏览器，不要拉满'

  return (
    <div className="w-full">
      <div className="mb-6">
        <h2 className="text-xl font-bold mb-2" style={{ color: 'var(--md-body)' }}>
          分配内存
        </h2>
        <p className="text-sm" style={{ color: 'var(--md-body-light)' }}>
          给 Java 虚拟机分配多少内存。内存越大，服务器能带的玩家和视距越高，但也要留一些给操作系统。
        </p>
      </div>

      <div
        className="md-card p-5 mb-6"
        style={{
          border: '1px solid var(--md-card-subtle-border)',
        }}
      >
        <div
          className="text-xs mb-5 flex items-center gap-2"
          style={{ color: 'var(--md-body-light)' }}
        >
          <span>🔍</span>
          <span>
            检测系统内存:{' '}
            {hasDeviceMemory ? (
              <b style={{ color: 'var(--md-body)' }}>
                {(deviceMemory! * 1.5).toFixed(1)}GB（推测）
              </b>
            ) : (
              <b style={{ color: 'var(--md-warning, #fbbf24)' }}>未知</b>
            )}
          </span>
          {hasDeviceMemory && (
            <span className="text-[11px] px-2 py-[2px] rounded-md"
              style={{
                backgroundColor: 'var(--md-card-hover)',
                color: 'var(--md-body-lighter)',
              }}
            >
              浏览器检测值仅供参考
            </span>
          )}
        </div>

        <div className="grid grid-cols-2 gap-4 mb-6">
          <div
            className="rounded-lg p-4 text-center"
            style={{
              backgroundColor: 'var(--md-card-hover)',
              border: '1px solid var(--md-card-subtle-border)',
            }}
          >
            <div
              className="text-[10px] font-semibold mb-1 tracking-wider"
              style={{ color: 'var(--md-body-lighter)' }}
            >
              初始堆 -Xms
            </div>
            <div
              className="text-lg font-bold"
              style={{ color: 'var(--md-primary-hue-light)' }}
            >
              {formatMB(xmsMB)}
            </div>
            <div className="text-[10px] mt-1" style={{ color: 'var(--md-body-lighter)' }}>
              {xmsMB}M
            </div>
          </div>
          <div
            className="rounded-lg p-4 text-center"
            style={{
              backgroundColor: 'var(--md-card-hover)',
              border: '1px solid var(--md-card-subtle-border)',
            }}
          >
            <div
              className="text-[10px] font-semibold mb-1 tracking-wider"
              style={{ color: 'var(--md-body-lighter)' }}
            >
              最大堆 -Xmx
            </div>
            <div
              className="text-lg font-bold"
              style={{ color: 'var(--md-primary-hue-mid)' }}
            >
              {formatMB(memoryMB)}
            </div>
            <div className="text-[10px] mt-1" style={{ color: 'var(--md-body-lighter)' }}>
              {memoryMB}M
            </div>
          </div>
        </div>

        <div className="mb-2">
          <input
            type="range"
            min={minMB}
            max={maxMB}
            step={stepMB}
            value={memoryMB}
            onChange={(e) => setMemoryMB(Number(e.target.value))}
            className="w-full h-2 rounded-lg appearance-none cursor-pointer"
            style={{
              background: `linear-gradient(to right, var(--md-primary-hue-mid) 0%, var(--md-primary-hue-mid) ${
                ((memoryMB - minMB) / (maxMB - minMB)) * 100
              }%, var(--md-card-hover) ${
                ((memoryMB - minMB) / (maxMB - minMB)) * 100
              }%, var(--md-card-hover) 100%)`,
              accentColor: 'var(--md-primary-hue-mid)',
            }}
          />
        </div>

        <div
          className="flex justify-between text-[11px] mb-2"
          style={{ color: 'var(--md-body-lighter)' }}
        >
          <span>
            {formatMB(minMB)}
            <span className="ml-1 opacity-60">（最低）</span>
          </span>
          <span>
            {formatMB(maxMB)}
            <span className="ml-1 opacity-60">（建议上限 75%）</span>
          </span>
        </div>

        <div className="flex flex-wrap gap-2 mt-4">
          {[2048, 4096, 6144, 8192].filter((v) => v <= maxMB).map((preset) => (
            <button
              key={preset}
              type="button"
              onClick={() => setMemoryMB(preset)}
              className="md-btn md-btn-outlined text-xs px-3 py-1"
              style={{
                borderColor:
                  memoryMB === preset ? 'var(--md-primary-hue-mid)' : undefined,
                color: memoryMB === preset ? 'var(--md-primary-hue-light)' : undefined,
                backgroundColor:
                  memoryMB === preset
                    ? 'color-mix(in srgb, var(--md-primary-hue-mid) 12%, transparent)'
                    : undefined,
              }}
            >
              {formatMB(preset)}
            </button>
          ))}
        </div>
      </div>

      <div
        className="p-3 rounded-lg text-xs flex items-start gap-2"
        style={{
          backgroundColor:
            'color-mix(in srgb, var(--md-warning, #f59e0b) 10%, transparent)',
          color: 'var(--md-body-light)',
          border: '1px solid color-mix(in srgb, var(--md-warning, #f59e0b) 25%, transparent)',
        }}
      >
        <span className="mt-[1px]">💡</span>
        <div>
          <div style={{ color: 'var(--md-warning, #fbbf24)' }} className="font-semibold mb-1">
            {systemReserveHint}
          </div>
          <div style={{ color: 'var(--md-body-lighter)' }} className="text-[11px]">
            内存 = 服务器世界加载 + 玩家实体 + 插件 Mod。
            1-3 人小服推荐 <b>2G~4G</b>；5-10 人推荐 <b>4G~6G</b>；大型 Mod 服建议 <b>8G+</b>。
          </div>
        </div>
      </div>
    </div>
  )
}
