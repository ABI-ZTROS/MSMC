// -----------------------------------------------------------------------------
// 文件名: AIDrawer.tsx
// 功能: Function Calling AI 诊断右侧抽屉 —— 流式显示工具执行日志 + AI 分析结果 + 修复按钮
// 位置: TutorialOverlay 左 60% + AIDrawer 右 40% 并排
// -----------------------------------------------------------------------------
import { useEffect, useRef, useState } from "react";
import type { MsmcBridge } from "@/utils/bridge";

interface Props {
  open: boolean;
  tutorialStep?: number;
  serverPath?: string | null;
  onClose?: () => void;
}

interface ToolLogEntry {
  id: number;
  toolName: string;
  status: "started" | "done" | "failed";
  elapsedMs?: number;
  round: number;
  error?: string;
}

interface RecommendedAction {
  fixId: string;
  label: string;
  dangerous: boolean;
  rationale?: string;
}

interface DeepSeekAnalysis {
  summary: string;
  keyFindings: string[];
  recommendedActions: RecommendedAction[];
  needMoreInfo: boolean;
  suggestedQuestions?: string[];
}

type BridgeLike = Pick<MsmcBridge, "sendEvent" | "on" | "invoke" | "invokeWithTimeout">;

export function AIDrawer({ open, tutorialStep, serverPath, onClose }: Props) {
  const [toolLogs, setToolLogs] = useState<ToolLogEntry[]>([]);
  const [analysis, setAnalysis] = useState<DeepSeekAnalysis | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [needsConfig, setNeedsConfig] = useState(false);
  const [pendingApiKey, setPendingApiKey] = useState("");
  const [savingKey, setSavingKey] = useState(false);
  const [sending, setSending] = useState(false);
  const [question, setQuestion] = useState("");
  const logIdRef = useRef(0);


  // ── 全链路 AI 引导日志辅助（统一前缀 [AI-GUIDE]）
  function logDiag(msg: string): void {
    try { console.log('[AI-GUIDE][AIDrawer]', msg) } catch {}
    try {
      const bridge = (window as unknown as { __msmc_bridge__?: BridgeLike }).__msmc_bridge__
      bridge?.invoke?.('log:write', {
        level: 'Information',
        message: `[AI-GUIDE][AIDrawer] ${msg}`,
      }).catch(() => {})
    } catch {}
  }

  // 触发一次 Function Calling 诊断
  function runDiagnostic(userQuestion?: string) {
    logDiag(`runDiagnostic 入口, isFollowUp=${typeof userQuestion === "string" && userQuestion.length > 0}, questionLen=${userQuestion?.length ?? 0}`)
    const bridge = (window as unknown as { __msmc_bridge__?: BridgeLike }).__msmc_bridge__;
    if (!bridge) {
      logDiag("❌ bridge 不可用 — 放弃诊断")
      setError("Bridge 不可用（WebView2 可能还没初始化）");
      setSending(false);
      return;
    }

    const isFollowUp = typeof userQuestion === "string" && userQuestion.length > 0;
    const action = isFollowUp ? "troubleshooting.aiSend" : "troubleshooting.aiInit";
    const payload = isFollowUp
      ? { message: userQuestion }
      : { tutorialStep, selectedServerPath: serverPath ?? undefined };

    // 优先用 invoke（后端 SafeRegister 是同步 response 模式），
    // 如果没有 invoke 就退化用 sendEvent + 依赖 aiDone/aiError event
    if (typeof bridge.invoke === "function") {
      logDiag(`🟢 调用 bridge.invoke("${action}", ${JSON.stringify(payload)})`) 
      bridge.invoke<Record<string, unknown>>(action, payload)
        .then(resp => {
          const success = Boolean(resp?.success);
          const err = typeof resp?.error === "string" ? resp.error : null;
          const ana = resp?.analysis as unknown as DeepSeekAnalysis | undefined;
          const needsCfg = Boolean(resp?.needsConfig);
          logDiag(`📥 ${action} 响应: success=${success}, needsConfig=${needsCfg}, error=${err ?? "(none)"}, hasAnalysis=${!!ana}`)

          if (needsCfg) {
            logDiag("🔑 needsConfig=true → 渲染配置卡（用户粘贴 API Key）")
            setNeedsConfig(true);
            setError(null);
            setAnalysis(null);
          } else if (success && ana) {
            logDiag("✅ 有 analysis → 渲染 AI 分析结果")
            setAnalysis(ana);
            setError(null);
            setNeedsConfig(false);
          } else if (err) {
            logDiag(`❌ 返回 error → ${err}`)
            setError(err);
          } else {
            logDiag("❌ AI 返回空结果")
            setError("AI 返回空结果");
          }
        })
        .catch((e: unknown) => {
          const msg = e instanceof Error ? e.message : String(e)
          logDiag(`❌ invoke 抛异常 → ${msg}`)
          setError(msg);
        })
        .finally(() => {
          setSending(false);
        });
    } else {
      bridge.sendEvent(action, payload);
      // 等 aiDone/aiError event 来收尾
    }
  }

  useEffect(() => {
    if (!open) return;

    logDiag(`🗂️  AIDrawer 抽屉打开 — useEffect 触发, tutorialStep=${tutorialStep ?? "(none)"}, serverPath=${serverPath ?? "(none)"}`)

    // Reset state
    setToolLogs([]);
    setAnalysis(null);
    setError(null);
    setNeedsConfig(false);
    setSending(true);
    logIdRef.current = 0;
    logDiag("✅ State 重置完成 — 准备调 runDiagnostic")

    const bridge = (window as unknown as { __msmc_bridge__?: BridgeLike }).__msmc_bridge__;
    let offFns: Array<() => void> = [];

    if (bridge?.on) {
      // 事件监听：工具执行过程（FunctionCallingEngine 主动推的流式日志）
      offFns.push(bridge.on("troubleshooting.aiToolExec", (payload) => {
        const d = (payload as Record<string, unknown>) ?? {};
        setToolLogs(prev => [...prev, {
          id: ++logIdRef.current,
          toolName: String(d.toolName ?? "unknown"),
          status: "started",
          round: Number(d.round ?? 0),
        }]);
      }));

      offFns.push(bridge.on("troubleshooting.aiStream", (payload) => {
        const d = (payload as Record<string, unknown>) ?? {};
        setToolLogs(prev => prev.map(l =>
          l.toolName === String(d.toolName) && l.round === Number(d.round) && l.status === "started"
            ? {
                ...l,
                status: (d.status as "done" | "failed") ?? "done",
                elapsedMs: typeof d.elapsedMs === "number" ? d.elapsedMs : undefined,
                error: typeof d.error === "string" ? d.error : undefined,
              }
            : l));
      }));

      // 兜底：如果后端也推 aiDone/aiError event（除了 invoke 返回之外），也能接住
      offFns.push(bridge.on("troubleshooting.aiDone", (payload) => {
        const resp = (payload as Record<string, unknown>) ?? {};
        if (resp.analysis) {
          setAnalysis(resp.analysis as unknown as DeepSeekAnalysis);
        } else {
          setAnalysis(resp as unknown as DeepSeekAnalysis);
        }
        setSending(false);
      }));

      offFns.push(bridge.on("troubleshooting.aiError", (payload) => {
        const d = (payload as Record<string, unknown>) ?? {};
        setError(String(d.message ?? "未知错误"));
        setSending(false);
      }));
    } else {
      // Fallback：DOM event 模式（某些 Bridge 版本会把 event 挂在 window 上）
      const handler = (e: Event) => {
        const detail = (e as CustomEvent).detail as Record<string, unknown> | undefined;
        if (!detail) return;
        const type = String(detail.eventType ?? "");
        const data = (detail.data as Record<string, unknown>) ?? {};

        if (type === "troubleshooting.aiToolExec") {
          setToolLogs(prev => [...prev, {
            id: ++logIdRef.current,
            toolName: String(data.toolName ?? "unknown"),
            status: "started",
            round: Number(data.round ?? 0),
          }]);
        } else if (type === "troubleshooting.aiStream") {
          setToolLogs(prev => prev.map(l =>
            l.toolName === String(data.toolName) && l.round === Number(data.round) && l.status === "started"
              ? {
                  ...l,
                  status: (data.status as "done" | "failed") ?? "done",
                  elapsedMs: typeof data.elapsedMs === "number" ? data.elapsedMs : undefined,
                  error: typeof data.error === "string" ? data.error : undefined,
                }
              : l));
        } else if (type === "troubleshooting.aiDone") {
          const resp = data as unknown as { success?: boolean; analysis?: DeepSeekAnalysis };
          if (resp?.analysis) setAnalysis(resp.analysis);
          else setAnalysis(data as unknown as DeepSeekAnalysis);
          setSending(false);
        } else if (type === "troubleshooting.aiError") {
          setError(String(data.message ?? "未知错误"));
          setSending(false);
        }
      };
      window.addEventListener("msmc-bridge-event", handler);
      offFns.push(() => window.removeEventListener("msmc-bridge-event", handler));
    }

    // 启动 Function Calling 诊断
    runDiagnostic();

    return () => {
      offFns.forEach(off => { try { off() } catch { /* ignore */ } });
      // 不发 aiStop——因为 runDiagnostic 是一次性 invoke，后端没有持续 CTS 需要取消
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  function handleSendQuestion() {
    const q = question.trim();
    if (!q) return;
    setSending(true);
    setError(null);
    setAnalysis(null);
    runDiagnostic(q);
    setQuestion("");
  }

  function handleConfirmFix(fixId: string) {
    const bridge = (window as unknown as { __msmc_bridge__?: BridgeLike }).__msmc_bridge__;
    if (typeof bridge?.invoke === "function") {
      bridge.invoke<Record<string, unknown>>("troubleshooting.confirmFix", {
        fixId,
        payload: { serverPath },
      }).then(() => {
        // 可以在这里刷新分析结果或显示成功提示
      }).catch(() => { /* ignore */ });
    } else {
      bridge?.sendEvent?.("troubleshooting.confirmFix", {
        fixId,
        payload: { serverPath },
      });
    }
  }

  async function handleSaveApiKeyAndRetry() {
    const key = pendingApiKey.trim();
    if (!key) { setError("API Key 不能为空"); return; }
    setSavingKey(true);
    setError(null);

    const bridge = (window as unknown as { __msmc_bridge__?: BridgeLike }).__msmc_bridge__;
    if (!bridge || typeof bridge.invoke !== "function") {
      setError("Bridge 不可用，无法保存 API Key");
      setSavingKey(false);
      return;
    }

    try {
      const resp = await bridge.invoke<Record<string, unknown>>(
        "diagnostic.setApiKey", { apiKey: key });
      const ok = Boolean(resp?.success);
      const errMsg = typeof resp?.error === "string" ? resp.error : null;

      if (!ok) {
        setError(errMsg ?? "保存 API Key 失败");
        setSavingKey(false);
        return;
      }

      // 保存成功 → 清 state → 重新跑诊断
      setNeedsConfig(false);
      setPendingApiKey("");
      setSending(true);
      setError(null);
      runDiagnostic();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setSavingKey(false);
    }
  }

  if (!open) return null;

  return (
    <div style={styles.drawer}>
      <div style={styles.header}>
        <span>🤖 MSMC AI 诊断</span>
        <button onClick={onClose} style={styles.closeBtn} title="关闭 AI 抽屉">×</button>
      </div>

      {needsConfig && (
        <div style={styles.configCard}>
          <div style={styles.configIcon}>🔑</div>
          <div style={styles.configTitle}>需要配置 DeepSeek API Key</div>
          <div style={styles.configDesc}>
            MSMC 使用 DeepSeek Function Calling 自主调用联网搜索、读日志、下载核心等工具帮你诊断。
            请前往 <a href="https://platform.deepseek.com/api_keys" target="_blank" rel="noreferrer" style={styles.linkStyle}>platform.deepseek.com/api_keys</a> 创建 API Key，粘贴到下方。
          </div>
          <input
            type="password"
            autoComplete="off"
            value={pendingApiKey}
            onChange={e => setPendingApiKey(e.target.value)}
            placeholder="sk-xxxxxxxxxxxxxxxxxxxxxxxx"
            style={styles.configInput}
            onKeyDown={e => e.key === "Enter" && handleSaveApiKeyAndRetry()}
          />
          <button
            style={{ ...styles.confirmBtn, opacity: savingKey ? 0.6 : 1, cursor: savingKey ? "wait" : "pointer" }}
            onClick={handleSaveApiKeyAndRetry}
            disabled={savingKey}
          >
            {savingKey ? "保存中..." : "保存并开始诊断"}
          </button>
          <div style={styles.configHint}>
            🔒 API Key 仅保存在本机（%APPDATA%/MSMC），不会上传。
          </div>
        </div>
      )}

      <div style={styles.section}>
        <div style={styles.sectionTitle}>工具执行日志</div>
        <div style={styles.logContainer}>
          {toolLogs.map(l => (
            <div key={l.id} style={{ color: l.status === "failed" ? "#e8964a" : l.status === "done" ? "#5DC8E8" : "#888" }}>
              {l.status === "started" ? "🔄" : l.status === "failed" ? "❌" : "✅"}
              {" "}round {l.round}: <code>{l.toolName}</code>
              {l.elapsedMs != null ? ` (${l.elapsedMs}ms)` : ""}
              {l.error ? <span style={{ color: "#c0392b", marginLeft: 8 }}>⚠ {l.error}</span> : null}
            </div>
          ))}
          {sending && toolLogs.length === 0 && (
            <div style={{ color: "#888", fontStyle: "italic" }}>正在启动 Function Calling 诊断...</div>
          )}
          {!sending && toolLogs.length === 0 && !analysis && !error && (
            <div style={{ color: "#888", fontStyle: "italic" }}>AI 未执行任何工具（可能不需要）</div>
          )}
        </div>
      </div>

      {analysis && (
        <>
          <div style={styles.section}>
            <div style={styles.sectionTitle}>💡 AI 诊断结论</div>
            <div style={styles.summary}>{analysis.summary}</div>
            {analysis.keyFindings.length > 0 && (
              <ul style={{ paddingLeft: 20, marginTop: 8 }}>
                {analysis.keyFindings.map((f, i) => (
                  <li key={i} style={{ margin: "4px 0", color: "#e2e8f0" }}>{f}</li>
                ))}
              </ul>
            )}
          </div>

          {analysis.recommendedActions.length > 0 && (
            <div style={styles.section}>
              <div style={styles.sectionTitle}>🔧 修复建议（FixPanel）</div>
              {analysis.recommendedActions.map((a, i) => (
                <div key={i} style={styles.actionCard}>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                    <strong>{a.label}</strong>
                    <span style={{
                      color: a.dangerous ? "#c0392b" : "#5DC8E8",
                      fontSize: 11,
                      padding: "2px 6px",
                      background: a.dangerous ? "rgba(192,57,43,0.15)" : "rgba(93,200,232,0.15)",
                      borderRadius: 4
                    }}>{a.dangerous ? "⚠ 危险" : "安全"}</span>
                  </div>
                  <div style={{ fontSize: 12, color: "#888", marginTop: 4 }}>
                    <code style={{ color: "#5DC8E8" }}>{a.fixId}</code>
                    {a.rationale ? ` — ${a.rationale}` : ""}
                  </div>
                  <button
                    style={styles.confirmBtn}
                    onClick={() => handleConfirmFix(a.fixId)}
                  >
                    确认执行修复
                  </button>
                </div>
              ))}
            </div>
          )}

          {analysis.needMoreInfo && analysis.suggestedQuestions && analysis.suggestedQuestions.length > 0 && (
            <div style={styles.section}>
              <div style={styles.sectionTitle}>🤔 AI 想了解更多</div>
              {analysis.suggestedQuestions.map((q, i) => (
                <div
                  key={i}
                  style={styles.suggestionChip}
                  onClick={() => setQuestion(q)}
                >{q}</div>
              ))}
            </div>
          )}
        </>
      )}

      {error && (
        <div style={styles.error}>❌ {error}</div>
      )}

      <div style={styles.inputBar}>
        <input
          value={question}
          onChange={e => setQuestion(e.target.value)}
          placeholder="追问 AI..."
          onKeyDown={e => e.key === "Enter" && handleSendQuestion()}
          style={styles.input}
        />
      </div>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  drawer: {
    width: "40%",
    height: "100%",
    background: "#020617",
    borderLeft: "1px solid #1e293b",
    overflow: "auto",
    display: "flex",
    flexDirection: "column",
    fontFamily: "-apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif"
  },
  header: {
    padding: "16px",
    borderBottom: "1px solid #1e293b",
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    color: "#e2e8f0",
    fontSize: 14,
    fontWeight: 600
  },
  closeBtn: {
    background: "none",
    border: "none",
    color: "#888",
    fontSize: 22,
    cursor: "pointer",
    lineHeight: 1
  },
  section: { padding: "12px 16px", borderBottom: "1px solid #1e293b" },
  sectionTitle: { fontSize: 12, color: "#888", marginBottom: 8, textTransform: "uppercase", letterSpacing: 1 },
  logContainer: { fontFamily: "Consolas, monospace", fontSize: 12, lineHeight: 2, maxHeight: 200, overflow: "auto" },
  summary: { color: "#e2e8f0", marginTop: 4, lineHeight: 1.6 },
  actionCard: {
    margin: "8px 0",
    padding: 12,
    background: "#0d1b2a",
    borderRadius: 6,
    border: "1px solid #1e293b"
  },
  confirmBtn: {
    marginTop: 8,
    padding: "6px 16px",
    background: "#5DC8E8",
    color: "#020617",
    border: "none",
    borderRadius: 4,
    cursor: "pointer",
    fontWeight: 500
  },
  suggestionChip: {
    margin: "4px 0",
    padding: "6px 12px",
    background: "#0d1b2a",
    borderRadius: 16,
    fontSize: 13,
    color: "#5DC8E8",
    cursor: "pointer",
    border: "1px solid #1e293b"
  },
  error: {
    padding: "12px 16px",
    color: "#c0392b",
    background: "rgba(192,57,43,0.1)",
    margin: "8px 16px",
    borderRadius: 6
  },
  inputBar: {
    padding: "12px 16px",
    borderTop: "1px solid #1e293b",
    marginTop: "auto"
  },
  input: {
    width: "100%",
    padding: "8px 12px",
    background: "#0d1b2a",
    border: "1px solid #1e293b",
    borderRadius: 6,
    color: "#e2e8f0",
    outline: "none",
    boxSizing: "border-box",
    fontSize: 14
  },
  configCard: {
    margin: 16,
    padding: 20,
    background: "linear-gradient(135deg, #0d1b2a 0%, #1a2b3c 100%)",
    borderRadius: 10,
    border: "1px solid #1e3a5f",
    textAlign: "center" as const
  },
  configIcon: { fontSize: 32, marginBottom: 8 },
  configTitle: { fontSize: 16, fontWeight: 600, color: "#5DC8E8", marginBottom: 8 },
  configDesc: { fontSize: 12, color: "#888", lineHeight: 1.6, marginBottom: 14, textAlign: "left" as const },
  linkStyle: { color: "#5DC8E8", textDecoration: "underline" },
  configInput: {
    width: "100%",
    padding: "10px 14px",
    background: "#020617",
    border: "1px solid #1e3a5f",
    borderRadius: 6,
    color: "#e2e8f0",
    outline: "none",
    boxSizing: "border-box",
    fontSize: 13,
    fontFamily: "Consolas, monospace",
    marginBottom: 10
  },
  configHint: { fontSize: 11, color: "#555", marginTop: 10 }
};
