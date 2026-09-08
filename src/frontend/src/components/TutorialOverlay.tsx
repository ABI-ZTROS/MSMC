import { useEffect, useState } from 'react'
import { AIDrawer } from './AIDrawer'

/**
 * TutorialOverlay — 「我不会开服」新手全流程教程
 * 叠加渲染在当前页面之上（不跳路由），完整覆盖：装 Java → 搞核心 → 导入 → 调内存 → 启动
 * → 开放端口 → 体检修复 → 监管兜底。
 *
 * 梦幻联动：教程左 60% + AI 诊断抽屉右 40% 并排。每一步都可以一键"让 AI 帮我"。
 * 风格要求：小屁孩都看得懂——大白话 + 比喻 + emoji，强制穿插 😡😋😂😅🤔🧐🤣😱😨🔥🤓
 */

interface TutorialStep {
  icon: string
  title: string
  lines: string[]
  action?: string
}

const STEPS: TutorialStep[] = [
  {
    icon: '🧠',
    title: '第一步：给你的电脑装个「大脑」—— Java',
    lines: [
      '开服之前，你的电脑得有个东西叫 Java（读音：抓哇）。你可以把它想成「服务器的心脏」，没有它，服务器一秒钟都活不了 🤔',
      '你不需要懂 Java 是什么，你只需要有它。就跟手机要有电一样，道理就是这么简单 😅',
      '打开左侧菜单的「Java 管理」页面，点「重新扫描」——如果列表里是空的，那就是没装，快去官网下载一个然后点「添加路径」把它指给 MSMC。',
    ],
    action: '在左侧菜单点「Java 管理」检查安装',
  },
  {
    icon: '📦',
    title: '第二步：搞一个服务器「本体」—— 核心 JAR',
    lines: [
      '服务器本体是一个 xxx.jar 文件，你可以把它当成「一个工厂的蓝图」。🧐',
      '去哪搞？两个地方：① MSMC 的「内容市场」页面，挑一个喜欢的核心直接下；② 去核心官网下载（Paper / Purpur 都很好用）。',
      '小屁孩求生指南：别贪新。选用户最多的那个核心，就像买奶茶排队最长的那家，大概率不踩雷 😋',
    ],
    action: '打开「市场」页挑一个核心，或自己准备好 xxx.jar',
  },
  {
    icon: '📥',
    title: '第三步：把「本体」塞进 MSMC —— 导入服务器',
    lines: [
      '回到首页（服务器管理），点左上角「导入服务器」按钮，选择你刚下载的 .jar 文件。',
      '导入之后，它就会出现在左边的「已知服务器」列表里。恭喜，你现在是个"有服务器的人"了 😂',
      '如果列表里还是没有：看看是不是文件选错了（要 .jar 结尾，不是 .zip！）。没反应？等两秒？还是没？刷新一下。——别慌，多试一次，失败是成功他妈 🤣',
    ],
    action: '首页 → 左上角「导入服务器」 → 选 jar',
  },
  {
    icon: '🎒',
    title: '第四步：给服务器塞满「小背包」—— 内存',
    lines: [
      '服务器跑起来要吃东西，吃的东西叫「内存」。你可以在首页选中你的服务器，点「JVM 参数」页签，把初始内存和最大内存都设成 4G（如果你的电脑内存 ≥ 8G）。',
      '太小会怎样？会卡、会闪退、甚至直接"心脏病发"（OutOfMemoryError）😱😨',
      '太大又会怎样？电脑剩不下内存给你自己用，然后你开个浏览器都卡成 PPT。量力而行，4G 是大多数萌新的甜蜜点 🤓',
    ],
    action: '首页 → 选中服务器 → JVM 参数 → 设 4G',
  },
  {
    icon: '🚀',
    title: '第五步：点火！启动服务器',
    lines: [
      '选中服务器，点那个大绿框「启动服务器」。第一次启动它会生成一堆文件，还会弹出一个英文协议：eula.txt —— 把它里面的 false 改成 true（意思就是"我同意规则"）。',
      '不知道在哪改？MSMC 的「配置编辑」页可以直接打开 eula.txt 让你改，不用手忙脚乱找文件夹 😅',
      '看到控制台出现 "Done!"（或者中文"完成"）——恭喜，你的服务器跑起来了！🔥',
    ],
    action: '首页 → 启动服务器 → 配置编辑里把 eula=false 改成 true',
  },
  {
    icon: '🚪',
    title: '第六步：让朋友进你家门 —— 端口和防火墙',
    lines: [
      '服务器默认走「25565」号门口（专业叫"端口"）。朋友想进来，你得把自家大门敞开：',
      '① 确认服务器监听正常（首页显示绿色圆点就行）。② 检查 Windows 防火墙有没有放行 —— 用「疑难解答」页面跑一次体检，它会直接告诉你防火墙规则有没有问题 😡 别让它拦着你！',
      '如果是联机到别人家（局域网），选"对局域网开放"或让朋友加你电脑的局域网 IP，端口 + IP 双确认，进不去就是这俩没对上 🤔',
    ],
    action: '疑难解答 → 体检 → 看防火墙/端口检查结果',
  },
  {
    icon: '🩺',
    title: '第七步：服务器"生病"了怎么办 —— 疑难解答',
    lines: [
      '卡顿？进不去？疯狂报错？别慌，MSMC 的「疑难解答」就是服务器的"体检医生"：',
      '跑一次体检，它会检查 Java 版本、内存、端口、防火墙、日志里的 OOM……一项一项告诉你哪里有问题，还能让 AI 帮你总结诊断结论 🧐',
      '查出毛病还能直接点「一键修复」——它会先帮你备份，再动手，改坏了也能回滚。这就是"医生先化验再开药"🤓',
      '要是它说你没问题你却觉得卡：多半是网络/电脑配置的事，别甩锅给服务器 😂',
    ],
    action: '疑难解答 → 跑体检 → 按结果一键修复',
  },
  {
    icon: '🛡️',
    title: '第八步：不怕它挂 —— 监管模式 & 自动重启',
    lines: [
      '服务器半夜自己崩了，你难道要守到天亮吗？不用！',
      '服务器的「监管」开关打开后，MSMC 会像家长一样看着它：偷偷崩了？自动帮你重启；还能设"崩溃上限"，反复崩溃就停下来报警，绝不让你家的服务器"原地躺平" 😱',
      '这是全网少数能做到崩溃自动拉起还带优先级调度的功能——等于白捡一个 24 小时不睡觉的网管 🔥😋',
    ],
    action: '选中服务器 → 打开监管开关（若该核心支持）',
  },
  {
    icon: '🎓',
    title: '毕业考试：现在你已经是"会开服的人"了！',
    lines: [
      '从头理一遍：Java 装了没？核心导入了没？内存设了没？启动完 eula 改了没？朋友进不来看防火墙？崩了用疑难解答。',
      '全对？那你毕业了，比 99% 的萌新都强 😎（没有这个 emoji？那换成 🤣）',
      '万一还是没搞定？别不好意思，再点一次这个按钮，从头再看一遍——我不是在教你，我是在把你教会 😅',
      '实在走投无路：底部菜单「疑难解答」里有详细的 AI 对话功能，把你的报错贴给它，它比搜索引擎懂你多了 🤓',
    ],
    action: '遇到问题 → 回到疑难解答 → 贴报错给 AI',
  },
]

interface TutorialOverlayProps {
  open: boolean
  onClose: () => void
}

export function TutorialOverlay({ open, onClose }: TutorialOverlayProps): JSX.Element | null {
  // AI 抽屉状态 + 当前聚焦的教程步骤（传给 AI 上下文）
  const [aiOpen, setAiOpen] = useState(false)
  const [currentStep, setCurrentStep] = useState(0)

  // Esc 键关闭（叠加层惯例）
  useEffect(() => {
    if (!open) return
    const handler = (e: KeyboardEvent): void => {
      if (e.key === 'Escape') {
        // 如果 AI 抽屉开着，先关 AI；再按一次 Esc 才关整个教程
        if (aiOpen) { setAiOpen(false); return }
        onClose()
      }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [open, onClose, aiOpen])

  // 每次打开教程时重置状态
  useEffect(() => {
    if (!open) return
    setAiOpen(false)
    setCurrentStep(0)

    // ═══ 【主动引导 - v2 带重试版】打开教程时检查 AI 配置状态 ═══
    // v1 缺陷: if (bridge?.invoke) 静默跳过 —— bridge 未 ready 时整个引导链崩了
    // v2 修复: 等 bridge 最多 5s（每 100ms 轮询一次），任何结果都上报 FE-DIAG
    //   - bridge 未 ready 或 invoke 失败 → 不阻塞教程，但把错误上报到 C#
    //   - 后端返回 configured=false → 自动打开 AI 抽屉
    //   - 后端返回 configured=true  → 不打开（用户已配 Key）
    const MAX_WAIT_MS = 5000
    const POLL_MS = 100
    const START = Date.now()
    let cancelled = false

    function logDiag(msg: string): void {
      try {
        const bridge = (window as unknown as {
          __msmc_bridge__?: { invoke?: (a: string, p: unknown) => Promise<unknown> }
        }).__msmc_bridge__
        bridge?.invoke?.('log:write', {
          level: 'Information',
          message: `[FE-DIAG][AI-GUIDE] ${msg}`,
        }).catch(() => {})
      } catch {}
      try { console.log('[AI-GUIDE]', msg) } catch {}
    }

    async function tryGetAiStatus(attempt: number): Promise<void> {
      const bridge = (window as unknown as {
        __msmc_bridge__?: { invoke?: (a: string, p: unknown) => Promise<unknown> }
      }).__msmc_bridge__

      if (cancelled) return

      if (!bridge?.invoke) {
        const elapsed = Date.now() - START
        if (elapsed >= MAX_WAIT_MS) {
          logDiag(`bridge 未在 ${MAX_WAIT_MS}ms 内就绪，放弃引导（attempt=${attempt}）`)
          return
        }
        if (attempt === 1 || attempt % 5 === 0) {
          logDiag(`bridge 未就绪，等待中... (attempt=${attempt}, elapsed=${elapsed}ms)`)
        }
        setTimeout(() => tryGetAiStatus(attempt + 1), POLL_MS)
        return
      }

      try {
        logDiag(`bridge 就绪，调用 diagnostic.getAiStatus (attempt=${attempt})`)
        const resp = (await bridge.invoke('diagnostic.getAiStatus', null)) as { configured?: boolean; hasKey?: boolean } | undefined
        logDiag(`getAiStatus 响应: configured=${resp?.configured}, hasKey=${resp?.hasKey}`)
        if (!cancelled && resp && resp.configured === false) {
          logDiag(`AI 未配置 Key → 自动打开 AI 抽屉`)
          setAiOpen(true)
        } else if (resp?.configured === true) {
          logDiag(`AI 已配置 Key，不自动打开抽屉`)
        } else {
          logDiag(`响应异常，不自动打开: ${JSON.stringify(resp)}`)
        }
      } catch (err: unknown) {
        logDiag(`getAiStatus 抛异常: ${err instanceof Error ? err.message : String(err)}`)
      }
    }

    tryGetAiStatus(1)
    return () => { cancelled = true }
  }, [open])

  if (!open) return null

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label="我不会开服 - 新手教程"
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 9999,
        background: 'var(--md-loading-overlay)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 24,
      }}
      onClick={onClose}
    >
      <div
        className="md-card md-card-elevated"
        style={{
          width: aiOpen ? 1200 : 720,
          maxWidth: '100%',
          height: '90vh',
          maxHeight: '92vh',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
          transition: 'width 0.25s ease-out',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* ── 头部：标题 + AI 开关 + 关闭 ── */}
        <div
          className="flex items-center flex-shrink-0"
          style={{
            padding: '14px 18px',
            borderBottom: '1px solid var(--md-card-subtle-border)',
            gap: 10,
          }}
        >
          <div style={{ fontSize: 20 }}>🤓</div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div
              style={{
                fontSize: 15,
                fontWeight: 700,
                color: 'var(--md-body)',
              }}
            >
              我不会开服 —— 从零到"有朋友来玩"的超简单教程
            </div>
            <div style={{ fontSize: 11, color: 'var(--md-body-light)', marginTop: 2 }}>
              全程大白话，跟着点就完事了 😋 大概需要 8 分钟，比看一集动画片还快
            </div>
          </div>
          {/* AI 诊断总开关 */}
          <button
            onClick={() => setAiOpen(v => !v)}
            className="md-btn md-btn-flat"
            style={{
              fontSize: 12,
              padding: '6px 12px',
              background: aiOpen
                ? 'color-mix(in srgb, var(--md-primary) 22%, transparent)'
                : 'transparent',
              border: '1px solid var(--md-card-subtle-border)',
              color: aiOpen ? 'var(--md-primary-hue-mid)' : 'var(--md-body-light)',
              whiteSpace: 'nowrap',
            }}
            title="让 AI 帮你诊断开服问题"
          >
            {aiOpen ? '🤖 AI 诊断已开启' : '🤖 让 AI 帮我'}
          </button>
          <button
            onClick={onClose}
            className="md-btn md-btn-flat md-btn-icon"
            title="关闭教程（按 Esc 也行）"
            style={{ fontSize: 16 }}
          >
            ✕
          </button>
        </div>

        {/* ── 主体：教程左 60% + AIDrawer 右 40% ── */}
        <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
          {/* 教程左栏 */}
          <div style={{
            flex: aiOpen ? '0 0 60%' : '1 1 auto',
            overflowY: 'auto',
            padding: '14px 18px 18px',
            transition: 'flex-basis 0.25s ease-out',
          }}>
          {STEPS.map((step, idx) => (
            <div
              key={idx}
              className="md-card"
              style={{
                marginBottom: 12,
                padding: 12,
                background: 'var(--md-card-hover)',
              }}
            >
              <div className="flex items-start" style={{ gap: 10 }}>
                <div
                  className="flex-shrink-0 flex items-center justify-center"
                  style={{
                    width: 34,
                    height: 34,
                    borderRadius: 'var(--md-radius-small)',
                    background: 'var(--md-primary-tint-soft, color-mix(in srgb, var(--md-primary) 15%, transparent))',
                    fontSize: 17,
                  }}
                >
                  {step.icon}
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--md-body)', marginBottom: 6 }}>
                    {step.title}
                  </div>
                  {step.lines.map((line, li) => (
                    <div
                      key={li}
                      style={{
                        fontSize: 12,
                        lineHeight: 1.65,
                        color: 'var(--md-body)',
                        opacity: 0.88,
                        marginBottom: 3,
                      }}
                    >
                      {line}
                    </div>
                  ))}
                  {/* 👉 行动提示 */}
                  {step.action && (
                    <div
                      style={{
                        marginTop: 8,
                        padding: '6px 10px',
                        borderRadius: 'var(--md-radius-small)',
                        background: 'color-mix(in srgb, var(--md-primary) 12%, transparent)',
                        fontSize: 11.5,
                        fontWeight: 600,
                        color: 'var(--md-primary-hue-mid)',
                      }}
                    >
                      👉 {step.action}
                    </div>
                  )}
                  {/* 🤖 梦幻联动：一键让 AI 诊断当前步骤 */}
                  <button
                    onClick={() => { setCurrentStep(idx); setAiOpen(true) }}
                    style={{
                      marginTop: 8,
                      padding: '4px 10px',
                      fontSize: 11,
                      fontWeight: 600,
                      borderRadius: 'var(--md-radius-small)',
                      background: currentStep === idx && aiOpen
                        ? 'color-mix(in srgb, var(--md-primary) 25%, transparent)'
                        : 'transparent',
                      border: '1px dashed var(--md-primary-hue-mid, var(--md-primary))',
                      color: 'var(--md-primary-hue-mid, var(--md-primary))',
                      cursor: 'pointer',
                    }}
                    title={`AI 会针对「${step.title}」这一步给你诊断建议`}
                  >
                    🤖 这步不会？让 AI 帮我
                  </button>
                </div>
              </div>
            </div>
          ))}

          {/* ── 结尾 ── */}
          <div
            className="md-card"
            style={{
              padding: 16,
              textAlign: 'center',
              background: 'linear-gradient(135deg, color-mix(in srgb, var(--md-primary) 18%, var(--md-card-background)), var(--md-card-background))',
            }}
          >
            <div style={{ fontSize: 22, marginBottom: 6 }}>🎉</div>
            <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--md-body)', marginBottom: 6 }}>
              你已经看完啦！剩下的就是动手点一点 🔥
            </div>
            <div style={{ fontSize: 11.5, color: 'var(--md-body-light)', lineHeight: 1.6 }}>
              记住这句话：先体检、再启动、崩了找疑难解答，朋友进不来就查防火墙 😡👍
              <br />
              祝你的服务器天天有人来玩，永不断档 😋
            </div>
            <div style={{ display: 'flex', gap: 10, justifyContent: 'center', marginTop: 14 }}>
              <button
                onClick={() => { setCurrentStep(STEPS.length - 1); setAiOpen(true) }}
                className="md-btn md-btn-flat"
                style={{ fontSize: 12, padding: '6px 16px', border: '1px dashed' }}
              >
                🤖 还不会？让 AI 全流程诊断
              </button>
              <button
                onClick={onClose}
                className="md-btn md-btn-primary"
                style={{ minHeight: 34, padding: '6px 22px' }}
              >
                懂了，我去开服了！🚀
              </button>
            </div>
          </div>
          </div>
          {/* /教程左栏 */}

          {/* AI 诊断右栏 */}
          {aiOpen && (
            <AIDrawer
              open={aiOpen}
              tutorialStep={currentStep}
              onClose={() => setAiOpen(false)}
            />
          )}
        </div>
        {/* /主体 flex */}
      </div>
    </div>
  )
}