#nullable enable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

public static class PaneSetColorPreviews
{
    public static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Pass output directory and A1 icon PNG path.");
        Render(args[0], args[1]);
    }
    private sealed record Palette(string Canvas, string Surface, string Aside, string Ink, string Muted,
        string Line, string SoftLine, string Selected, string Number, string NumberInk,
        string Primary, string PrimaryInk, string Preview, string Pane, string PaneInk);

    private static readonly (string Key, Palette P)[] Variants =
    [
        ("A", new("#F8F9F7","#FFFFFF","#F7F8F7","#233039","#617079","#D7DFDF","#E7ECEA","#E6F4EB","#294D51","#E9FFF1","#25464C","#F7FFF9","#E8EEEA","#CAE9D8","#284F52")),
        ("B", new("#E8EEF1","#FFFFFF","#DBE5E9","#1F3441","#57707D","#BED0D7","#E0E9EB","#D9EEE4","#345D6A","#F2FFF9","#B6DFC9","#1B3B42","#35596A","#D1E9DE","#254A55")),
        ("C", new("#132332","#1B3041","#1A2D3D","#E9F1F0","#A9BAC0","#3A5260","#304655","#2B514E","#A9DEC1","#142F31","#AEDFC1","#163236","#0D1C2B","#376A68","#E1F7E9"))
    ];

    private static Color C(string hex) => ColorTranslator.FromHtml(hex);
    private static void Box(Graphics g, string fill, string? line, float x, float y, float w, float h, float radius = 0)
    {
        using var path = Path(x, y, w, h, radius);
        using var brush = new SolidBrush(C(fill));
        g.FillPath(brush, path);
        if (line is not null) { using var pen = new Pen(C(line)); g.DrawPath(pen, path); }
    }
    private static GraphicsPath Path(float x, float y, float w, float h, float r)
    {
        var path = new GraphicsPath();
        if (r <= 0) { path.AddRectangle(new RectangleF(x,y,w,h)); return path; }
        float d = Math.Min(r * 2, Math.Min(w,h));
        path.AddArc(x,y,d,d,180,90); path.AddArc(x+w-d,y,d,d,270,90);
        path.AddArc(x+w-d,y+h-d,d,d,0,90); path.AddArc(x,y+h-d,d,d,90,90);
        path.CloseFigure(); return path;
    }
    private static void Line(Graphics g, string color, float x1, float y1, float x2, float y2)
    { using var pen = new Pen(C(color)); g.DrawLine(pen,x1,y1,x2,y2); }
    private static void Text(Graphics g, string value, string color, float x, float y, float w, float h, float size = 12,
        bool bold = false, StringAlignment align = StringAlignment.Near)
    {
        using var font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(C(color));
        using var format = new StringFormat { Alignment=align, LineAlignment=StringAlignment.Center,
            Trimming=StringTrimming.EllipsisCharacter, FormatFlags=StringFormatFlags.NoWrap };
        g.DrawString(value,font,brush,new RectangleF(x,y,w,h),format);
    }
    private static void Button(Graphics g, Palette p, string label, float x, float y, float w, float h,
        bool primary = false, bool quiet = false, bool disabled = false)
    {
        string fill = primary ? p.Primary : quiet ? p.Canvas : p.Surface;
        string ink = primary ? p.PrimaryInk : p.Ink;
        if (disabled) { fill = p.Canvas; ink = p.Muted; }
        Box(g,fill,primary ? p.Primary : p.Line,x,y,w,h,7);
        Text(g,label,ink,x+4,y,w-8,h,12,primary,StringAlignment.Center);
    }
    private static void Header(Graphics g, Palette p, string variant, Image icon, bool compact)
    {
        float width = compact ? 560 : 820, height = compact ? 64 : 84;
        if (variant=="B") Box(g,"#F8FBFA",p.Line,20,20,width,height,12);
        else if (variant=="A") { Box(g,p.Surface,null,20,20,width,height,0); Line(g,p.Line,20,20+height,20+width,20+height); }
        else Line(g,p.Line,20,20+height,20+width,20+height);
        float iconY = compact ? 36 : 44;
        g.DrawImage(icon,38,iconY,33,33);
        Text(g,"PaneSet",p.Ink,82,compact?37:39,170,31,compact?20:22,true);
        if (!compact) Text(g,"选择窗口，安排到同一屏",p.Muted,83,70,220,19,11);
        if (compact) { Button(g,p,"A · 本屏全部",358,35,98,35); Button(g,p,"B · 本屏选窗",464,35,96,35,variant=="C"); }
        else { Button(g,p,"A · 本屏全部",615,44,101,38); Button(g,p,"B · 本屏选窗",724,44,104,38,variant=="C"); }
    }
    private static void Toolbar(Graphics g, Palette p, bool compact)
    {
        float y=compact?96:120, searchWidth=compact?375:595;
        Box(g,p.Surface,p.Line,20,y,searchWidth,42,7);
        Text(g,"搜索应用或窗口标题 · Ctrl+F",p.Muted,33,y,searchWidth-25,42,12);
        float refreshX=compact?403:623;
        Button(g,p,"刷新窗口",refreshX,y,compact?94:104,42);
        Button(g,p,"设置",compact?505:735,y,compact?75:105,42);
    }
    private static readonly (string App,string Title)[] Windows =
    [
        ("Cursor · 显示器 1（主屏）","callback.py - Untitled (Workspace) - Cursor"),
        ("ChatGPT · 显示器 1（主屏）","ChatGPT"),
        ("pebrel · 显示器 2","Pebrel"),
        ("chrome · 显示器 1（主屏）","YouTube - Google Chrome"),
        ("WXWork · 显示器 1（主屏）","企业微信"),
        ("Explorer · 显示器 2","PaneSet 项目文件"),
        ("Notion · 显示器 1（主屏）","项目备忘录")
    ];
    private static void List(Graphics g, Palette p, string variant, float x,float y,float w,float h,bool compact)
    {
        Box(g,p.Surface,p.Line,x,y,w,h,variant=="B"?12:8);
        Text(g,"窗口列表",p.Ink,x+14,y+3,120,32,14,true);
        Text(g,"按选择顺序编号",p.Muted,x+w-145,y+3,132,32,11,false,StringAlignment.Far);
        float rowY=y+38, rowH=variant=="C"?49:53;
        for(int i=0;i<Windows.Length;i++)
        {
            if (rowY+rowH>y+h) break;
            if(i<2) Box(g,p.Selected,null,x+1,rowY,w-2,rowH);
            Line(g,p.SoftLine,x+1,rowY,x+w-1,rowY);
            Box(g,i<2?p.Number:p.Canvas,i<2?p.Number:p.Line,x+12,rowY+14,25,25,7);
            Text(g,i<2?(i+1).ToString():"·",i<2?p.NumberInk:p.Muted,x+12,rowY+14,25,25,12,i<2,StringAlignment.Center);
            Text(g,Windows[i].App,p.Muted,x+48,rowY+6,w-62,17,11,true);
            Text(g,Windows[i].Title,p.Ink,x+48,rowY+24,w-62,19,12);
            rowY+=rowH;
        }
        if (compact) Box(g,p.Canvas,null,x+w-8,y+47,4,h-58,2);
    }
    private static void Aside(Graphics g, Palette p, string variant,float x,float y,float w,float h,bool compact)
    {
        Box(g,p.Aside,variant=="A"||variant=="C"?p.Line:null,x,y,w,h,variant=="B"?12:8);
        if (compact)
        {
            Text(g,"目标显示器",p.Muted,x+10,y+5,w/2-15,15,11);
            Text(g,"整组布局",p.Muted,x+w/2+3,y+5,w/2-15,15,11);
            Box(g,p.Surface,p.Line,x+10,y+24,w/2-17,35,5);
            Box(g,p.Surface,p.Line,x+w/2+3,y+24,w/2-13,35,5);
            Text(g,"显示器 1（主屏） · 2560 × 1400",p.Ink,x+17,y+24,w/2-30,35,11);
            Text(g,"自动布局",p.Ink,x+w/2+10,y+24,w/2-26,35,12);
            return;
        }
        Text(g,"排布目标",p.Ink,x+15,y+12,w-30,24,variant=="B"?16:14,true);
        Text(g,"目标显示器",p.Muted,x+15,y+49,w-30,16,11);
        Box(g,p.Surface,p.Line,x+15,y+70,w-30,37,6);
        Text(g,"显示器 1（主屏） · 2560 × 1400",p.Ink,x+23,y+70,w-46,37,11);
        Text(g,"整组布局",p.Muted,x+15,y+124,w-30,16,11);
        Box(g,p.Surface,p.Line,x+15,y+145,w-30,37,6);
        Text(g,"自动布局",p.Ink,x+23,y+145,w-46,37,12);
        Text(g,"Alt+1 等宽 · Alt+2 主辅 · Alt+3 网格",p.Muted,x+15,y+192,w-30,18,10);
        Text(g,"布局预览",p.Ink,x+15,y+227,w-30,20,12,true);
        Box(g,p.Preview,null,x+15,y+253,w-30,119,8);
        float half=(w-54)/2;
        Box(g,p.Pane,null,x+27,y+265,half,95,4); Box(g,p.Pane,null,x+29+half,y+265,half,95,4);
        Text(g,"1",p.PaneInk,x+27,y+265,half,95,28,false,StringAlignment.Center);
        Text(g,"2",p.PaneInk,x+29+half,y+265,half,95,28,false,StringAlignment.Center);
    }
    private static void Bottom(Graphics g, Palette p, string variant,bool compact)
    {
        float summaryY=compact?469:572, statusY=compact?502:615, footerY=compact?530:645;
        Text(g,"已选 2 扇",p.Ink,20,summaryY,90,30,12,true);
        Text(g,"1 · callback.py - Untitled　2 · ChatGPT",p.Muted,110,summaryY,compact?245:425,30,11);
        if(!compact) { Button(g,p,"调整最近组",compact?0:610,summaryY,104,32,false,false,true); Button(g,p,"交换左右",722,summaryY,118,32); }
        else { Button(g,p,"调整最近组",393,summaryY,89,30,false,false,true); Button(g,p,"交换左右",489,summaryY,91,30); }
        Text(g,"可选择各屏幕上被遮挡的窗口。收起后仍在运行，退出会清除恢复记录。",p.Muted,20,statusY,compact?560:820,18,11);
        if(variant=="B") Box(g,"#F8FBFA",p.Line,20,footerY,compact?560:820,compact?50:61,8);
        else Line(g,p.Line,20,footerY,compact?580:840,footerY);
        float buttonY=footerY+(compact?7:11), left=variant=="B"?31:20;
        Button(g,p,"排布所选窗口",left,buttonY,126,39,true);
        Button(g,p,"恢复上次排布",left+134,buttonY,128,39,false,false,true);
        Button(g,p,"收起",compact?455:726,buttonY,52,39,false,true);
        Button(g,p,"退出",compact?515:788,buttonY,52,39,false,true);
        if(!compact) Text(g,"Ctrl+Alt+Space 打开 / 收起 · Enter 排布 · Alt+S 轮换",p.Muted,20,704,820,16,10);
    }
    private static void RenderOne(string output, string variant, Palette p, Image icon, bool compact)
    {
        int width=compact?600:860,height=compact?600:730;
        using var bitmap=new Bitmap(width*2,height*2,PixelFormat.Format32bppArgb);
        bitmap.SetResolution(192,192);
        using(var g=Graphics.FromImage(bitmap))
        {
            g.SmoothingMode=SmoothingMode.AntiAlias; g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.ScaleTransform(2,2);
            g.Clear(C(p.Canvas));
            Header(g,p,variant,icon,compact); Toolbar(g,p,compact);
            if(compact)
            {
                List(g,p,variant,20,151,560,228,true);
                Aside(g,p,variant,20,390,560,70,true);
            }
            else if(variant=="B")
            {
                Aside(g,p,variant,20,177,230,384,false);
                List(g,p,variant,266,177,574,384,false);
            }
            else
            {
                List(g,p,variant,20,177,532,384,false);
                Aside(g,p,variant,568,177,272,384,false);
            }
            Bottom(g,p,variant,compact);
        }
        bitmap.Save(output,ImageFormat.Png);
    }
    public static void Render(string outputDirectory,string iconPath)
    {
        Directory.CreateDirectory(outputDirectory);
        using var icon=Image.FromFile(iconPath);
        foreach(var (key,p) in Variants)
        {
            RenderOne(System.IO.Path.Combine(outputDirectory,$"{key}-normal.png"),key,p,icon,false);
            RenderOne(System.IO.Path.Combine(outputDirectory,$"{key}-minimum.png"),key,p,icon,true);
        }
    }
}
