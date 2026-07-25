import { useState } from 'react'
import { NavLink, useLocation } from 'react-router-dom'
import { clsx } from 'clsx'
import {
  FaServer,
  FaSliders,
  FaChartLine,
  FaNetworkWired,
  FaCog,
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
  { path: '/settings', label: '设置', icon: <FaCog size={16} /> },
]

export function Sidebar() {
  const [expanded, setExpanded] = useState(false)
  const location = useLocation()

  return (
    <aside
      className={clsx(
        'h-full flex flex-col bg-[var(--md-card-background)] border-r border-[var(--md-card-subtle-border)] relative',
        'transition-[width] duration-300 ease-[var(--md-ease-standard)]'
      )}
      style={{
        width: expanded ? 'var(--sidebar-width-expanded)' : 'var(--sidebar-width-collapsed)',
      }}
      onMouseEnter={() => setExpanded(true)}
      onMouseLeave={() => setExpanded(false)}
    >
      <div className="p-3">
        <div className="flex items-center gap-3">
          <div
            className="w-8 h-8 flex items-center justify-center flex-shrink-0 rounded-md"
            style={{ backgroundColor: 'var(--md-primary-subtle-background)' }}
          >
            <FaShield size={16} style={{ color: 'var(--md-nav-item-selected)' }} />
          </div>
          <div
            className={clsx(
              'flex-1 min-w-0 transition-all duration-200 overflow-hidden',
              expanded ? 'opacity-100 w-auto' : 'opacity-0 w-0'
            )}
          >
            <div className="text-sm font-bold text-[var(--md-body)] whitespace-nowrap">MSMC</div>
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

      <nav className="flex-1 px-1 py-2 overflow-y-auto">
        <div className="space-y-0">
          {navItems.map((item) => {
            const isActive =
              item.path === '/'
                ? location.pathname === '/'
                : location.pathname.startsWith(item.path)

            return (
              <NavLink
                key={item.path}
                to={item.path}
                end={item.path === '/'}
                className={clsx('md-nav-item', isActive && 'md-nav-item-active')}
                title={item.label}
                style={!expanded ? { justifyContent: 'center', paddingLeft: 0, paddingRight: 0 } : {}}
              >
                <span className="flex-shrink-0">{item.icon}</span>
                <span
                  className={clsx(
                    'whitespace-nowrap transition-all duration-200 overflow-hidden',
                    expanded ? 'opacity-100 w-auto' : 'opacity-0 w-0'
                  )}
                >
                  {item.label}
                </span>
                {expanded && isActive && (
                  <FaChevronRight
                    size={10}
                    className="ml-auto"
                    style={{ color: 'var(--md-nav-item-selected)' }}
                  />
                )}
              </NavLink>
            )
          })}
        </div>
      </nav>

      <div className="p-3 border-t border-[var(--md-card-subtle-border)]">
        <div
          className={clsx(
            'flex items-center gap-2 px-2 py-2 rounded-md',
            expanded ? '' : 'justify-center px-1'
          )}
          style={{ backgroundColor: 'var(--md-primary-subtle-background)' }}
        >
          <FaShield
            size={14}
            className="flex-shrink-0"
            style={{ color: 'var(--md-nav-item-selected)' }}
          />
          <div
            className={clsx(
              'transition-all duration-200 overflow-hidden',
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
