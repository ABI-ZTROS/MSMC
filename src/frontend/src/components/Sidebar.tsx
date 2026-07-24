import { NavLink } from 'react-router-dom'
import { useAppStore } from '@/stores/appStore'
import { clsx } from 'clsx'

interface NavItem {
  path: string
  label: string
  icon: string
}

const navItems: NavItem[] = [
  { path: '/', label: '服务器管理', icon: '🎮' },
  { path: '/config', label: '配置编辑', icon: '⚙️' },
  { path: '/system', label: '系统监控', icon: '📊' },
  { path: '/network', label: '网络监控', icon: '🌐' },
  { path: '/settings', label: '设置', icon: '🔧' },
]

export function Sidebar(): JSX.Element {
  const collapsed = useAppStore((s) => s.sidebarCollapsed)
  const toggleSidebar = useAppStore((s) => s.toggleSidebar)

  return (
    <aside
      className={clsx(
        'h-full flex flex-col bg-white dark:bg-slate-800 border-r border-slate-200 dark:border-slate-700 transition-all duration-300 ease-out',
        collapsed ? 'w-16' : 'w-60'
      )}
      onMouseEnter={() => collapsed && toggleSidebar()}
      onMouseLeave={() => !collapsed && toggleSidebar()}
    >
      <div className="p-4 border-b border-slate-200 dark:border-slate-700">
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 rounded-lg bg-primary-100 dark:bg-primary-900/30 flex items-center justify-center text-primary-600 dark:text-primary-400 font-bold text-lg">
            M
          </div>
          <div
            className={clsx(
              'flex-1 overflow-hidden transition-all duration-300',
              collapsed ? 'w-0 opacity-0' : 'w-auto opacity-100'
            )}
          >
            <div className="text-sm font-bold text-slate-900 dark:text-slate-100 whitespace-nowrap">
              MSMC
            </div>
            <div className="text-xs text-slate-500 dark:text-slate-400 whitespace-nowrap">
              服务器管理工具
            </div>
          </div>
        </div>
      </div>

      <nav className="flex-1 p-2 overflow-y-auto">
        {navItems.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            end={item.path === '/'}
            className={({ isActive }) =>
              clsx(
                'nav-item mb-1',
                isActive ? 'nav-item-active' : 'nav-item-inactive',
                collapsed && 'justify-center'
              )
            }
            title={item.label}
          >
            <span className="text-lg">{item.icon}</span>
            <span
              className={clsx(
                'transition-all duration-300 whitespace-nowrap',
                collapsed ? 'w-0 opacity-0 overflow-hidden' : 'w-auto opacity-100'
              )}
            >
              {item.label}
            </span>
          </NavLink>
        ))}
      </nav>

      <div className="p-3 border-t border-slate-200 dark:border-slate-700">
        <div
          className={clsx(
            'text-xs text-slate-500 dark:text-slate-400 transition-all duration-300',
            collapsed ? 'text-center' : ''
          )}
        >
          <span className={clsx(collapsed ? 'hidden' : 'inline')}>v0.1.0</span>
          <span className={clsx(collapsed ? 'inline' : 'hidden')}>v</span>
        </div>
      </div>
    </aside>
  )
}
