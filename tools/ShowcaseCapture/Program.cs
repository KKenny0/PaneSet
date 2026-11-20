// README showcase 摄影棚驱动：fixture 测试窗口出镜（绝不动用户真实窗口），
// 注入 PaneSet 快捷键触发真实产品行为，ffmpeg gdigrab 抓屏合成 GIF。
// 用法：ShowcaseCapture.exe hero|drag|quick
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;

if (args.Length < 1 || !new[] { "hero", "drag", "quick" }.Contains(args[0]))
{ Console.Error.WriteLine("usage: ShowcaseCapture.exe hero|drag|quick"); return 2; }
string shot = args[0];

string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string checksExe = Path.Combine(root, "tests", "PaneSet.Checks", "bin", "Debug", "net10.0-windows", "PaneSet.Checks.exe");
string ffmpeg = "E:\\ffmpeg\\bin\\ffmpeg.exe";
string outDir = Path.Combine(root, "docs", "images", "readme");
string framesDir = Path.Combine(Path.GetTempPath(), $"paneset-showcase-{shot}");
if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
Directory.CreateDirectory(framesDir);

// ---- 1. fixture：3 个测试窗口 ----
var fixture = Process.Start(new ProcessStartInfo(checksExe)
{ Arguments = "--fixture 3", UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true })!;
string? line = fixture.StandardOutput.ReadLine();
if (line is null) { Console.Error.WriteLine("fixture did not start"); return 1; }
nint[] handles = line.Split(',').Select(s => (nint)long.Parse(s)).ToArray();
Console.WriteLine($"fixture windows: {string.Join(',', handles.Select(h => h.ToString()))}");
async Task Send(string command)
{
    await fixture.StandardInput.WriteLineAsync(command);
    await fixture.StandardInput.FlushAsync();
    string? ack = await fixture.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
    if (ack != "OK") throw new InvalidOperationException($"fixture rejected '{command}': {ack}");
    await Task.Delay(80);
}

// ---- 2. 快捷键注入（SendInput，检查套件同级手法）----
// 用户设置：OpenPanel=Ctrl+Alt+Space(3,32) Restore=Ctrl+Alt+Z(6,90) QuickSelect=Ctrl+Alt+2(6,50) drag=Ctrl+Shift(6)
const uint KCtrl = 0x11, KAlt = 0x12, KShift = 0x10, LZuo = 0x5A, LNum2 = 0x32, KSpace = 0x20;
void Key(uint vk, bool down)
{
    var input = new INPUT { type = 1, U = new U { ki = new KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = down ? 0u : 2u, time = 0, dwExtraInfo = 0 } } };
    SendInput(1, [input], Marshal.SizeOf<INPUT>());
}
void Chord(params uint[] keys)
{
    foreach (var k in keys) Key(k, true);
    Thread.Sleep(90);
    foreach (var k in keys.Reverse()) Key(k, false);
    Thread.Sleep(420);
}
void TypeKey(uint vk) { Key(vk, true); Thread.Sleep(50); Key(vk, false); Thread.Sleep(150); }

// ---- 3. 画面区域：屏 2（DISPLAY2，2560,0 起）工作区，terminal 作背景 ----
var screen = System.Windows.Forms.Screen.AllScreens.First(s => !s.Primary);
var wa = screen.WorkingArea;
int capX = wa.X, capY = wa.Y, capW = wa.Width, capH = wa.Height;
Console.WriteLine($"capture area: {capX},{capY} {capW}x{capH}");

// ---- 4. 逐帧抓屏：ffmpeg gdigrab 持续写帧，脚本控制节奏 ----
var ffmpegArgs = $"-hide_banner -loglevel error -f gdigrab -framerate 15 -offset_x {capX} -offset_y {capY} -video_size {capW}x{capH} -draw_mouse 1 -i desktop -q:v 2 {framesDir}\\f%05d.jpg";
var cap = Process.Start(new ProcessStartInfo(ffmpeg) { Arguments = ffmpegArgs, UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true });
Thread.Sleep(1200); // 抓屏先稳定

void WaitFront(nint h, string what)
{
    for (int i = 0; i < 50 && GetForegroundWindow() != h; i++)
    {
        // 检查套件同款：零位移合成鼠标事件声明"最后输入者"，AttachThreadInput 后转交前台
        var move = new INPUT { type = 0, U = new U { mi = new MOUSEINPUT { dwFlags = 1 } } };
        SendInput(1, [move], Marshal.SizeOf<INPUT>());
        uint me = GetCurrentThreadId();
        nint fg = GetForegroundWindow();
        uint fgThread = fg == 0 ? 0 : GetWindowThreadProcessId(fg, out _);
        uint targetThread = GetWindowThreadProcessId(h, out _);
        bool a1 = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
        bool a2 = targetThread != 0 && targetThread != me && AttachThreadInput(me, targetThread, true);
        try { SetForegroundWindow(h); }
        finally { if (a2) AttachThreadInput(me, targetThread, false); if (a1) AttachThreadInput(me, fgThread, false); }
        Thread.Sleep(120);
    }
    if (GetForegroundWindow() != h) Console.WriteLine($"WARN front={what}");
}

try
{
    if (shot == "hero")
    {
        await Send($"position 0 {capX + 140} {capY + 90} 760 560");    // 窗口0 左
        await Send($"position 1 {capX + 480} {capY + 240} 760 560");   // 窗口1 右、部分遮挡
        WaitFront(handles[1], "w1");
        Thread.Sleep(700);
        Chord(KCtrl, KAlt, KSpace);                 // 唤出面板（前台=窗口1 预选）
        Thread.Sleep(900);
        ClickNamed("采购合同 · 初稿");               // 勾选窗口0（Name=窗口标题）
        Thread.Sleep(600);
        TypeKey(0x0D);                              // Enter 排布
        Thread.Sleep(1400);
        Chord(KCtrl, KShift, LZuo);                 // Restore=Ctrl+Shift+Z（用户设置 modifiers=6）
        Thread.Sleep(1200);
    }
    else if (shot == "drag")
    {
        await Send($"position 0 {capX + 100} {capY + 110} 980 700");
        await Send($"position 1 {capX + 1180} {capY + 300} 600 460");
        WaitFront(handles[1], "w1");
        Thread.Sleep(600);
        DragWithModifiers(handles[1]);              // Ctrl+Shift 拖动 → 分区 → 缩略图 → 落格
    }
    else // quick
    {
        await Send($"position 0 {capX + 150} {capY + 100} 660 480");
        await Send($"position 1 {capX + 330} {capY + 240} 660 480");
        await Send($"position 2 {capX + 510} {capY + 380} 660 480");
        WaitFront(handles[1], "w1");
        Thread.Sleep(600);
        Chord(KCtrl, KShift, LNum2);                // QuickSelect=Ctrl+Shift+2（用户设置）
        Thread.Sleep(900);
        ClickNamed("报价清单");                       // 勾选窗口2 缩略图（Name=窗口标题）
        Thread.Sleep(500);
        TypeKey(0x0D);
        Thread.Sleep(1400);
    }
    Thread.Sleep(800); // 结尾定格
}
finally
{
    if (cap is { HasExited: false }) { cap.Kill(); cap.WaitForExit(3000); }
    await Send("quit");
    if (!fixture.HasExited) { fixture.Kill(true); }
}

// ---- 5. 帧序列 → GIF（调色板优化两遍法）----
string gif = Path.Combine(outDir, $"{shot}.gif");
string palette = Path.Combine(framesDir, "palette.png");
// 抓屏为全尺寸 2560 宽，导出缩到 1280 宽 GIF（AquaSnap 体积基准）
Run(ffmpeg, $"-hide_banner -loglevel error -i {framesDir}/f%05d.jpg -vf \"fps=12,scale=1280:-1,palettegen=max_colors=128\" -y {palette}");
Run(ffmpeg, $"-hide_banner -loglevel error -framerate 12 -i {framesDir}/f%05d.jpg -i {palette} -lavfi \"scale=1280:-1 [x]; [x][1:v] paletteuse=dither=bayer:bayer_scale=4\" -loop 0 -y {gif}");
var size = new FileInfo(gif).Length;
Console.WriteLine($"DONE {gif} frames=~{(Directory.GetFiles(framesDir, "f*.jpg").Length)} size={size / 1024}KB");
return 0;

static void Run(string exe, string args)
{
    var p = Process.Start(new ProcessStartInfo(exe) { Arguments = args, UseShellExecute = false, CreateNoWindow = true });
    p.WaitForExit();
    if (p.ExitCode != 0) throw new InvalidOperationException($"{exe} {args} -> {p.ExitCode}");
}

void ClickAt(int x, int y)
{
    SetCursorPos(x, y); Thread.Sleep(120);
    mouse_event(2, 0, 0, 0, 0); Thread.Sleep(70);
    mouse_event(4, 0, 0, 0, 0); Thread.Sleep(120);
}

// UI Automation：按 Name 定位控件并点击其中心（检查套件同款手法，坐标零猜测）
void ClickNamed(string name, int timeoutMs = 6000)
{
    var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
    while (DateTime.UtcNow < deadline)
    {
        var root = AutomationElement.RootElement;
        var element = root.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, name));
        if (element != null)
        {
            var point = element.GetClickablePoint();
            ClickAt((int)point.X, (int)point.Y);
            return;
        }
        Thread.Sleep(150);
    }
    throw new InvalidOperationException($"UIA element not found: {name}");
}

void DragWithModifiers(nint h)
{
    GetWindowRect(h, out var r);
    int titleX = (r.L + r.Rt) / 2, titleY = r.T + 18;
    SetCursorPos(titleX, titleY); Thread.Sleep(150);
    Key(KCtrl, true); Key(KShift, true); Thread.Sleep(250);
    mouse_event(2, 0, 0, 0, 0); Thread.Sleep(200);          // 左键按下（开始拖）
    // 拖到屏幕中部（触发分区浮现）
    MoveCursorGradual(titleX, titleY, capX + capW * 2 / 3, capY + capH / 2, 700);
    Thread.Sleep(1200);                                       // 分区浮现、模板条可见
    // 划过缩略图条（顶部第4枚=辅主）：缩略图条在屏顶 y≈14+32
    MoveCursorGradual(capX + capW * 2 / 3, capY + capH / 2, capX + capW / 2, capY + 40, 600);
    Thread.Sleep(400);
    MoveCursorGradual(capX + capW / 2, capY + 40, capX + capW / 2 - 260, capY + 40, 500); // 主辅→辅主方向划
    Thread.Sleep(500);
    // 落到左侧 1/3 格
    MoveCursorGradual(capX + capW / 2 - 260, capY + 40, capX + capW / 6, capY + capH / 2, 600);
    Thread.Sleep(600);
    mouse_event(4, 0, 0, 0, 0);                              // 松手 → 投放
    Key(KShift, false); Key(KCtrl, false);
}

void MoveCursorGradual(int x0, int y0, int x1, int y1, int ms)
{
    int steps = Math.Max(8, ms / 30);
    for (int i = 1; i <= steps; i++)
    {
        int t = i, n = steps;
        int x = x0 + (x1 - x0) * t / n, y = y0 + (y1 - y0) * t / n;
        SetCursorPos(x, y); Thread.Sleep(ms / steps);
    }
}

[DllImport("user32.dll")] static extern bool SetForegroundWindow(nint w);
[DllImport("user32.dll")] static extern nint GetForegroundWindow();
[DllImport("user32.dll")] static extern bool GetWindowRect(nint w, out RECT r);
[DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] static extern void mouse_event(uint f, uint dx, uint dy, uint d, nint e);
[DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
[DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint w, out uint p);
[DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
[DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
[StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, Rt, B; }
[StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public uint wVk, wScan, dwFlags, time; public nint dwExtraInfo; }
[StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public nint dwExtraInfo; }
[StructLayout(LayoutKind.Explicit)] struct U { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
[StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public U U; }
