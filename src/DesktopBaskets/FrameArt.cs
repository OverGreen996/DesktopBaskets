using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace DesktopBaskets;

internal static class FrameArt
{
    public static void Prepare(Graphics g)
    {
        g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;
    }
    static readonly PrivateFontCollection displayFonts=new();
    static readonly Dictionary<float,Font> display=new();
    static readonly Dictionary<float,Font> data=new();
    public static bool NativeFontRegistered {get;}
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern int AddFontResourceEx(string path,uint flags,IntPtr reserved);
    static FrameArt()
    {
        string path=Path.Combine(AppContext.BaseDirectory,"Assets","RussoOne-Regular.ttf");
        if(File.Exists(path)){displayFonts.AddFontFile(path);NativeFontRegistered=AddFontResourceEx(path,0x10,IntPtr.Zero)>0;}
    }
    public static Font Display(float pixels)
    {
        if(!display.TryGetValue(pixels,out var font))display[pixels]=font=displayFonts.Families.Length>0
            ?new Font(displayFonts.Families[0],pixels,FontStyle.Regular,GraphicsUnit.Pixel)
            :new Font("Bahnschrift",pixels,FontStyle.Bold,GraphicsUnit.Pixel);
        return font;
    }
    public static Font Data(float pixels=10)
    {
        if(!data.TryGetValue(pixels,out var font))data[pixels]=font=new Font("Bahnschrift",pixels,FontStyle.Regular,GraphicsUnit.Pixel);
        return font;
    }
    public static string Version=>typeof(FrameArt).Assembly.GetName().Version!.ToString(3);
    public static Font FitDisplay(Graphics g,string text,int width,int desired,int minimum=12)
    {
        for(int size=desired;size>=minimum;size--){var font=Display(size);if(TextRenderer.MeasureText(text,font,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.SingleLine).Width<=width||size==minimum)return font;}
        return Display(minimum);
    }
    public static void Label(Graphics g,string text,RectangleF rect,Font font,Color color)
    {
        TextRenderer.DrawText(g,text,font,Rectangle.Round(rect),color,TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.PreserveGraphicsClipping);
    }
    public static void Cross(Graphics g,int x,int y,Color color)
    {
        using var pen=new Pen(color);g.DrawLine(pen,x-4,y,x+4,y);g.DrawLine(pen,x,y-4,x,y+4);
    }
    public static void Tracked(Graphics g,string text,float x,float y,float size,float tracking,Color color)
    {
        var font=Data(Math.Max(Theme.MinimumMetadataPixels,size));
        using var format=(StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags|=StringFormatFlags.MeasureTrailingSpaces;
        foreach(char character in text)
        {
            string value=character.ToString();TextRenderer.DrawText(g,value,font,new Point((int)Math.Round(x),(int)Math.Round(y)),color,TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.PreserveGraphicsClipping);
            x+=g.MeasureString(value,font,1000,format).Width+tracking;
        }
    }
    public static void TrackedVector(Graphics g,string text,float x,float y,float size,float tracking,Color color)
    {
        using var brush=new SolidBrush(color);using var format=(StringFormat)StringFormat.GenericTypographic.Clone();format.FormatFlags|=StringFormatFlags.MeasureTrailingSpaces;
        var font=Data(Math.Max(Theme.MinimumMetadataPixels,size));g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;
        foreach(char character in text){string value=character.ToString();g.DrawString(value,font,brush,x,y,format);x+=g.MeasureString(value,font,1000,format).Width+tracking;}
    }
    public static void ReferenceHeader(Graphics g,int width,int height,int ordinal,string title,int count,string status)
    {
        Prepare(g);
        if(Grid.Compact(width)){CompactHeader(g,width,height,ordinal,title,count,status);return;}
        float s=width/1537f,t=height/102f;using var line=new Pen(Color.FromArgb(149,153,153),1);
        g.DrawLine(line,182*s,88*t,221*s,41*t);
        Label(g,ordinal.ToString("00"),new RectangleF(50*s,10*t,133*s,86*t),Display(Math.Max(12,80*s)),Theme.Text);
        Tracked(g,"LOCAL / DESKTOP",239*s,20*t,14*s,3*s,Theme.Text);
        var titleRect=new RectangleF(239*s,42*t,328*s,57*t);
        var font=title.Any(c=>c>127)?Theme.Body(Math.Max(7,37*s),FontStyle.Bold):FitDisplay(g,title,(int)titleRect.Width,(int)Math.Max(10,50*s),Math.Max(8,(int)(36*s)));
        Label(g,title,titleRect,font,Theme.Text);
        g.DrawLine(line,593*s,46*t,593*s,86*t);
        Tracked(g,"OBJECT GROUP",626*s,52*t,10*s,2.2f*s,Theme.Muted);
        Tracked(g,"LOCAL STORAGE SECTOR",626*s,76*t,10*s,2*s,Theme.Muted);
        Tracked(g,"OBJECTS",width-313,41*t,12,1.6f,Theme.Text);
        string countText=count.ToString("000");float countSize=28;
        while(countSize>16&&TextRenderer.MeasureText(countText,Data(countSize),Size.Empty,TextFormatFlags.NoPadding).Width>62)countSize--;
        Label(g,countText,new RectangleF(width-313,55*t,66,34),Data(countSize),Theme.Text);
        g.DrawLine(line,width-245,46*t,width-245,85*t);
        using var signal=new SolidBrush(Theme.Accent);g.FillEllipse(signal,width-215,61*t,14,14);
        Tracked(g,"STATUS : "+status,width-193,63*t,12,1.2f,Theme.Text);
        Tracked(g,"VER "+Version,width-(63*s)-70,12*t,12,.5f,Theme.Muted);
        g.DrawLine(line,43*s,1,82*s,1);g.DrawLine(line,103*s,1,814*s,1);g.DrawLine(line,834*s,1,916*s,1);g.DrawLine(line,932*s,1,1462*s,1);
        g.DrawLine(line,0,40*t,43*s,1);g.DrawLine(line,0,1,0,24*t);
        for(int i=0;i<5;i++)g.DrawLine(line,32*s,(height-5)+i*.8f,Math.Max(32,46-i*3)*s,(height-5)+i*.8f);
    }
    public static void ReferenceFooter(Graphics g,int width,int height,int ordinal,int revision,DateTimeOffset modified,int columns,int rows,bool locked)
    {
        Prepare(g);
        if(Grid.Compact(width)){CompactFooter(g,width,height,ordinal,revision,modified,columns,rows);return;}
        float s=width/1537f,t=height/53f;using var line=new Pen(Theme.FrameLine,1);using var face=new SolidBrush(Color.FromArgb(47,50,51));
        g.FillPolygon(face,new[]{new PointF(0,0),new PointF(24*s,0),new PointF(89*s,height-1),new PointF(4*s,height-1),new PointF(0,height-7)});
        g.DrawLine(line,0,0,0,height-7);g.DrawLine(line,0,height-7,4*s,height-1);g.DrawLine(line,4*s,height-1,89*s,height-1);
        using var stripe=new Pen(Theme.Text,Math.Max(1,4*s));for(int i=0;i<4;i++)g.DrawLine(stripe,(14+i*8)*s,29*t,(22+i*8)*s,17*t);
        g.DrawLine(line,102*s,17*t,102*s,42*t);
        Tracked(g,"SYS: LOCAL",121*s,12*t,11*s,2.7f*s,Theme.Text);
        Tracked(g,"SECTOR "+ordinal.ToString("00"),121*s,31*t,11*s,2.7f*s,Theme.Text);
        Tracked(g,$"GRID {columns} × {rows}  /  PATH: ORIGINAL",370*s,25*t,10*s,1.5f*s,Theme.Muted);
        g.DrawLine(line,width-242,10*t,width-242,38*t);
        Tracked(g,"REV."+revision.ToString("00"),width-224,23*t,12,1.2f,Theme.Text);
        g.DrawLine(line,width-162,10*t,width-162,38*t);
        Tracked(g,modified.ToLocalTime().ToString("yyyy.MM.dd"),width-143,23*t,12,1.2f,Theme.Text);
        g.DrawLine(line,97*s,height-1,288*s,height-1);g.DrawLine(line,312*s,height-1,1360*s,height-1);g.DrawLine(line,1385*s,height-1,1468*s,height-1);g.DrawLine(line,1468*s,height-1,1535*s,0);
        g.DrawLine(line,1500*s,height-1,1535*s,height-1);g.DrawLine(line,1535*s,height-1,1535*s,12*t);Cross(g,(int)(1514*s),(int)(30*t),Theme.Muted);
    }
    static void CompactHeader(Graphics g,int width,int height,int ordinal,string title,int count,string status)
    {
        // Reflow the same live fields; never scale a text bitmap or omit ornaments.
        using var line=new Pen(Theme.FrameLine,1);
        g.DrawLine(line,0,22,22,1);g.DrawLine(line,0,1,0,13);
        g.DrawLine(line,22,1,40,1);g.DrawLine(line,48,1,width*.52f,1);
        g.DrawLine(line,width*.53f,1,width*.66f,1);g.DrawLine(line,width*.68f,1,width-48,1);
        Label(g,ordinal.ToString("00"),new Rectangle(16,5,63,49),Display(48),Theme.Text);
        g.DrawLine(line,76,53,91,24);
        Tracked(g,"LOCAL / DESKTOP",94,6,12,.8f,Theme.Text);
        var titleRect=new Rectangle(94,23,width-278,32);
        var font=title.Any(c=>c>127)?Theme.Body(18,FontStyle.Bold):FitDisplay(g,title,titleRect.Width,30,20);
        Label(g,title,titleRect,font,Theme.Text);
        g.DrawLine(line,width-184,24,width-184,49);
        Tracked(g,"OBJECTS",width-176,25,12,.5f,Theme.Text);
        Label(g,count.ToString("000"),new Rectangle(width-106,20,61,33),Data(28),Theme.Text);
        using var signal=new SolidBrush(Theme.Accent);g.FillEllipse(signal,width-176,54,14,14);
        Tracked(g,"STATUS : "+status,width-154,55,12,.2f,Theme.Text);
        Tracked(g,"VER "+Version,width-126,7,12,.3f,Theme.Muted);
        Tracked(g,"OBJECT GROUP",20,57,12,.3f,Theme.Muted);
        Tracked(g,"LOCAL STORAGE SECTOR",138,57,12,.3f,Theme.Muted);
        for(int i=0;i<5;i++)g.DrawLine(line,10,height-5+i*.8f,20-i*2,height-5+i*.8f);
    }
    static void CompactFooter(Graphics g,int width,int height,int ordinal,int revision,DateTimeOffset modified,int columns,int rows)
    {
        using var line=new Pen(Theme.FrameLine,1);using var face=new SolidBrush(Color.FromArgb(47,50,51));
        g.FillPolygon(face,new[]{new Point(0,0),new Point(8,0),new Point(30,height-1),new Point(4,height-1),new Point(0,height-7)});
        g.DrawLine(line,0,0,0,height-7);g.DrawLine(line,0,height-7,4,height-1);g.DrawLine(line,4,height-1,30,height-1);
        using var stripe=new Pen(Theme.Text,1.5f);for(int i=0;i<4;i++)g.DrawLine(stripe,5+i*4,24,9+i*4,17);
        g.DrawLine(line,32,7,32,38);
        Tracked(g,"SYS: LOCAL",40,4,12,.6f,Theme.Text);
        Tracked(g,"SECTOR "+ordinal.ToString("00"),142,4,12,.6f,Theme.Text);
        Tracked(g,$"GRID {columns} × {rows}",240,4,12,.6f,Theme.Muted);
        Tracked(g,"PATH: ORIGINAL",240,24,12,.6f,Theme.Muted);
        Tracked(g,"REV."+revision.ToString("00"),40,24,12,.6f,Theme.Text);
        g.DrawLine(line,120,24,120,39);
        Tracked(g,modified.ToLocalTime().ToString("yyyy.MM.dd"),130,24,12,.6f,Theme.Text);
        g.DrawLine(line,35,height-1,110,height-1);g.DrawLine(line,119,height-1,width-72,height-1);
        g.DrawLine(line,width-61,height-1,width-33,height-1);g.DrawLine(line,width-33,height-1,width-1,7);
        g.DrawLine(line,width-23,height-1,width-1,height-1);g.DrawLine(line,width-1,height-1,width-1,24);
        Cross(g,width-14,34,Theme.Muted);
    }
    public static void Flag(Graphics g,int width,int height)
    {
        Prepare(g);
        using var signal=new SolidBrush(Theme.Accent);using var ink=new SolidBrush(Theme.Background);float s=width/63f,t=height/102f;
        g.FillPolygon(signal,new[]{new PointF(0,0),new PointF(width-7*s,0),new PointF(width,7*t),new PointF(width,height),new PointF(0,43*t)});
        for(int y=0;y<2;y++)for(int x=0;x<2;x++)g.FillRectangle(ink,(24+x*13)*s,(15+y*13)*t,11*s,11*t);
    }
}

internal sealed class ArtPanel:Panel
{
    public ArtPanel(){DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e){FrameArt.Prepare(e.Graphics);base.OnPaint(e);}
}
