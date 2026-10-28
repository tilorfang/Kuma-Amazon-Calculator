using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace AmazonFreight {
internal sealed class MacPageBar : Control {
    public int Maximum, PageSize, Value;
    public Action<int> Changed;
    bool dragging,hover; int dragOffset;
    public MacPageBar(){DoubleBuffered=true;TabStop=false;Width=14;Cursor=Cursors.Default;}
    Rectangle Thumb { get { int h=Math.Min(Height,Math.Max(36,(int)((long)Height*PageSize/Math.Max(1,Maximum+PageSize))));return new Rectangle(4,Maximum>0?(int)((long)(Height-h)*Value/Maximum):0,7,h); } }
    public void SetValue(int value){Value=Math.Max(0,Math.Min(Maximum,value));Invalidate();if(Changed!=null)Changed(Value);}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;Rectangle t=Thumb;if(t.Height<1)return;using(var p=Shapes.Round(t,3.5f))using(var b=new SolidBrush(dragging||hover?Color.FromArgb(130,139,149):Color.FromArgb(175,182,190)))e.Graphics.FillPath(b,p);}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;dragging=true;Capture=true;dragOffset=Thumb.Contains(e.Location)?e.Y-Thumb.Top:Thumb.Height/2;MoveThumb(e.Y);}
    void MoveThumb(int y){SetValue((int)Math.Round((double)(y-dragOffset)*Maximum/Math.Max(1,Height-Thumb.Height)));}
    protected override void OnMouseMove(MouseEventArgs e){if(dragging)MoveThumb(e.Y);base.OnMouseMove(e);}
    protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;Invalidate();base.OnMouseUp(e);}
}
internal sealed class PageWheelFilter:IMessageFilter {
    readonly Form owner;readonly Action<int> scroll;
    public PageWheelFilter(Form owner,Action<int> scroll){this.owner=owner;this.scroll=scroll;}
    public bool PreFilterMessage(ref Message m){
        if(m.Msg!=0x20a||!owner.Visible||Form.ActiveForm!=owner||!owner.RectangleToScreen(owner.ClientRectangle).Contains(Cursor.Position))return false;
        Control target=Control.FromHandle(m.HWnd);
        for(Control c=target;c!=null;c=c.Parent){if(c is ToolStripDropDown||c is ListBox)return false;DataGridView grid=c as DataGridView;if(grid!=null&&grid.Rows.Count>grid.DisplayedRowCount(false))return false;}
        int delta=(short)((m.WParam.ToInt64()>>16)&0xffff);scroll(-delta*64/120);return true;
    }
}
internal partial class MainForm {
    void ApplyVisualRefinement(Panel packaging){
        foreach(Control c in freightBody.Controls){
            Label label=c as Label;
            if(label!=null&&label.Text=="亚马逊运费计算器"){label.SetBounds(22,24,520,56);label.TextAlign=ContentAlignment.MiddleLeft;}
            if(c is Button&&c.Top==818)c.Top=844;
        }
        Button[] buttons={pasteButton,captureButton,fileButton};
        for(int i=0;i<buttons.Length;i++){buttons[i].SetBounds(570+i*140,68,128,38);buttons[i].BringToFront();}
        packaging.SetBounds(22,124,956,360);
        foreach(Control c in packaging.Controls) {
            if(c is Label&&(c.Text=="包装信息"||c.Text=="运输设置"))c.Visible=false;
            if(c is Label&&c.Text.Contains("包装信息与运输设置"))c.SetBounds(18,12,920,28);
        }
        TextBox[] fields={weight,l,w,h};TextBox[] imperial={weightImperial,lengthImperial,widthImperial,heightImperial};
        for(int i=0;i<fields.Length;i++){imperial[i].Left=fields[i].Parent.Left;imperial[i].Width=fields[i].Parent.Width;imperial[i].TextAlign=HorizontalAlignment.Center;}
        foreach(Control c in packaging.Controls)if(c is Label&&c.Text=="包装宽 · CM")c.Width=w.Parent.Width-32;
        actualResult.Parent.Top=498;
        Panel fba=(Panel)fbaPrice.Parent.Parent;fba.Top=628;
        Label title=null;
        foreach(Control c in fba.Controls){
            if(c is Label&&c.Text.Contains("FBA 配送费"))title=(Label)c;
            if(c is Label&&c.Text=="美国站")c.Visible=false;

        }
        if(title!=null){
            title.Text="02  FBA配送费";
            Font countryFont=new Font("Microsoft YaHei UI",9F);
            int titleWidth=TextRenderer.MeasureText(title.Text,title.Font).Width;
            int countryWidth=TextRenderer.MeasureText("（美国站）",countryFont).Width;
            int start=18;
            title.SetBounds(start,12,titleWidth,28);
            Label country=LabelAt(fba,"（美国站）",start+titleWidth+6,14,countryWidth,26,9,muted,false);
            country.Font=countryFont;
        }
        fbaSeasonHint.TextAlign=ContentAlignment.MiddleCenter;
        fbaSeasonHint.Left=fbaSeason.Parent.Left;fbaSeasonHint.Width=fbaSeason.Parent.Width;
        exchangeHint.Visible=false;
        message.Top=892;
        foreach(Control c in packaging.Controls)if(c is Label&&c.Text=="运输方式")((Label)c).TextAlign=ContentAlignment.MiddleCenter;
        foreach(Control c in fba.Controls)if(c is Label&&(c.Text=="淡旺季费率"||c.Text=="尺寸分档"))((Label)c).TextAlign=ContentAlignment.MiddleCenter;
        fbaTierLabel.TextAlign=ContentAlignment.MiddleCenter;
        UseRoundedTransport();CenterCombo(fbaSeason);
    }
    void UseRoundedTransport() {
        Control frame=mode.Parent;
        var face=new RoundedComboFace(mode){Bounds=new Rectangle(0,0,frame.Width,frame.Height),Font=new Font(mode.Font,FontStyle.Bold),BackColor=Color.White,ForeColor=ink,AccessibleName="运输方式"};
        mode.Visible=false;mode.TabStop=false;frame.Controls.Add(face);face.BringToFront();
    }
    internal static void BoldButtons(Control parent) {
        foreach(Control c in parent.Controls){if(c is Button)c.Font=new Font(c.Font,c.Font.Style|FontStyle.Bold);BoldButtons(c);}
    }
    void VerifyButtonsAndCaption(string folder) {
        VerifyButtonsRecursive(this);
        profitForm.CheckCostCaption();
        RoundedComboFace selector=null;foreach(Control c in mode.Parent.Controls)if(c is RoundedComboFace)selector=(RoundedComboFace)c;
        if(selector==null||mode.Visible)throw new Exception("Rounded transport selector");
        selector.VerifyChoices(System.IO.Path.Combine(folder,"transport-dropdown.png"));
    }
    static void VerifyButtonsRecursive(Control parent) {
        foreach(Control c in parent.Controls) {
            if(c is Button&&!c.Font.Bold)throw new Exception("Button text not bold: "+c.Text);
            if(c is Button&&c.Text=="保存分享图")throw new Exception("Share button removed");
            VerifyButtonsRecursive(c);
        }
    }
    static void CenterCombo(ComboBox combo){
        combo.DrawMode=DrawMode.OwnerDrawFixed;
        combo.DrawItem+=delegate(object sender,DrawItemEventArgs e){
            e.DrawBackground();string text=e.Index>=0?Convert.ToString(combo.Items[e.Index]):combo.Text;
            TextRenderer.DrawText(e.Graphics,text,combo.Font,e.Bounds,e.ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
        };
    }
    MacPageBar pageBar;PageWheelFilter pageWheel;int pageOffset;
    bool testingSession;string[] persistedPrices;readonly string[] lastAskedPrices=new string[3];
    TextBox ImperialReading(Control parent,string text,int x,int y,int width){
        var box=new TextBox{Text=text,Location=new Point(x,y),Size=new Size(width,24),AutoSize=false,BorderStyle=BorderStyle.None,ReadOnly=true,ShortcutsEnabled=true,BackColor=parent.BackColor,ForeColor=muted,Font=new Font("Microsoft YaHei UI",9F),TextAlign=HorizontalAlignment.Center};
        parent.Controls.Add(box);return box;
    }
    void ConfirmRateChange(Func<DialogResult> ask=null){
        if(persistedPrices==null||(testingSession&&ask==null))return;
        prices[mode.SelectedIndex]=RawText(rate);
        for(int index=0;index<prices.Length;index++) {
            decimal value,saved;
            if(!decimal.TryParse(prices[index],NumberStyles.Number,CultureInfo.InvariantCulture,out value)||value<0)continue;
            if(decimal.TryParse(persistedPrices[index],NumberStyles.Number,CultureInfo.InvariantCulture,out saved)&&saved==value)continue;
            string transport=new[]{"海运","空运","快递"}[index];
            DialogResult answer=ask!=null?ask():MessageBox.Show(this,"是否将"+transport+"运费单价 "+Reading(value)+" 元/KG 保存为下次使用？\r\n选择“否”则不保存这次修改。","保存运费单价",MessageBoxButtons.YesNo,MessageBoxIcon.Question);
            if(answer==DialogResult.Yes)persistedPrices[index]=prices[index];
        }
    }
    void SetupMacScroll(){
        AutoScrollMinSize=Size.Empty;AutoScroll=false;
        pageBar=new MacPageBar{BackColor=BackColor,Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Right};Controls.Add(pageBar);
        pageBar.Changed=delegate(int value){pageOffset=value;calculatorPage.Top=-value;pageBar.BringToFront();};
        pageWheel=new PageWheelFilter(this,delegate(int delta){SetPageScroll(pageOffset+delta);});Application.AddMessageFilter(pageWheel);
        ClientSizeChanged+=delegate{UpdateMacScroll();};
        KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.PageDown||e.KeyCode==Keys.PageUp){SetPageScroll(pageOffset+(e.KeyCode==Keys.PageDown?1:-1)*(ClientSize.Height-60));e.Handled=true;}};
        FormClosed+=delegate{Application.RemoveMessageFilter(pageWheel);};
    }
    void UpdateMacScroll(){
        if(pageBar==null)return;
        pageBar.SetBounds(ClientSize.Width-16,8,14,Math.Max(1,ClientSize.Height-16));
        pageBar.PageSize=ClientSize.Height;pageBar.Maximum=Math.Max(0,calculatorPage.Height-ClientSize.Height);
        pageBar.Visible=pageBar.Maximum>0;pageBar.SetValue(pageOffset);pageBar.BringToFront();
    }
    void SetPageScroll(int value){if(pageBar!=null)pageBar.SetValue(value);}
    public new void ScrollControlIntoView(Control control){
        if(control==profitForm)SetPageScroll(profitForm.Top-12);
    }
    void VerifyNewUi(string folder){
        if(pasteButton.GetType()!=typeof(RoundedButton)||pasteButton.UseVisualStyleBackColor)throw new Exception("Custom paste button rendering");
        if(weight.Font.Name!="Microsoft YaHei UI"||weight.Font.Size!=15F||feeResult.Font.Size!=25F)throw new Exception("Freight font consistency: "+weight.Font+" / "+feeResult.Font);
        if(fbaSeasonHint.Left!=fbaSeason.Parent.Left||fbaSeasonHint.Width!=fbaSeason.Parent.Width||fbaSeasonHint.TextAlign!=ContentAlignment.MiddleCenter)throw new Exception("Season date alignment");
        foreach(Control c in fbaSeasonHint.Parent.Controls)if(c is Label&&c.Text=="美国站"&&c.Visible)throw new Exception("Standalone country label removed");
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"font-report.txt"),"UI family: Microsoft YaHei UI (restored baseline)\r\nFreight inputs: "+weight.Font+"\r\nFreight result: "+feeResult.Font+"\r\nImperial readings: "+weightImperial.Font+"\r\nButtons: "+pasteButton.Font+"\r\nSeason dates: "+fbaSeasonHint.Font+"\r\nFreight input frame height: "+weight.Parent.Height+"; column pitch: "+(l.Parent.Left-weight.Parent.Left)+"; column gap: "+(l.Parent.Left-weight.Parent.Right)+"\r\n"+profitForm.FontDetails());
        if(mode.SelectedIndex!=0||fbaSeason.SelectedIndex!=1)throw new Exception("Default sea and 2026 peak");
        if(profitLinked.Visible||profitLinkNote.Visible)throw new Exception("Removed linked labels");
        foreach(TextBox box in new[]{weightImperial,lengthImperial,widthImperial,heightImperial}){box.SelectAll();if(!box.ReadOnly||!box.ShortcutsEnabled||box.SelectedText!=box.Text)throw new Exception("Imperial readings selectable");}
        string old=rate.Text;string saved=persistedPrices[0];string oldAsked=lastAskedPrices[0];
        string oldSettings=settings;bool oldTesting=testingSession;settings=System.IO.Path.Combine(folder,"rate-choice-settings.txt");testingSession=false;
        rate.Text="13";ConfirmRateChange(delegate{return DialogResult.No;});SaveSettings();if(persistedPrices[0]!=saved||System.IO.File.ReadAllLines(settings)[0]!=saved)throw new Exception("Declined rate must not persist");
        rate.Text="14";ConfirmRateChange(delegate{return DialogResult.Yes;});SaveSettings();if(persistedPrices[0]!="14"||System.IO.File.ReadAllLines(settings)[0]!="14")throw new Exception("Approved rate persists");
        persistedPrices[0]=saved;
        string[] savedRates=(string[])persistedPrices.Clone();
        mode.SelectedIndex=0;rate.Text="13";mode.SelectedIndex=1;rate.Text="56";
        int questions=0;ConfirmRateChange(delegate{return ++questions==1?DialogResult.No:DialogResult.Yes;});SaveSettings();
        string[] choice=System.IO.File.ReadAllLines(settings);
        if(questions!=2||choice[0]!=saved||choice[1]!="56")throw new Exception("Close checks each changed transport rate separately");
        persistedPrices=savedRates;prices=(string[])savedRates.Clone();mode.SelectedIndex=0;rate.Text=old;
        lastAskedPrices[0]=oldAsked;settings=oldSettings;testingSession=oldTesting;
        int oldOffset=pageOffset;SetPageScroll(100);if(calculatorPage.Top!=-100)throw new Exception("Custom scroll drag state");SetPageScroll(oldOffset);
        if(AutoScroll||VerticalScroll.Visible||pageBar.Height<ClientSize.Height-20)throw new Exception("Native scrollbars hidden and custom track fills viewport");
        if(!fbaDetail.Text.Contains("基础费用")||!fbaDetail.Text.Contains("税费")||!fbaDetail.Text.Contains("美元"))throw new Exception("FBA labelled formula");
    }
}
}
