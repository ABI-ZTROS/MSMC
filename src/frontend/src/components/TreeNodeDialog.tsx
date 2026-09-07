// -----------------------------------------------------------------------------
// TreeNodeDialog.tsx — 二叉树症状导航对话
// 从 troubleshootingNodes.ts 常量中按 id 取节点，支持父子跳转
// 血红色强制配色
// -----------------------------------------------------------------------------
import { useState } from 'react'

import { nodeMap } from '@/data/troubleshootingNodes'
import type { TreeNode } from '@/types/bridge'

export interface TreeNodeDialogProps {
  onClose?: () => void
  onCheckReference?: (checkId: string) => void
}

const PRIMARY = '#c0392b'

export function TreeNodeDialog({ onClose, onCheckReference }: TreeNodeDialogProps) {
  const [currentId, setCurrentId] = useState<string>('root')
  const current: TreeNode | undefined = nodeMap[currentId]

  function handleOption(next?: string) {
    if (!next) return
    const nextNode = nodeMap[next]
    if (nextNode) setCurrentId(next)
    else setCurrentId('root')
  }

  function handleBack() {
    // 简易：回到 root（后续可以记录完整路径栈）
    if (currentId !== 'root') setCurrentId('root')
    else onClose?.()
  }

  if (!current) return null

  return (
    <div
      style={{
        position: 'fixed', inset: 0,
        background: 'rgba(0,0,0,0.55)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        zIndex: 1000,
      }}
      onClick={onClose}
    >
      <div
        style={{
          background: 'var(--md-card-background)',
          border: `1px solid ${PRIMARY}`,
          borderRadius: 14,
          width: 480,
          maxWidth: '92vw',
          padding: 22,
          boxShadow: `0 8px 40px ${PRIMARY}40`,
        }}
        onClick={e => e.stopPropagation()}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{
              padding: '3px 10px', borderRadius: 4,
              background: PRIMARY, color: '#fff',
              fontSize: 11, fontWeight: 700,
            }}>
              {current.category}
            </span>
            {current.checkReference && (
              <span
                onClick={() => onCheckReference?.(current.checkReference!)}
                style={{
                  cursor: 'pointer', fontSize: 11, opacity: 0.7,
                  textDecoration: 'underline',
                }}
              >
                🔗 关联检查: {current.checkReference}
              </span>
            )}
          </div>
          <button
            onClick={handleBack}
            style={{
              background: 'transparent', border: 'none',
              cursor: 'pointer', color: 'var(--md-body-light)',
              fontSize: 14, display: 'flex', alignItems: 'center', gap: 4,
            }}
          >
            ← {currentId === 'root' ? '关闭' : '返回'}
          </button>
        </div>

        <div style={{
          fontSize: 16, fontWeight: 600, marginBottom: 18,
          color: 'var(--md-body)', lineHeight: 1.6,
        }}>
          {current.question}
        </div>

        {current.options && current.options.length > 0 && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {current.options.map((opt, i) => (
              <button
                key={i}
                onClick={() => handleOption(opt.next)}
                style={{
                  padding: '10px 14px',
                  borderRadius: 8,
                  border: '1px solid rgba(192,57,43,0.3)',
                  background: 'rgba(192,57,43,0.05)',
                  color: 'var(--md-body)',
                  fontSize: 13,
                  textAlign: 'left',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: 8,
                  transition: 'all 0.15s',
                }}
                onMouseEnter={e => {
                  e.currentTarget.style.background = 'rgba(192,57,43,0.15)'
                  e.currentTarget.style.borderColor = PRIMARY
                }}
                onMouseLeave={e => {
                  e.currentTarget.style.background = 'rgba(192,57,43,0.05)'
                  e.currentTarget.style.borderColor = 'rgba(192,57,43,0.3)'
                }}
              >
                ›
                {opt.label}
                {opt.severity !== undefined && opt.severity >= 3 && (
                  <span style={{ marginLeft: 'auto', fontSize: 10, color: PRIMARY }}>⚠️</span>
                )}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
