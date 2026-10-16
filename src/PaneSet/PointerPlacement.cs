namespace PaneSet;

public sealed partial class WindowManager
{
    internal sealed record PointerPlacementRequest(WindowTarget Source, PlacementDisplay SourceEnvironment, ReferencePosition Reference, Native.Point Point);

    internal PointerPlacementRequest CapturePointerPlacement(nint foreground)
    {
        lock (sync) if (operation != null) throw new InvalidOperationException("正在等待窗口响应，完成前不能再次放置。");
        var pointer = placementEnvironment.ReadPointer();
        var origin = placementEnvironment.Capture(DisplayFor(foreground));
        var candidates = ListWindows();
        var source = candidates.FirstOrDefault(t => t.Handle == foreground)
            ?? throw new InvalidOperationException("没有合格的当前窗口，请从要放置的窗口重试。");
        if (!placementEnvironment.IsForeground(source.Handle)) throw new InvalidOperationException("源窗口已失去前台，未放置。");
        var reference = candidates.FirstOrDefault(t => t.Handle == pointer.Window && t.Handle != source.Handle)
            ?? throw new InvalidOperationException("请将鼠标移到另一扇可见、可调整大小的窗口，再按放置键。");
        if (!TryReadReference(reference, out var state, out string message)) throw new InvalidOperationException(message);
        var rect = state!.Bounds;
        if (pointer.Position.X < rect.X || pointer.Position.Y < rect.Y
            || pointer.Position.X >= (long)rect.X + rect.Width || pointer.Position.Y >= (long)rect.Y + rect.Height)
            throw new InvalidOperationException("无法确认鼠标命中的窗口位置，请重试。");
        var position = new ReferencePosition(reference, state, placementEnvironment.Capture(DisplayFor(reference.Handle)),
            placementEnvironment.DesktopFor(reference.Handle));
        var request = new PointerPlacementRequest(source, origin, position, pointer.Position);
        ValidatePointerPlacement(request);
        if (!placementEnvironment.IsForeground(source.Handle)) throw new InvalidOperationException("源窗口已失去前台，未放置。");
        return request;
    }

    private void ValidatePointerPlacement(PointerPlacementRequest request)
    {
        ValidateReference(request.Source, request.Reference);
        if (placementEnvironment.Capture(request.SourceEnvironment.Display) != request.SourceEnvironment)
            throw new InvalidOperationException("来源显示环境已改变，已停止放置；已改变时可尝试恢复。");
        if (!Native.IsWindowEnabled(request.Source.Handle) || !Native.IsWindowEnabled(request.Reference.Target.Handle))
            throw new InvalidOperationException("鼠标目标或源窗口已被禁用，请关闭对话框后重试。");

    }

    internal async Task<OperationReport> PlaceFromPointerAsync(PointerPlacementRequest request)
    {
        lock (sync)
        {
            if (operation != null) return new([], Pending: true);
            operation = Task.Run(() =>
            {
                try
                {
                    ValidatePointerPlacement(request);
                    if (!placementEnvironment.IsForeground(request.Source.Handle))
                        throw new InvalidOperationException("源窗口已失去前台，未放置。");
                    if (placementEnvironment.WindowAtPoint(request.Point) != request.Reference.Target.Handle)
                        throw new InvalidOperationException("原指向位置已被遮挡，未放置；请重新指向目标。");
                    var report = PlaceCore(request.Source, request.Reference.Environment, request.Reference.DesktopId,
                        request.Reference.State.Bounds, () => ValidatePointerPlacement(request), activateSource: false, sourceEnvironment: request.SourceEnvironment);
                    return report.Success && !placementEnvironment.IsForeground(request.Source.Handle)
                        ? report with { Notice = "位置已放置，前台已切换；未再次激活源窗口。" } : report;
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
                { return new OperationReport([new(request.Source, false, e.Message)], NeedsRefresh: true); }
            });
        }
        return await CollectWithinDeadline();
    }
}
