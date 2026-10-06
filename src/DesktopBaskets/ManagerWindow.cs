

namespace DesktopBaskets;

internal sealed class ManagerWindow : ModernWindow
{
    readonly App app;
    readonly ListBox baskets=new(){Dock=DockStyle.Fill,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=62};
    readonly ListView entries=new(){Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=true,HideSelection=false};
    readonly Label status=new(){Dock=DockStyle.Bottom,Height=50,Padding=new Padding(20,10,20,10)};
    readonly Label detail=new(){Dock=DockStyle.Top,Height=76,Padding=new Padding(16,12,16,8)};
    readonly Button toggle;
    bool loading;
    public Basket? Selected => baskets.SelectedItem as Basket;
    public bool Exiting { get; set; }
    public string StatusText => status.Text;
    public static readonly int TaskbarCreated=(int)RegisterWindowMessage("TaskbarCreated");
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);
    public ManagerWindow(App app)
    {
        this.app=app;Theme.Form(this);Text="Desktop Baskets · 桌面分類工具";
        Icon=Theme.AppIcon;ClientSize=new Size(1060,680);MinimumSize=new Size(850,560);
        var top=new WindowHeaderPanel{Dock=DockStyle.Top,Height=132,Padding=new Padding(24)};
        var captionButtons=new[]{new CaptionButton(this,CaptionAction.Minimize),new CaptionButton(this,CaptionAction.Maximize),new CaptionButton(this,CaptionAction.Close)};
        top.Paint+=(_,e)=>
        {
            FrameArt.Prepare(e.Graphics);
            FrameArt.Label(e.Graphics,app.Store.State.Baskets.Count.ToString("00"),new RectangleF(24,26,88,67),FrameArt.Display(54),Theme.Text);
            using var slash=new Pen(Theme.FrameLine);e.Graphics.DrawLine(slash,120,86,150,29);
            FrameArt.Tracked(e.Graphics,"LOCAL / DESKTOP",166,14,12,2,Theme.Text);
            FrameArt.Label(e.Graphics,"DESKTOP",new RectangleF(166,38,245,50),FrameArt.Display(38),Theme.Text);
            FrameArt.Tracked(e.Graphics,"OBJECTS",441,12,12,1,Theme.Muted);
            FrameArt.Label(e.Graphics,app.Store.State.Baskets.Sum(b=>b.Entries.Count).ToString("000"),new RectangleF(441,31,104,37),FrameArt.Data(28),Theme.Text);
            using var accent=new SolidBrush(Theme.Accent);e.Graphics.FillRectangle(accent,24,123,52,3);
            using var line=new Pen(Theme.Line);e.Graphics.DrawLine(line,88,124,top.Width-24,124);
        };
        top.Controls.Add(new Label{Text="原位分類  /  拖入檔案 · 磁吸對齊 · 按格縮放",Location=new Point(88,90),AutoSize=true,ForeColor=Theme.Muted,Font=Theme.Body(9)});
        var commands=new FlowLayoutPanel{Width=418,Height=50,FlowDirection=FlowDirection.LeftToRight,Padding=new Padding(0,4,0,0)};
        commands.Controls.Add(Theme.Button("＋ 新增分類",(_,_)=>app.NewBasket(),true));
        toggle=Theme.Button("暫停桌面",(_,_)=>app.Toggle());commands.Controls.Add(toggle);
        commands.Controls.Add(Theme.Button("關閉並退出",(_,_)=>app.Quit()));top.Controls.Add(commands);
        foreach(var button in captionButtons)top.Controls.Add(button);
        void LayoutCaption()
        {
            int buttonWidth=(int)Math.Round(52*DeviceDpi/96.0),buttonHeight=(int)Math.Round(44*DeviceDpi/96.0);
            for(int i=0;i<captionButtons.Length;i++)captionButtons[i].Bounds=new Rectangle(top.Width-(captionButtons.Length-i)*buttonWidth,0,buttonWidth,buttonHeight);
            commands.Location=new Point(top.Width-commands.Width-24,70);
            CaptionRegion=top.Bounds;
        }
        top.Resize+=(_,_)=>LayoutCaption();top.LocationChanged+=(_,_)=>LayoutCaption();
        Resize+=(_,_)=>{LayoutCaption();foreach(var button in captionButtons)button.Invalidate();};
        DpiChanged+=(_,_)=>LayoutCaption();LayoutCaption();
        var content=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24,0,24,0),ColumnCount=2};
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,260));content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        var left=new Panel{Dock=DockStyle.Fill,Padding=new Padding(0,0,14,0)};
        baskets.BackColor=Theme.Panel;baskets.ForeColor=Theme.Text;baskets.BorderStyle=BorderStyle.None;
        baskets.DrawItem+=(_,e)=>
        {
            if(e.Index<0)return;var b=(Basket)baskets.Items[e.Index];
            using var fill=new SolidBrush((e.State&DrawItemState.Selected)!=0?Theme.Raised:Theme.Panel);e.Graphics.FillRectangle(fill,e.Bounds);
            if((e.State&DrawItemState.Selected)!=0){using var stripe=new SolidBrush(Theme.Accent);e.Graphics.FillRectangle(stripe,e.Bounds.X,e.Bounds.Y,3,e.Bounds.Height);}
            FrameArt.Label(e.Graphics,(e.Index+1).ToString("00"),new RectangleF(e.Bounds.X+13,e.Bounds.Y+11,38,36),FrameArt.Display(25),Theme.Text);
            TextRenderer.DrawText(e.Graphics,b.Name,Font,new Rectangle(e.Bounds.X+59,e.Bounds.Y+9,e.Bounds.Width-73,23),Theme.Text,TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics,$"{b.Entries.Count:000} 項  /  {(b.Collapsed?"收合":b.Locked?"鎖定":"可排位")}",Theme.Body(9),new Point(e.Bounds.X+59,e.Bounds.Y+35),Theme.Muted);
            e.DrawFocusRectangle();
        };
        baskets.SelectedIndexChanged+=(_,_)=>{if(!loading)RefreshEntries();};
        baskets.DoubleClick+=(_,_)=>{if(Selected is Basket b)app.EditBasket(b);};
        var leftActions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=132};
        leftActions.Controls.Add(Theme.Button("分類設定",(_,_)=>{if(Selected is Basket b)app.EditBasket(b);}));
        leftActions.Controls.Add(Theme.Button("移除分類",(_,_)=>{if(Selected is Basket b)app.DeleteBasket(b);}));
        leftActions.Controls.Add(Theme.Button("開啟設定資料夾",(_,_)=>Theme.Try(()=>Theme.Open(app.Store.Root))));
        left.Controls.Add(baskets);left.Controls.Add(leftActions);
        var right=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Panel};
        entries.BackColor=Theme.Panel;entries.ForeColor=Theme.Text;entries.BorderStyle=BorderStyle.None;
        entries.Columns.Add("檔案／程式",220);entries.Columns.Add("方式",84);entries.Columns.Add("所在位置",330);
        entries.HeaderStyle=ColumnHeaderStyle.None;
        var fileHeader=new ArtPanel{Dock=DockStyle.Top,Height=30,BackColor=Theme.Raised};
        fileHeader.Paint+=(_,e)=>
        {
            int position=0;using var divider=new Pen(Theme.Line);
            foreach(ColumnHeader column in entries.Columns)
            {
                TextRenderer.DrawText(e.Graphics,column.Text,Font,new Rectangle(position+10,5,column.Width-16,22),Theme.Text,TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
                position+=column.Width;e.Graphics.DrawLine(divider,position,7,position,23);
            }
        };
        entries.ColumnWidthChanged+=(_,_)=>fileHeader.Invalidate();
        entries.DoubleClick+=(_,_)=>{if(entries.SelectedItems.Count>0&&entries.SelectedItems[0].Tag is Entry e)Theme.Try(()=>Theme.Open(e.Path));};
        entries.KeyDown+=(_,e)=>
        {
            if(e.KeyCode==Keys.Delete)RemoveSelected();
            else if((e.KeyCode==Keys.Apps||(e.KeyCode==Keys.F10&&e.Shift))&&entries.SelectedItems.Count>0&&entries.SelectedItems[0].Tag is Entry item)
            {app.ShowEntryMenu(item,entries.PointToScreen(entries.SelectedItems[0].Bounds.Location));e.Handled=true;e.SuppressKeyPress=true;}
        };
        entries.MouseUp+=(_,e)=>{if(e.Button==MouseButtons.Right&&entries.GetItemAt(e.X,e.Y)?.Tag is Entry item)app.ShowEntryMenu(item,Native.PhysicalCursor);};
        var rightActions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=56,Padding=new Padding(12,8,0,0)};
        rightActions.Controls.Add(Theme.Button("加入桌面檔案",(_,_)=>{if(Selected is Basket b)app.PickDesktop(b);}));
        rightActions.Controls.Add(Theme.Button("加入外部檔案連結",(_,_)=>PickExternal()));
        rightActions.Controls.Add(Theme.Button("移出分類回桌面",(_,_)=>RemoveSelected()));
        right.Controls.Add(entries);right.Controls.Add(fileHeader);right.Controls.Add(detail);right.Controls.Add(rightActions);
        content.Controls.Add(left,0,0);content.Controls.Add(right,1,0);
        Controls.Add(content);Controls.Add(top);Controls.Add(status);
        AllowDrop=true;entries.AllowDrop=true;
        entries.DragEnter+=(_,e)=>{if(Selected!=null&&e.Data?.GetDataPresent(DataFormats.FileDrop)==true)e.Effect=DragDropEffects.Copy;};
        entries.DragDrop+=(_,e)=>{if(Selected is Basket b&&e.Data?.GetData(DataFormats.FileDrop) is string[] paths)app.AddPaths(b,paths);};
        FormClosing+=(_,e)=>{if(!Exiting){e.Cancel=true;Hide();}};
        Shown+=(_,_)=>RefreshState();
        RefreshState();
    }
    void PickExternal()
    {
        if(Selected is not Basket b)return;
        using var dialog=new OpenFileDialog{Multiselect=true,Title="加入檔案連結（原檔保留在原位）"};
        if(dialog.ShowDialog(this)==DialogResult.OK)app.AddPaths(b,dialog.FileNames);
    }
    void RemoveSelected()
    {
        if(Selected is not Basket b)return;
        foreach(var entry in entries.SelectedItems.Cast<ListViewItem>().Select(i=>(Entry)i.Tag!).ToArray())app.RemoveEntry(b,entry);
    }
    public void SetStatus(string text,bool error=false){status.Text=text;status.ForeColor=error?Color.FromArgb(255,184,147):Theme.Muted;}
    public void RefreshState(string? selected=null)
    {
        selected??=Selected?.Id;loading=true;baskets.BeginUpdate();baskets.Items.Clear();
        foreach(var basket in app.Store.State.Baskets)baskets.Items.Add(basket);
        int index=app.Store.State.Baskets.FindIndex(b=>b.Id==selected);
        if(baskets.Items.Count>0)baskets.SelectedIndex=Math.Max(0,index);
        baskets.EndUpdate();loading=false;
        toggle.Text=app.Store.State.Enabled?"暫停並還原圖示":"啟用桌面";
        RefreshEntries();
        Invalidate(true);
    }
    void RefreshEntries()
    {
        entries.BeginUpdate();entries.Items.Clear();
        if(Selected is Basket b)
        {
            detail.Text=b.Name+"  /  "+b.Entries.Count+" 個項目\n"+$"{b.Columns} × {b.Rows} 格  ·  玻璃底色 {b.OpacityPercent}%  ·  "+(b.Locked?"位置與大小已鎖定":"雙擊框標題可改名");
            foreach(var entry in b.Entries)
            {
                var item=new ListViewItem(entry.DisplayName){Tag=entry};
                item.SubItems.Add("原位分類");item.SubItems.Add(entry.Path);entries.Items.Add(item);
            }
        }
        else detail.Text="從「新增分類」開始\n例如：遊戲、工作、雜項。新增時會直接放上桌面，並自動擠開被擋住的圖示。";
        entries.EndUpdate();
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==TaskbarCreated)BeginInvoke(new Action(app.Reconnect));
        else if(m.Msg==0x7E)BeginInvoke(new Action(app.DisplayChanged));
        base.WndProc(ref m);
    }
}
