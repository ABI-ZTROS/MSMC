import { useState } from 'react'
import { SectionHeader } from '@/components/ui'
import { useAppStore } from '@/stores/appStore'

const themeColors = [
  { name: '天蓝', value: '#3b82f6', ring: 'ring-blue-500', bg: 'bg-blue-500' },
  { name: '紫罗兰', value: '#8b5cf6', ring: 'ring-violet-500', bg: 'bg-violet-500' },
  { name: '粉紫', value: '#ec4899', ring: 'ring-pink-500', bg: 'bg-pink-500' },
  { name: '翠绿', value: '#22c55e', ring: 'ring-green-500', bg: 'bg-green-500' },
  { name: '橙色', value: '#f97316', ring: 'ring-orange-500', bg: 'bg-orange-500' },
  { name: '青色', value: '#06b6d4', ring: 'ring-cyan-500', bg: 'bg-cyan-500' },
]

export function SettingsPage(): JSX.Element {
  const [darkMode, setDarkMode] = useState(true)
  const [animations, setAnimations] = useState(true)
  const [selectedColor, setSelectedColor] = useState(themeColors[0])
  const version = useAppStore((s) => s.version)

  return (
    <div className="p-6 pb-8 max-w-3xl mx-auto">
      {/* Page Header */}
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-slate-900 dark:text-white mb-1">
          设置
        </h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          自定义应用程序外观和行为
        </p>
      </div>

      {/* Appearance Section */}
      <div className="mb-6">
        <SectionHeader title="外观" />
        <div className="card divide-y divide-slate-100 dark:divide-slate-700/50 overflow-hidden">
          {/* Dark Mode */}
          <div className="p-5 flex items-center justify-between">
            <div>
              <div className="font-medium text-slate-800 dark:text-slate-200">
                深色模式
              </div>
              <div className="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
                使用深色主题保护眼睛
              </div>
            </div>
            <button
              onClick={() => setDarkMode(!darkMode)}
              className={`relative w-12 h-7 rounded-full transition-all duration-300 ${
                darkMode
                  ? 'bg-primary-500'
                  : 'bg-slate-300 dark:bg-slate-600'
              }`}
            >
              <span
                className={`absolute top-0.5 w-6 h-6 bg-white rounded-full shadow-md transition-all duration-300 flex items-center justify-center text-xs ${
                  darkMode ? 'left-5' : 'left-0.5'
                }`}
              >
                {darkMode ? '🌙' : '☀️'}
              </span>
            </button>
          </div>

          {/* Animations */}
          <div className="p-5 flex items-center justify-between">
            <div>
              <div className="font-medium text-slate-800 dark:text-slate-200">
                动画效果
              </div>
              <div className="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
                启用过渡动画和微交互
              </div>
            </div>
            <button
              onClick={() => setAnimations(!animations)}
              className={`relative w-12 h-7 rounded-full transition-all duration-300 ${
                animations
                  ? 'bg-primary-500'
                  : 'bg-slate-300 dark:bg-slate-600'
              }`}
            >
              <span
                className={`absolute top-0.5 w-6 h-6 bg-white rounded-full shadow-md transition-all duration-300 ${
                  animations ? 'left-5' : 'left-0.5'
                }`}
              />
            </button>
          </div>

          {/* Theme Color */}
          <div className="p-5">
            <div className="font-medium text-slate-800 dark:text-slate-200 mb-3">
              主题色
            </div>
            <div className="flex gap-3">
              {themeColors.map((color) => (
                <button
                  key={color.value}
                  onClick={() => setSelectedColor(color)}
                  title={color.name}
                  className={`w-10 h-10 rounded-xl ${color.bg} transition-all duration-200 hover:scale-110 ${
                    selectedColor.value === color.value
                      ? `ring-4 ring-offset-2 dark:ring-offset-slate-800 ${color.ring}`
                      : ''
                  }`}
                />
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Server Section */}
      <div className="mb-6">
        <SectionHeader title="服务器设置" />
        <div className="card divide-y divide-slate-100 dark:divide-slate-700/50 overflow-hidden">
          <div className="p-5 flex items-center justify-between">
            <div>
              <div className="font-medium text-slate-800 dark:text-slate-200">
                自动启动服务器
              </div>
              <div className="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
                应用启动时自动启动已保存的服务器
              </div>
            </div>
            <button className="relative w-12 h-7 rounded-full bg-slate-300 dark:bg-slate-600 transition-all duration-300">
              <span className="absolute top-0.5 left-0.5 w-6 h-6 bg-white rounded-full shadow-md transition-all duration-300" />
            </button>
          </div>
          <div className="p-5 flex items-center justify-between">
            <div>
              <div className="font-medium text-slate-800 dark:text-slate-200">
                自动备份
              </div>
              <div className="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
                每天自动备份服务器世界数据
              </div>
            </div>
            <button className="relative w-12 h-7 rounded-full bg-primary-500 transition-all duration-300">
              <span className="absolute top-0.5 left-5 w-6 h-6 bg-white rounded-full shadow-md transition-all duration-300" />
            </button>
          </div>
        </div>
      </div>

      {/* About Section */}
      <div>
        <SectionHeader title="关于" />
        <div className="card p-5">
          <div className="flex items-center gap-4">
            <div className="w-14 h-14 rounded-2xl bg-gradient-to-br from-primary-400 via-primary-500 to-accent-500 flex items-center justify-center text-white font-bold text-xl shadow-lg shadow-primary-500/30">
              M
            </div>
            <div>
              <div className="font-bold text-lg text-slate-900 dark:text-white">
                MSMC
              </div>
              <div className="text-sm text-slate-500 dark:text-slate-400">
                Minecraft Server Management Console
              </div>
              <div className="text-xs text-slate-400 dark:text-slate-500 mt-0.5">
                版本 v{version} (Web UI Preview)
              </div>
            </div>
          </div>
          <div className="divider my-5" />
          <div className="grid grid-cols-2 gap-4">
            <button className="btn btn-secondary w-full">
              📖 文档
            </button>
            <button className="btn btn-secondary w-full">
              💬 反馈
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
