import { useState } from 'react'
import { NavLink, useLocation } from 'react-router-dom'
import { clsx } from 'clsx'
import {
  FaServer,
  FaSliders,
  FaChartLine,
  FaNetworkWired,
  FaGear,
  FaShield,
  FaChevronRight,
} from 'react-icons/fa6'

interface NavItem {
  path: string
  label: string
  icon: React.ReactNode
}

const navItems: NavItem[] = [
  { path: '/', label: '服务器管理', icon: <FaServer size={16} /> },
  { path: '/system', label: '系统监控', icon: <FaChartLine size={16} /> },
  { path: '/network', label: '网络监控', icon: <FaNetworkWired size={16} /> },
  { path: '/config', label: '配置编辑', icon: <FaSliders size={16} /> },
  { path: '/settings', label: '设置', icon: <FaGear size={16} /> },
]

export function Sidebar() {
  const [expanded, setExpanded] = useState(false)
  const location = useLocation()

  return (
    <aside
      className={clsx(
        'h-full flex flex-col bg-[var(--md-card-background)] border-r border-[var(--md-card-subtle-border)] relative',
        'md-sidebar-transition'
      )}
      style={{
        width: expanded ? 'var(--sidebar-width-expanded)' : 'var(--sidebar-width-collapsed)',
      }}
      onMouseEnter={() => setExpanded(true)}
      onMouseLeave={() => setExpanded(false)}
    >
      {/* 顶部品牌区 —— ColorOS 风格呼吸光晕 */}
      <div className="p-3 md-stagger-item" style={{ '--md-stagger-i': 0 } as React.CSSProperties}>
        <div
          className={clsx('flex items-center gap-3', expanded ? '' : 'justify-center')}
        >
          <div
            className="w-8 h-8 flex items-center justify-center flex-shrink-0 rounded-md md-brand-pulse relative"
            style={{ backgroundColor: 'var(--md-primary-subtle-background)' }}
          >
            <FaShield size={16} style={{ color: 'var(--md-nav-item-selected)' }} />
            {/* Aquamarine 辅色光晕：ColorOS AOD 流动配色点缀 */}
            <div
              aria-hidden
              className="absolute inset-0 rounded-md"
              style={{
                boxShadow: '0 0 12px 1px var(--md-aquamarine-soft)',
                pointerEvents: 'none',
              }}
            />
          </div>
          <div
            className={clsx(
              'flex-1 min-w-0 md-sidebar-text-transition overflow-hidden',
              expanded ? 'opacity-100 w-auto' : 'opacity-0 w-0'
            )}
          >
            <div className="text-sm font-bold text-[var(--md-body)] whitespace-nowrap">
              MSMC
            </div>
            <div
              className="text-[10px] whitespace-nowrap"
              style={{ color: 'var(--md-body-light)', opacity: 0.7 }}
            >
              v0.1.0
            </div>
          </div>
        </div>
      </div>

      <div className="px-3">
        <div className="h-px bg-[var(--md-subtle-border)] opacity-30" />
      </div>

      {/* 导航列表 —— 交错入场（ColorOS 公式） */}
      <nav className="flex-1 px-1 py-2 overflow-y-auto">
        <div className="space-y-0">
          {navItems.map((item, index) => {
            const isActive =
              item.path === '/'
                ? location.pathname === '/'
                : location.pathname.startsWith(item.path)

            return (
              <NavLink
                key={item.path}
                to={item.path}
                end={item.path === '/'}
                className={clsx(
                  'md-nav-item md-stagger-item',
                  isActive && 'md-nav-item-active'
                )}
                title={item.label}
                style={{
                  // 交错入场延迟（ColorOS 公式由 CSS 计算，这里只传 index）
                  '--md-stagger-i': index + 1,
                  // 折叠态保留 padding 让 ::before 指示器位置正确，仅居中内容
                  justifyContent: expanded ? undefined : 'center',
                } as React.CSSProperties}
              >
                <span
                  className={clsx('flex-shrink-0 md-nav-icon', isActive && 'md-nav-icon-active')}
                >
                  {item.icon}
                </span>
                <span
                  className={clsx(
                    'whitespace-nowrap md-sidebar-text-transition overflow-hidden',
                    expanded ? 'opacity-100 w-auto' : 'opacity-0 w-0'
                  )}
                >
                  {item.label}
                </span>
                {expanded && isActive && (
                  <FaChevronRight
                    size={10}
                    className="ml-auto md-nav-chevron"
                    // ColorOS 辅色：激活态 chevron 用 Aquamarine 点缀
                    style={{ color: 'var(--md-aquamarine-light)' }}
                  />
                )}
              </NavLink>
            )
          })}
        </div>
      </nav>

      {/* 底部信息卡 —— Aquamarine 微光描边 */}
      <div className="p-3 border-t border-[var(--md-card-subtle-border)]">
        <div
          className={clsx(
            'flex items-center gap-2 px-2 py-2 rounded-md md-sidebar-footer',
            expanded ? '' : 'justify-center px-1'
          )}
          style={{
            backgroundColor: 'var(--md-primary-subtle-background)',
            boxShadow: 'inset 0 0 0 1px var(--md-aquamarine-soft)',
          }}
        >
          <FaShield
            size={14}
            className="flex-shrink-0 md-breathe"
            style={{ color: 'var(--md-nav-item-selected)' }}
          />
          <div
            className={clsx(
              'md-sidebar-text-transition overflow-hidden',
              expanded ? 'opacity-100 w-auto' : 'opacity-0 w-0'
            )}
          >
            <div
              className="text-[11px] font-medium whitespace-nowrap"
              style={{ color: 'var(--md-body)' }}
            >
              MSMC
            </div>
            <div
              className="text-[10px] whitespace-nowrap"
              style={{ color: 'var(--md-body-light)', opacity: 0.6 }}
            >
              服务器管理控制台
            </div>
          </div>
        </div>
      </div>
    </aside>
  )
}
