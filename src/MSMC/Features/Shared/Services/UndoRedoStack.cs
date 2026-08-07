// -----------------------------------------------------------------------------
// 文件名: UndoRedoStack.cs
// 命名空间: io.NET.ZTR_OS.Features.Shared.Services
// 功能描述: 通用撤销/重做栈（支持最大历史容量限制、中间态Push清空Redo历史）
// 依赖组件: 无（纯BCL）
// 设计模式: 双栈模式（Undo栈 + Redo栈）
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.Shared.Services;

/// <summary>
/// 通用撤销/重做栈
/// </summary>
/// <typeparam name="T">状态元素类型</typeparam>
/// <remarks>
/// 行为契约（与单测对齐）：
/// <list type="bullet">
/// <item>Push 新状态后，CanUndo=true，CanRedo=false</item>
/// <item>Undo 返回最近 Push 的值，并把当前指针前移；Redo 反之</item>
/// <item>在中间状态（Undo 后）再次 Push 会清空 Redo 历史（分叉抛弃）</item>
/// <item>超过 maxHistory 时，最老的 Undo 条目被丢弃（FIFO eviction）</item>
/// <item>空栈 Undo / 无 Redo 历史 Redo 抛 InvalidOperationException</item>
/// </list>
/// </remarks>
public class UndoRedoStack<T>
{
    /// <summary>Undo 历史栈（栈顶 = 最近一次 Push 的状态）</summary>
    private readonly List<T> _undo;

    /// <summary>Redo 历史栈（栈顶 = 最近一次 Undo 的状态）</summary>
    private readonly List<T> _redo;

    /// <summary>最大历史容量；0 表示无限</summary>
    private readonly int _maxHistory;

    /// <summary>
    /// 当前状态（当 _undo 为空时，为 default(T)）
    /// Undo 行为：把 Current 压进 Redo，然后 Pop _undo 作为新 Current
    /// Redo 行为：把 Current 压进 Undo，然后 Pop _redo 作为新 Current
    /// Push 行为：把 Current 压进 Undo，设置新 Current，清空 Redo
    /// </summary>
    public T Current { get; private set; } = default!;

    /// <summary>
    /// 是否可撤销（Undo 栈非空）
    /// </summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>
    /// 是否可重做（Redo 栈非空）
    /// </summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// 当前 Undo 历史条数（用于诊断/容量验证）
    /// </summary>
    public int UndoCount => _undo.Count;

    /// <summary>
    /// 当前 Redo 历史条数
    /// </summary>
    public int RedoCount => _redo.Count;

    /// <summary>
    /// 创建一个默认无限容量的撤销/重做栈
    /// </summary>
    public UndoRedoStack() : this(maxHistory: 0) { }

    /// <summary>
    /// 创建一个带最大历史容量限制的撤销/重做栈
    /// </summary>
    /// <param name="maxHistory">最大 Undo 历史条目数；0 = 不限制</param>
    public UndoRedoStack(int maxHistory)
    {
        _maxHistory = Math.Max(0, maxHistory);
        _undo = _maxHistory > 0
            ? new List<T>(_maxHistory + 1)
            : new List<T>();
        _redo = new List<T>();
    }

    /// <summary>
    /// 推入一个新状态快照
    /// </summary>
    /// <param name="state">新的当前状态</param>
    /// <remarks>
    /// 行为：
    /// 1) 把旧 Current 放进 Undo 栈（首次 Push 也会入栈，使可撤销回 default 状态）
    /// 2) 更新 Current = state
    /// 3) 清空 Redo 栈（分支产生，旧 Redo 路径作废）
    /// 4) 超过容量时，从 Undo 栈底移除最老的条目
    /// </remarks>
    public void Push(T state)
    {
        _undo.Add(Current);

        if (_maxHistory > 0 && _undo.Count >= _maxHistory)
        {
            _undo.RemoveAt(0);
        }

        Current = state;
        _redo.Clear();
    }

    /// <summary>
    /// 撤销一次，返回被撤销出去的那个状态（= 撤销前的 Current）
    /// </summary>
    /// <exception cref="InvalidOperationException">Undo 栈为空</exception>
    public T Undo()
    {
        if (!CanUndo)
            throw new InvalidOperationException("Undo 栈为空，无法撤销");

        var prevCurrent = Current;
        _redo.Add(prevCurrent);

        // 栈顶 = 最新条目（List 末尾）
        var lastIdx = _undo.Count - 1;
        Current = _undo[lastIdx];
        _undo.RemoveAt(lastIdx);

        return prevCurrent;
    }

    /// <summary>
    /// 重做一次，返回重做后的新状态（= 重做后的 Current）
    /// </summary>
    /// <exception cref="InvalidOperationException">Redo 栈为空</exception>
    public T Redo()
    {
        if (!CanRedo)
            throw new InvalidOperationException("Redo 栈为空，无法重做");

        _undo.Add(Current);

        var lastIdx = _redo.Count - 1;
        var restored = _redo[lastIdx];
        _redo.RemoveAt(lastIdx);

        Current = restored;
        return restored;
    }

    /// <summary>
    /// 清空 Undo/Redo 历史，Current 重置为 default(T)
    /// </summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Current = default!;
    }

    /// <summary>
    /// 尝试撤销（不抛异常版本）
    /// </summary>
    public bool TryUndo(out T undoneValue)
    {
        if (!CanUndo)
        {
            undoneValue = default!;
            return false;
        }
        undoneValue = Undo();
        return true;
    }

    /// <summary>
    /// 尝试重做（不抛异常版本）
    /// </summary>
    public bool TryRedo(out T redoneValue)
    {
        if (!CanRedo)
        {
            redoneValue = default!;
            return false;
        }
        redoneValue = Redo();
        return true;
    }
}
