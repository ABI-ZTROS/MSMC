import { NavLink } from 'react-router-dom'
import { useAppStore } from '@/stores/appStore'
import { clsx } from 'clsx'

interface NavItem {
  path: string
  label: string
  icon: string
  badge?: string
  badgeColor?: 'success' | 'warning' | 'danger' | 'primary'
}

const navItems: NavItem[] = [
  { path: '/', label: '服务器管理', icon: '🎮' },
  { path: '/config', label: '配置编辑', icon: '⚙️' },
  { path: '/system', label: '系统监控', icon: '📊', badge: '实时', badgeColor: 'success' },
  { path: '/network', label: '网络监控', icon: '🌐' },
  { path: '/settings', label: '设置', icon: '🔧' },
]

const badgeColorMap = {
  success: 'bg-success-500',
  warning: 'bg-warning-500',
  danger: 'bg-danger-500',
  primary: 'bg-primary-500',
}

export function Sidebar(): JSX.Element {
  const collapsed = useAppStore((s) => s.sidebarCollapsed)
  const toggleSidebar = useAppStore((s) => s.toggleSidebar)
  const version = useAppStore((s) => s.version)

  return (
    <aside
      className={clsx(
        'h-full flex flex-col glass-strong border-r border-white/30 dark:border-slate-700/40 transition-all duration-500 ease-smooth relative overflow-hidden',
        collapsed ? 'w-[72px]' : 'w-64'
      )}
      onMouseEnter={() => collapsed && toggleSidebar()}
      onMouseLeave={() => !collapsed && toggleSidebar()}
    >
      <div className="absolute inset-0 bg-grid opacity-30 pointer-events-none" />

      <div className="relative z-10 flex flex-col h-full">
        <div className="p-4 pb-3">
          <div className="flex items-center gap-3">
            <div className="w-11 h-11 rounded-2xl bg-gradient-to-br from-primary-400 via-primary-500 to-accent-500 flex items-center justify-center text-white font-bold text-lg shadow-lg shadow-primary-500/30 flex-shrink-0 relative">
              <span className="relative z-10">M</span>
              <div className="absolute inset-0 rounded-2xl bg-white/20 animate-pulse-glow" />
            </div>
            <div
              className={clsx(
                'flex-1 min-w-0 transition-all duration-400 ease-smooth',
                collapsed ? 'w-0 opacity-0 scale-95' : 'w-auto opacity-100 scale-100'
              )}
            >
              <div className="text-base font-bold text-slate-900 dark:text-white whitespace-nowrap">
                MSMC
              </div>
              <div className="text-xs text-slate-500 dark:text-slate-400 whitespace-nowrap">
                服务器管理控制台
              </div>
            </div>
          </div>
        </div>

        <div className={clsx('px-3 transition-all duration-300', collapsed ? 'opacity-0 h-0' : 'opacity-100 h-auto')}>
          <div className="divider mb-2" />
        </div>

        <nav className="flex-1 px-3 py-2 overflow-y-auto overflow-x-hidden">
          <div className="space-y-1">
            {navItems.map((item, index) => (
              <NavLink
                key={item.path}
                to={item.path}
                end={item.path === '/'}
                className={({ isActive }) =>
                  clsx(
                    'nav-item',
                    isActive ? 'nav-item-active' : 'nav-item-inactive',
                    collapsed && 'justify-center px-0'
                  )
                }
                title={item.label}
                style={{ animationDelay: `${index * 50}ms` }}
              >
                <span className="text-xl flex-shrink-0 transition-transform duration-300 group-hover:scale-110">
                  {item.icon}
                </span>
                <span
                  className={clsx(
                    'flex-1 transition-all duration-400 ease-smooth whitespace-nowrap overflow-hidden',
                    collapsed ? 'w-0 opacity-0' : 'w-auto opacity-100'
                  )}
                >
                  {item.label}
                </span>
                {item.badge && (
                  <span
                    className={clsx(
                      'flex-shrink-0 px-2 py-0.5 text-[10px] font-semibold rounded-full text-white',
                      badgeColorMap[item.badgeColor || 'primary'],
                      collapsed ? 'hidden' : 'inline-block'
                    )}
                  >
                    {item.badge}
                  </span>
                )}
              </NavLink>
            ))}
          </div>
        </nav>

        <div className="px-3 py-3 border-t border-white/20 dark:border-slate-700/40">
          <div
            className={clsx(
              'flex items-center gap-3 px-3 py-2.5 rounded-xl transition-all duration-300',
              collapsed ? 'justify-center px-0' : ''
            )}
          >
            <div className="w-9 h-9 rounded-xl bg-gradient-to-br from-accent-400 to-primary-500 flex items-center justify-center text-white text-sm font-semibold flex-shrink-0">
              U
            </div>
            <div
              className={clsx(
                'flex-1 min-w-0 transition-all duration-400 ease-smooth',
                collapsed ? 'w-0 opacity-0' : 'w-auto opacity-100'
              )}
            >
              <div className="text-sm font-medium text-slate-800 dark:text-slate-200 truncate">
                用户
              </div>
              <div className="text-xs text-slate-500 dark:text-slate-400 truncate">
                v{version}
              </div>
            </div>
          </div>
        </div>
      </div>
    </aside>
  )
}
