using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using SABepInExManager.Models;
using SABepInExManager.ViewModels;

namespace SABepInExManager.Behaviors;

public class ModsListBoxDropHandler : DropHandlerBase
{
    private const string DraggingUpClass = "DraggingUp";
    private const string DraggingDownClass = "DraggingDown";

    private static void ClearInsertIndicators(ListBox listBox)
    {
        foreach (var item in listBox.GetVisualDescendants().OfType<ListBoxItem>())
        {
            item.Classes.Remove(DraggingUpClass);
            item.Classes.Remove(DraggingDownClass);
        }
    }

    private static ListBoxItem? GetTargetItemContainer(ListBox listBox, DragEventArgs e)
    {
        if (listBox.GetVisualAt(e.GetPosition(listBox)) is not Control targetControl)
        {
            return null;
        }

        return targetControl as ListBoxItem ?? targetControl.FindAncestorOfType<ListBoxItem>();
    }

    private static bool IsInsertBefore(ListBoxItem targetItem, DragEventArgs e)
    {
        var y = e.GetPosition(targetItem).Y;
        return y < targetItem.Bounds.Height / 2;
    }

    private static void UpdateInsertIndicator(ListBox listBox, DragEventArgs e)
    {
        ClearInsertIndicators(listBox);

        var targetItem = GetTargetItemContainer(listBox, e);
        if (targetItem is null)
        {
            return;
        }

        if (IsInsertBefore(targetItem, e))
        {
            targetItem.Classes.Add(DraggingUpClass);
        }
        else
        {
            targetItem.Classes.Add(DraggingDownClass);
        }
    }

    private bool Reorder(ListBox listBox, DragEventArgs e, object? sourceContext, object? targetContext, bool execute)
    {
        if (sourceContext is not WorkshopModInfo sourceItem
            || targetContext is not HomePageViewModel vm
            || GetTargetItemContainer(listBox, e) is not { DataContext: WorkshopModInfo targetItem } targetContainer)
        {
            return false;
        }

        var items = vm.Mods;
        var sourceIndex = items.IndexOf(sourceItem);
        var targetIndex = items.IndexOf(targetItem);

        if (sourceIndex < 0 || targetIndex < 0)
        {
            return false;
        }

        var insertBefore = IsInsertBefore(targetContainer, e);
        var insertIndex = insertBefore ? targetIndex : targetIndex + 1;
        if (sourceIndex < insertIndex)
        {
            insertIndex--;
        }

        insertIndex = int.Clamp(insertIndex, 0, items.Count - 1);
        if (sourceIndex == insertIndex)
        {
            return false;
        }

        if (execute)
        {
            MoveItem(items, sourceIndex, insertIndex);
            vm.SelectedMod = sourceItem;
        }

        return true;
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        return sender is ListBox listBox
            && Reorder(listBox, e, sourceContext, targetContext, execute: false);
    }

    public override void Enter(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        if (sender is ListBox listBox)
        {
            UpdateInsertIndicator(listBox, e);
        }

        base.Enter(sender, e, sourceContext, targetContext);
    }

    public override void Over(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        if (sender is ListBox listBox)
        {
            UpdateInsertIndicator(listBox, e);
        }

        base.Over(sender, e, sourceContext, targetContext);
    }

    public override void Leave(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            ClearInsertIndicators(listBox);
        }

        base.Leave(sender, e);
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (sender is not ListBox listBox)
        {
            return false;
        }

        try
        {
            return Reorder(listBox, e, sourceContext, targetContext, execute: true);
        }
        finally
        {
            ClearInsertIndicators(listBox);
        }
    }
}

