namespace PaneSet;

internal static class ZoneTemplateCatalog
{
    public static readonly ZoneTemplate[] Order =
        [ZoneTemplate.TwoColumns, ZoneTemplate.ThreeColumns, ZoneTemplate.MainAndSide, ZoneTemplate.TwoRows, ZoneTemplate.Grid];

    public static string Label(ZoneTemplate template) => template switch
    {
        ZoneTemplate.TwoColumns => "左右两栏",
        ZoneTemplate.ThreeColumns => "三等分栏",
        ZoneTemplate.MainAndSide => "主辅（2/3 + 1/3）",
        ZoneTemplate.TwoRows => "上下两栏",
        ZoneTemplate.Grid => "2×2 网格",
        _ => throw new ArgumentOutOfRangeException(nameof(template)),
    };
}

internal sealed record DragThumbSlot(WindowRect Rect, ZoneTemplate Template);

/// One observed frame of the pointer screen: its work area, the active template's zones,
/// and the thumbnail strip slot rectangles in physical pixels.
internal sealed record DragZoneView(WindowRect WorkArea, WindowRect[] Zones, DragThumbSlot[] Slots);

/// The decision produced when a drag session ends with a placement intent.
internal sealed record DragPlacementCommit(ZoneTemplate Template, int ZoneIndex, WindowRect Zone, WindowRect WorkArea);

/// Pure interaction state machine for drag placement. All native observations are fed in as
/// arguments so checks can drive synthetic event streams; the machine never calls the system.
internal sealed class DragPlacementStateMachine
{
    private bool started, leftDownAtStart, maximizedAtStart, resizeObserved, escapeSeen, rightButtonSeen;
    private bool modifierHeldLast, leftDownLast;
    private WindowRect startBounds;
    private int hitZone = -1;

    public ZoneTemplate ActiveTemplate { get; private set; } = ZoneTemplate.TwoColumns;
    public bool SessionEngaged { get; private set; }
    public int HitZoneIndex => SessionEngaged ? hitZone : -1;
    public bool Cancelled => escapeSeen || rightButtonSeen;

    /// Sets the template shown when a session engages (the in-session memory of the last choice).
    public void SetActiveTemplate(ZoneTemplate template) { if (Enum.IsDefined(template)) ActiveTemplate = template; }

    public void Start(bool leftDown, bool maximized, WindowRect bounds)
    {
        started = true;
        leftDownAtStart = leftDown;
        maximizedAtStart = maximized;
        startBounds = bounds;
        resizeObserved = escapeSeen = rightButtonSeen = false;
        SessionEngaged = false;
        hitZone = -1;
        modifierHeldLast = leftDownLast = false;
    }

    public void End() { started = false; SessionEngaged = false; hitZone = -1; }

    /// Low-level hook observations: any escape or right-button press during the session cancels
    /// placement permanently, even if the button is released again before the drag ends.
    public void ObserveCancel(bool escape, bool rightButton)
    {
        if (escape) escapeSeen = true;
        if (rightButton) rightButtonSeen = true;
        // Disengage immediately so the overlay disappears the moment a cancel is observed,
        // without waiting for the next sample.
        if (escapeSeen || rightButtonSeen) { SessionEngaged = false; hitZone = -1; }
    }

    /// Pointer passing over a thumbnail slot switches the sticky template before zones are computed.
    public ZoneTemplate? HoverTemplate(Native.Point pointer, DragThumbSlot[] slots)
    {
        foreach (var slot in slots)
            if (Contains(slot.Rect, pointer))
            {
                if (slot.Template != ActiveTemplate) { ActiveTemplate = slot.Template; return slot.Template; }
                return null;
            }
        return null;
    }

    public void Sample(bool modifierHeld, bool leftDown, Native.Point pointer, WindowRect bounds, DragZoneView view)
    {
        if (!started) return;
        modifierHeldLast = modifierHeld;
        leftDownLast = leftDown;
        // A non-maximized window whose size changes is being resized by its border, not moved by its caption.
        if (!maximizedAtStart && (bounds.Width != startBounds.Width || bounds.Height != startBounds.Height))
            resizeObserved = true;
        hitZone = -1;
        for (int i = 0; i < view.Zones.Length; i++)
            if (Contains(view.Zones[i], pointer)) { hitZone = i; break; }
        SessionEngaged = leftDownAtStart && leftDown && modifierHeld && !escapeSeen && !rightButtonSeen && !resizeObserved;
    }

    /// A session commits only when every signal agrees: the modifier is still held, a zone is hit,
    /// no cancel key or right button was observed, the primary button was observed released (not
    /// merely up at the end—the low-level mouse hook must have seen the release), and the drag
    /// never turned into a resize. Any uncertainty drops the placement.
    public DragPlacementCommit? ShouldCommit(DragZoneView view, bool leftReleaseObserved, bool leftDownNow)
    {
        if (!SessionEngaged || !modifierHeldLast || hitZone < 0 || hitZone >= view.Zones.Length) return null;
        if (escapeSeen || rightButtonSeen || resizeObserved || !leftDownAtStart) return null;
        if (!leftReleaseObserved || leftDownNow) return null;
        return new(ActiveTemplate, hitZone, view.Zones[hitZone], view.WorkArea);
    }

    internal static bool Contains(WindowRect rect, Native.Point point) =>
        point.X >= rect.X && point.Y >= rect.Y && point.X < (long)rect.X + rect.Width && point.Y < (long)rect.Y + rect.Height;
}
