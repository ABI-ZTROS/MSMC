using io.NET.ZTR_OS.Features.Shared.Services;
using Xunit;

namespace io.NET.ZTR_OS.Tests.Services;

/// <summary>🧪 TDD RED: UndoRedoStack&lt;T&gt; 撤销/重做栈测试 —— 空壳功能补全验证</summary>
public class UndoRedoStackTests
{
    // ─────────── 基础: Push / Undo / CanUndo / CanRedo ───────────

    [Fact]
    public void Push_OneItem_CanUndo_True_CanRedo_False()
    {
        // 🟥 RED: 空壳代码 —— UndoRedoStack 尚不存在
        var stack = new UndoRedoStack<string>();
        stack.Push("A");

        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Undo_AfterPush_ReturnsLastPushed()
    {
        var stack = new UndoRedoStack<string>();
        stack.Push("A");
        stack.Push("B");

        var undone = stack.Undo();

        Assert.Equal("B", undone);
        Assert.Equal("A", stack.Current);
        Assert.True(stack.CanRedo);
    }

    [Fact]
    public void Redo_AfterUndo_RestoresState()
    {
        var stack = new UndoRedoStack<string>();
        stack.Push("A");
        stack.Push("B");
        stack.Undo();

        var redone = stack.Redo();

        Assert.Equal("B", redone);
        Assert.Equal("B", stack.Current);
        Assert.False(stack.CanRedo);
    }

    // ─────────── Push 在中间状态破坏 Redo 栈 ───────────

    [Fact]
    public void Push_AfterUndo_ClearsRedoHistory()
    {
        var stack = new UndoRedoStack<string>();
        stack.Push("A");
        stack.Push("B");
        stack.Undo(); // CanRedo = true

        stack.Push("C"); // 新分支，redo 历史应当被清零

        Assert.False(stack.CanRedo);
        Assert.Equal("C", stack.Current);
        Assert.True(stack.CanUndo);
    }

    // ─────────── 边界 ───────────

    [Fact]
    public void Undo_EmptyStack_ThrowsOrReturnsDefault()
    {
        var stack = new UndoRedoStack<string>();
        // 空栈 Undo: 抛 InvalidOperationException
        Assert.Throws<InvalidOperationException>(() => stack.Undo());
    }

    [Fact]
    public void Redo_NoRedoHistory_Throws()
    {
        var stack = new UndoRedoStack<string>();
        stack.Push("A");
        Assert.Throws<InvalidOperationException>(() => stack.Redo());
    }

    // ─────────── 容量限制: 超过最大容量丢弃最老 undo ───────────

    [Fact]
    public void Push_ExceedsMaxHistory_DropsOldestUndo()
    {
        var stack = new UndoRedoStack<int>(maxHistory: 3);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        stack.Push(4); // 超过容量，undo 栈中最老的 "1" 应当被丢弃

        // Undo 三次回到 1 应该取不到（因为 1 被 drop 了），剩下: 2,3,4 的 undo
        var v4 = stack.Undo(); // 4 → 3
        var v3 = stack.Undo(); // 3 → 2
        // 再 undo 应抛或无法回到 1（容量=3意味着 3条undo：2→3, 3→4, 加上initial共3undo条目）
        // 具体行为: 栈里 {undo:[2,3], current:4} push 4，max=3，1 被 drop。undo 2 次回到 2，再 undo 会抛
        Assert.Equal(4, v4);
        Assert.Equal(3, v3);
        Assert.Throws<InvalidOperationException>(() => stack.Undo());
    }

    // ─────────── Clear / Current ───────────

    [Fact]
    public void Clear_ResetsStack_CannotUndoOrRedo()
    {
        var stack = new UndoRedoStack<int>();
        stack.Push(10);
        stack.Push(20);
        stack.Undo();
        Assert.True(stack.CanUndo);
        Assert.True(stack.CanRedo);

        stack.Clear();

        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.Equal(default, stack.Current);
    }
}
