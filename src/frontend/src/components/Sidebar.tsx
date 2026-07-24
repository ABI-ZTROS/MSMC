import { useState } from 'react'
import { NavLink } from 'react-router-dom'
import { clsx } from 'clsx'

interface NavItem {
  path: string
  label: string
  icon: string
}

const navItems: NavItem[] = [
  { path: '/', label: '服务器管理', icon: '🎮' },
  { path: '/config', label: '配置编辑', icon: '📝' },
  { path: '/system', label: '系统监控', icon: '📊' },
  { path: '/network', label: '网络监控', icon: '🌐' },
  { path: '/settings', label: '设置', icon: '⚙️' },
]

export function Sidebar(): JSX.Element {
  const [expanded, setExpanded] = useState(false)
  const version = '0.1.0'

  return (
    <aside
      className={clsx(
        'h-full flex flex-col bg-slate-900 border-r border-slate-700/50 transition-all duration-300 relative',
        expanded ? 'w-56' : 'w-[56px]'
      )}
      onMouseEnter={() => setExpanded(true)}
      onMouseLeave={() => setExpanded(false)}
    >
      {/* 顶部 Logo 区 */}
      <div className="p-3">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-blue-600/20 flex items-center justify-center flex-shrink-0">
            <span className="text-blue-400 text-sm">🛡️</span>
          </div>
          <div
            className={clsx(
              'flex-1 min-w-0 transition-all duration-200 overflow-hidden',
              expanded ? 'opacity-100 w-auto' : 'opacity-0 w-0'
            )}
          >
            <div className="text-sm font-bold text-white whitespace-nowrap">MSMC</div>
            <div className="text-[10px] text-slate-400 whitespace-nowrap">v{version}</div>
          </div>
        </div>
      </div>

      {/* 分隔线 */}
      <div className="px-3">
        <div className="h-px bg-slate-700/50" />
      </div>

      {/* 导航列表 */}
      <nav className="flex-1 px-1 py-2 overflow-y-auto">
        <div className="space-y-0.5">
          {navItems.map((item) => (
            <NavLink
              key={item.path}
              to={item.path}
              end={item.path === '/'}
              className={({ isActive }) =>
                clsx(
                  'flex items-center gap-3 px-2.5 py-2 rounded-md text-sm font-medium transition-all duration-200',
                  isActive
                    ? 'bg-blue-600/15 text-blue-400'
                    : 'text-slate-300 hover:bg-slate-800 hover:text-white',
                  expanded ? '' : 'justify-center'
                )
              }
              title={item.label}
            >
              <span className="text-base flex-shrink-0">{item.icon}</span>
              <span
                className={clsx(
                  'whitespace-nowrap transition-all duration-200 overflow-hidden',
                  expanded ? 'opacity-100 w-auto' : 'opacity-0 w-0'
                )}
              >
                {item.label}
              </span>
            </NavLink>
          ))}
        </div>
      </nav>

      {/* 底部信息 */}
      <div className="p-3 border-t border-slate-700/50">
        <div
          className={clsx(
            'flex items-center gap-2 px-2 py-2 rounded-md bg-blue-600/10 transition-all duration-200',
            expanded ? '' : 'justify-center px-1'
          )}
        >
          <span className="text-blue-400 text-sm flex-shrink-0">ℹ️</span>
          <div
            className={clsx(
              'transition-all duration-200 overflow-hidden',
              expanded ? 'opacity-100 w-auto' : 'opacity-0 w-0'
            )}
          >
            <div className="text-[11px] text-slate-300 font-medium whitespace-nowrap">MSMC</div>
            <div className="text-[10px] text-slate-500 whitespace-nowrap">服务器管理控制台</div>
          </div>
        </div>
      </div>
    </aside>
  )
}
