import { useState } from 'react'
import { SectionHeader } from '@/components/ui'

interface ConfigFile {
  id: string
  name: string
  path: string
  type: 'properties' | 'yaml' | 'json' | 'txt'
  modified: string
}

const mockFiles: ConfigFile[] = [
  { id: '1', name: 'server.properties', path: 'survival/server.properties', type: 'properties', modified: '2小时前' },
  { id: '2', name: 'spigot.yml', path: 'survival/spigot.yml', type: 'yaml', modified: '昨天' },
  { id: '3', name: 'paper.yml', path: 'survival/paper.yml', type: 'yaml', modified: '3天前' },
  { id: '4', name: 'bukkit.yml', path: 'survival/bukkit.yml', type: 'yaml', modified: '1周前' },
]

export function ConfigEditorPage(): JSX.Element {
  const [selectedFile, setSelectedFile] = useState<string | null>('1')
  const [hasUnsavedChanges] = useState(false)

  return (
    <div className="h-full flex">
      {/* File Sidebar */}
      <div className="w-64 border-r border-slate-200 dark:border-slate-700/50 bg-white/50 dark:bg-slate-800/30 backdrop-blur-sm flex flex-col">
        <div className="p-4 border-b border-slate-200 dark:border-slate-700/50">
          <h3 className="font-semibold text-slate-800 dark:text-slate-200 text-sm">
            配置文件
          </h3>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
            生存服务器
          </p>
        </div>
        <div className="flex-1 overflow-y-auto p-2 space-y-1">
          {mockFiles.map((file) => (
            <button
              key={file.id}
              onClick={() => setSelectedFile(file.id)}
              className={`w-full text-left p-3 rounded-xl transition-all duration-200 ${
                selectedFile === file.id
                  ? 'bg-primary-50 dark:bg-primary-500/10 text-primary-700 dark:text-primary-300'
                  : 'hover:bg-slate-100 dark:hover:bg-slate-700/40 text-slate-700 dark:text-slate-300'
              }`}
            >
              <div className="flex items-center gap-2 mb-1">
                <span className="text-sm">📄</span>
                <span className="font-medium text-sm truncate">{file.name}</span>
              </div>
              <div className="text-xs text-slate-500 dark:text-slate-400 pl-6">
                {file.modified}
              </div>
            </button>
          ))}
        </div>
      </div>

      {/* Editor Area */}
      <div className="flex-1 flex flex-col overflow-hidden">
        {/* Toolbar */}
        <div className="h-14 border-b border-slate-200 dark:border-slate-700/50 bg-white/50 dark:bg-slate-800/30 backdrop-blur-sm flex items-center px-4 gap-3">
          <div className="flex-1 min-w-0">
            <div className="font-medium text-sm text-slate-800 dark:text-slate-200 truncate">
              {mockFiles.find((f) => f.id === selectedFile)?.name}
            </div>
          </div>
          {hasUnsavedChanges && (
            <span className="badge badge-warning">未保存</span>
          )}
          <button className="btn btn-ghost btn-icon" title="撤销">
            ↶
          </button>
          <button className="btn btn-ghost btn-icon" title="重做">
            ↷
          </button>
          <div className="w-px h-6 bg-slate-200 dark:bg-slate-700" />
          <button className="btn btn-secondary">
            重置更改
          </button>
          <button
            className={`btn ${hasUnsavedChanges ? 'btn-primary' : 'btn-secondary opacity-50 cursor-not-allowed'}`}
            disabled={!hasUnsavedChanges}
          >
            💾 保存
          </button>
        </div>

        {/* Editor Content */}
        <div className="flex-1 overflow-auto p-6">
          <div className="card p-5">
            <SectionHeader
              title="配置编辑器"
              subtitle="选择一个配置文件开始编辑"
            />
            <div className="mt-6 py-12 text-center">
              <div className="text-5xl mb-4">⚙️</div>
              <p className="text-slate-500 dark:text-slate-400 mb-2">
                编辑器正在开发中...
              </p>
              <p className="text-sm text-slate-400 dark:text-slate-500">
                将支持语法高亮、实时预览、错误检测等高级功能
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
