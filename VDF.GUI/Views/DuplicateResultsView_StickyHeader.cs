using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VDF.GUI.ViewModels;

namespace VDF.GUI.Views;

public partial class DuplicateResultsView {
    readonly TranslateTransform stickyHeaderOffset = new();
    bool stickyHeaderUpdateQueued;

    ScrollContentPresenter? ResultsViewport => resultsScrollViewer?
        .GetVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault();

    void QueueStickyGroupHeaderUpdate() {
        if (stickyHeaderUpdateQueued) return;
        stickyHeaderUpdateQueued = true;
        Dispatcher.UIThread.Post(() => {
            stickyHeaderUpdateQueued = false;
            UpdateStickyGroupHeader();
        }, DispatcherPriority.Loaded);
    }

    void HideStickyGroupHeader() {
        var pinned = this.FindControl<ContentControl>("StickyGroupHeader");
        if (pinned == null) return;
        pinned.IsVisible = false;
        pinned.Content = null;
    }

    void UpdateStickyGroupHeader() {
        var pinned = this.FindControl<ContentControl>("StickyGroupHeader");
        var viewport = ResultsViewport;
        if (pinned == null || viewport == null || viewport.Bounds.Height <= 0) {
            HideStickyGroupHeader();
            return;
        }

        // Only inspect realized containers, keeping work bounded even for huge libraries.
        object? firstRow = null;
        double firstTop = double.PositiveInfinity;
        double nextHeaderTop = double.PositiveInfinity;
        foreach (var container in ResultsListControl.GetRealizedContainers()) {
            if (container.TranslatePoint(default, viewport) is not { } point) continue;
            int index = ResultsListControl.IndexFromContainer(container);
            if (index < 0 || index >= ResultsListControl.Items.Count) continue;
            var row = ResultsListControl.Items[index];
            if (row is ResultsGroupHeader && point.Y >= 0)
                nextHeaderTop = Math.Min(nextHeaderTop, point.Y);
            if (point.Y + container.Bounds.Height <= 0 || point.Y >= viewport.Bounds.Height)
                continue;
            if (point.Y < firstTop) {
                firstRow = row;
                firstTop = point.Y;
            }
        }

        ResultsGroupHeader? group = firstRow switch {
            ResultsGroupHeader header when firstTop < -0.5 => header,
            ResultsItemRow item => item.Group,
            ResultsDetailsRow details => details.Row.Group,
            _ => null
        };
        if (group == null || group.IsCollapsed) {
            HideStickyGroupHeader();
            return;
        }

        if (viewport.TranslatePoint(default, ResultsListControl) is not { } origin) return;
        pinned.Width = viewport.Bounds.Width;
        pinned.Margin = new Thickness(origin.X, origin.Y, 0, 0);
        pinned.RenderTransform = stickyHeaderOffset;
        if (!ReferenceEquals(pinned.Content, group)) {
            pinned.Content = group;
            Avalonia.Automation.AutomationProperties.SetName(pinned, group.AccessibleName);
        }
        pinned.IsVisible = true;
        // The incoming group's normal header pushes the pinned one out of its way.
        stickyHeaderOffset.Y = Math.Min(0, nextHeaderTop - pinned.Bounds.Height);
    }

    void OnStickyHeaderPointerWheelChanged(object? sender, PointerWheelEventArgs e) {
        var viewport = ResultsViewport;
        var root = TopLevel.GetTopLevel(this);
        if (viewport == null || root == null) return;
        var point = e.GetCurrentPoint(root);
        var forwarded = new PointerWheelEventArgs(viewport, e.Pointer, root, point.Position,
            e.Timestamp, point.Properties, e.KeyModifiers, e.Delta);
        viewport.RaiseEvent(forwarded);
        e.Handled = forwarded.Handled;
    }
}
