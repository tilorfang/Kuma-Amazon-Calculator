using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace AmazonFreight {
internal static class Shapes {
    public static GraphicsPath Round(RectangleF r,float radius){
        float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));var p=new GraphicsPath();
        p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
}
internal sealed class RoundedPanel:Panel {
    public Color BorderColor=Color.FromArgb(220,228,236);public int Radius=13;
    public RoundedPanel(){DoubleBuffered=true;ResizeRedraw=true;}
    protected override void OnPaintBackground(PaintEventArgs e){
        e.Graphics.Clear(Parent==null?SystemColors.Control:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=Shapes.Round(new RectangleF(.5f,.5f,Width-1,Height-1),Radius))using(var brush=new SolidBrush(BackColor))using(var pen=new Pen(BorderColor)){e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(pen,path);}
    }
}
internal sealed class RoundedButton:Button {
    public RoundedButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);DoubleBuffered=true;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;}
    protected override void OnPaintBackground(PaintEventArgs e){}
    protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(Parent.BackColor);
        Color fill=Enabled?BackColor:Color.FromArgb(234,239,243);
        if(Enabled&&ClientRectangle.Contains(PointToClient(Cursor.Position)))fill=ControlPaint.Light(fill,.08f);
        using(var path=Shapes.Round(new RectangleF(.5f,.5f,Width-1,Height-1),9))using(var brush=new SolidBrush(fill))using(var pen=new Pen(FlatAppearance.BorderColor)){e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(pen,path);}
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:SystemColors.GrayText,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
    }
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);Invalidate();}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);Invalidate();}
}
internal sealed class TransportMenuRenderer:ToolStripProfessionalRenderer {
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){
        using(var fill=new SolidBrush(e.Item.Selected?Color.FromArgb(229,244,240):Color.White))e.Graphics.FillRectangle(fill,new Rectangle(Point.Empty,e.Item.Size));
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){
        TextRenderer.DrawText(e.Graphics,e.Text,e.TextFont,new Rectangle(Point.Empty,e.Item.Size),e.Item.Selected?Color.FromArgb(15,118,110):Color.FromArgb(27,43,65),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
    }
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){}
}
internal sealed class RoundedComboFace:Control {
    readonly ComboBox source;
    readonly ToolStripDropDownMenu popup=new ToolStripDropDownMenu{AutoSize=true,ShowImageMargin=false,ShowCheckMargin=false,Padding=new Padding(4),Margin=Padding.Empty,BackColor=Color.White,DropShadowEnabled=true,Renderer=new TransportMenuRenderer()};
    int lastMouseDown;bool hadMouseDown,opened;
    public RoundedComboFace(ComboBox source){
        this.source=source;DoubleBuffered=true;ResizeRedraw=true;TabStop=true;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.ComboBox;
        source.SelectedIndexChanged+=delegate{Invalidate();};
        popup.Closing+=delegate(object sender,ToolStripDropDownClosingEventArgs e){
            if(e.CloseReason==ToolStripDropDownCloseReason.AppClicked&&RectangleToScreen(ClientRectangle).Contains(Cursor.Position)&&hadMouseDown&&unchecked(Environment.TickCount-lastMouseDown)<SystemInformation.DoubleClickTime)e.Cancel=true;
        };
        popup.VisibleChanged+=delegate{opened=popup.Visible;if(opened){using(var path=Shapes.Round(new RectangleF(0,0,popup.Width,popup.Height),9)){Region old=popup.Region;popup.Region=new Region(path);if(old!=null)old.Dispose();}}Invalidate();};
        popup.Paint+=delegate(object sender,PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Shapes.Round(new RectangleF(.5f,.5f,popup.Width-1,popup.Height-1),9))using(var pen=new Pen(Color.FromArgb(210,222,233)))e.Graphics.DrawPath(pen,path);};
    }
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.Clear(Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        Color edge=Focused||opened?Color.FromArgb(15,118,110):Color.FromArgb(210,222,233);
        using(var path=Shapes.Round(new RectangleF(.5f,.5f,Width-1,Height-1),9))using(var fill=new SolidBrush(BackColor))using(var border=new Pen(edge)){e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(border,path);}
        TextRenderer.DrawText(e.Graphics,source.Text,Font,new Rectangle(12,0,Width-24,Height),ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
        using(var pen=new Pen(Color.FromArgb(100,116,139),1.5f)){int x=Width-21,y=Height/2;e.Graphics.DrawLines(pen,new[]{new Point(x-3,y-2),new Point(x,y+1),new Point(x+3,y-2)});}
    }
    void Commit(int index){if(index>=0&&index<source.Items.Count)source.SelectedIndex=index;popup.Close();Focus();}
    void OpenMenu(){
        if(opened){popup.Close();return;}
        popup.Items.Clear();popup.Font=Font;popup.MinimumSize=new Size(Width,0);popup.MaximumSize=new Size(Width,0);
        for(int i=0;i<source.Items.Count;i++){
            int index=i;
            var item=new ToolStripMenuItem(Convert.ToString(source.Items[i])){AutoSize=false,Size=new Size(Width-8,34),Margin=Padding.Empty,Padding=Padding.Empty};
            item.Click+=delegate{Commit(index);};popup.Items.Add(item);
        }
        Point location=PointToScreen(new Point(0,Height+4));Rectangle area=Screen.FromControl(this).WorkingArea;
        if(location.Y+source.Items.Count*34+8>area.Bottom)location.Y=PointToScreen(Point.Empty).Y-source.Items.Count*34-12;
        popup.Show(location);if(source.SelectedIndex>=0)popup.Items[source.SelectedIndex].Select();
    }
    internal void VerifyChoices(string preview){int original=source.SelectedIndex;for(int i=0;i<source.Items.Count;i++){OpenMenu();if(i==0){using(var bitmap=new Bitmap(popup.Width,popup.Height)){popup.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(preview);}}if(popup.Width!=Width||popup.Height<source.Items.Count*34)throw new Exception("Dropdown size and rows");((ToolStripMenuItem)popup.Items[i]).PerformClick();if(source.SelectedIndex!=i)throw new Exception("Rounded selector choice failed");}source.SelectedIndex=original;}
    internal void OpenForCloseCheck(){if(!opened)OpenMenu();}
    internal void VerifyDoubleClickInteraction(){
        if(opened)popup.Close();hadMouseDown=false;
        OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,Width/2,Height/2,0));Application.DoEvents();
        OnMouseDown(new MouseEventArgs(MouseButtons.Left,2,Width/2,Height/2,0));Application.DoEvents();
        if(!opened)throw new Exception("Double click must leave the transport list open");
    }
    protected override void OnMouseDown(MouseEventArgs e){
        if(e.Button==MouseButtons.Left){int now=Environment.TickCount;if(e.Clicks>1||(hadMouseDown&&unchecked(now-lastMouseDown)<SystemInformation.DoubleClickTime))return;lastMouseDown=now;hadMouseDown=true;Focus();OpenMenu();}base.OnMouseDown(e);
    }
    protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
    protected override void OnKeyDown(KeyEventArgs e){
        if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter||e.KeyCode==Keys.F4||(e.Alt&&e.KeyCode==Keys.Down)){OpenMenu();e.Handled=true;}
        else if(e.KeyCode==Keys.Up||e.KeyCode==Keys.Down||e.KeyCode==Keys.Left||e.KeyCode==Keys.Right){source.SelectedIndex=Math.Max(0,Math.Min(source.Items.Count-1,source.SelectedIndex+(e.KeyCode==Keys.Up||e.KeyCode==Keys.Left?-1:1)));e.Handled=true;}
        base.OnKeyDown(e);
    }
    protected override void Dispose(bool disposing){if(disposing)popup.Dispose();base.Dispose(disposing);}
}
internal sealed class CaptureOverlay:Form {
    readonly Bitmap screen;Point start;bool dragging;Rectangle selection;public Bitmap SelectedImage;
    public CaptureOverlay(Bitmap screenshot,Rectangle bounds){
        screen=screenshot;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;Bounds=bounds;TopMost=true;ShowInTaskbar=false;KeyPreview=true;Cursor=Cursors.Cross;DoubleBuffered=true;
        AccessibleName="截图框选区域，按 Esc 取消";
    }
    public static Rectangle Selection(Point a,Point b,Size size){return Rectangle.Intersect(new Rectangle(Point.Empty,size),Rectangle.FromLTRB(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y)));}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.DrawImageUnscaled(screen,0,0);using(var dim=new SolidBrush(Color.FromArgb(125,8,19,32)))e.Graphics.FillRectangle(dim,ClientRectangle);
        if(selection.Width>0&&selection.Height>0){e.Graphics.DrawImage(screen,selection,selection,GraphicsUnit.Pixel);using(var pen=new Pen(Color.FromArgb(44,211,192),2))e.Graphics.DrawRectangle(pen,selection);}
        int x=Math.Max(20,-Left+20),y=Math.Max(20,-Top+20);
        using(var brush=new SolidBrush(Color.FromArgb(235,19,38,54)))e.Graphics.FillRectangle(brush,x,y,340,42);
        TextRenderer.DrawText(e.Graphics,"拖动框选截图 · 松开识别 · Esc / 右键取消",Font,new Rectangle(x+10,y,325,42),Color.White,TextFormatFlags.VerticalCenter);
    }
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Right){DialogResult=DialogResult.Cancel;Close();return;}if(e.Button==MouseButtons.Left){start=e.Location;dragging=true;Capture=true;selection=Rectangle.Empty;Invalidate();}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging){selection=Selection(start,e.Location,screen.Size);Invalidate();}}
    protected override void OnMouseUp(MouseEventArgs e){
        base.OnMouseUp(e);if(e.Button!=MouseButtons.Left||!dragging)return;dragging=false;Capture=false;selection=Selection(start,e.Location,screen.Size);
        if(selection.Width<8||selection.Height<8){selection=Rectangle.Empty;Invalidate();return;}
        SelectedImage=screen.Clone(selection,PixelFormat.Format24bppRgb);DialogResult=DialogResult.OK;Close();
    }
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Escape){DialogResult=DialogResult.Cancel;e.Handled=true;Close();}}
    internal static Bitmap VerifyCrop(Bitmap image,bool reverse){
        using(var form=new CaptureOverlay(image,new Rectangle(Point.Empty,image.Size))){
            Point a=reverse?new Point(image.Width,image.Height):Point.Empty,b=reverse?Point.Empty:new Point(image.Width,image.Height);
            form.OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,a.X,a.Y,0));form.OnMouseMove(new MouseEventArgs(MouseButtons.Left,0,b.X,b.Y,0));form.OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,b.X,b.Y,0));
            if(form.DialogResult!=DialogResult.OK||form.SelectedImage==null)throw new Exception("Capture selection failed");return form.SelectedImage;
        }
    }
    internal static void VerifyCancel(Bitmap image){using(var form=new CaptureOverlay(image,new Rectangle(Point.Empty,image.Size))){form.OnKeyDown(new KeyEventArgs(Keys.Escape));if(form.DialogResult!=DialogResult.Cancel||form.SelectedImage!=null)throw new Exception("Capture cancellation failed");}}
}
}
