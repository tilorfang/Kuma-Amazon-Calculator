using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AmazonFreight {
internal sealed class Package {
    public decimal? Weight, Length, Width, Height, Price;
    public string WeightSource = "", SizeSource = "", PriceSource = "";
}
internal static class Model {
    public static decimal Volume(decimal l, decimal w, decimal h, decimal divisor) { return l*w*h/divisor; }
    public static decimal Number(string s) { return decimal.Parse(s, CultureInfo.InvariantCulture); }
    public static Package Parse(string text) {
        Package p = new Package();
        string normalized=Regex.Replace(text,@"\s+", "").ToLowerInvariant().Replace('．','.').Replace('。','.').Replace('，',',').Replace("·", "");
        normalized=Regex.Replace(normalized,@"(?<=[\u4e00-\u9fff])[,.:：、](?=[\u4e00-\u9fff])", "");
        string labels=@"(?=(?:包装重量|包装尺寸|包裝重量|包裝尺寸|商品重量|商品尺寸|产品重量|产品尺寸|packageweight|packagedimensions|shippingweight|itemweight|itemdimensions|productweight|productdimensions|价格|售价|price|上架时间|评分|配送时长|prime配送时长|全部流量词|搜索推荐词|自然搜索词|广告流量词|color|size))";
        foreach(string original in Regex.Split(normalized,labels)){
            string s=original;
            if(!p.Price.HasValue){
                string priceLine=s.Replace('＄','$');
                Match price=Regex.Match(priceLine,@"^(?:价格|售价|price)[:：]?(?:us\$|usd|\$)?(\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?)(?![\d.,])(?:美元|usd)?");
                decimal amount;if(price.Success&&decimal.TryParse(price.Groups[1].Value,NumberStyles.AllowDecimalPoint|NumberStyles.AllowThousands,CultureInfo.InvariantCulture,out amount)&&amount>0){p.Price=amount;p.PriceSource=original;}
            }
            s = Regex.Replace(s, @"(?<=\d),(?=\d)", ".");
            bool packaging=Regex.IsMatch(s,@"^(?:包装重量|包装尺寸|包裝重量|包裝尺寸|packageweight|packagedimensions|shippingweight)");
            if (!packaging) continue;
            Match dim = Regex.Match(s, @"(?<![&\d.])(\d+(?:\.\d+)?)\s*[x×*]\s*(\d+(?:\.\d+)?)\s*[x×*]\s*(\d+(?:\.\d+)?)(inches|inch|in\b|英寸|cm|厘米|公分|mm|毫米)");
            if (Regex.IsMatch(s,@"^(?:包装尺寸|包裝尺寸|packagedimensions)") && dim.Success && !p.Length.HasValue) {
                decimal factor = dim.Groups[4].Value == "mm" || dim.Groups[4].Value == "毫米" ? .1m : Regex.IsMatch(dim.Groups[4].Value, "^(in|英寸)") ? 2.54m : 1m;
                decimal l=Number(dim.Groups[1].Value)*factor,w=Number(dim.Groups[2].Value)*factor,h=Number(dim.Groups[3].Value)*factor;
                if(l>0 && w>0 && h>0) {p.Length=l;p.Width=w;p.Height=h;p.SizeSource=original;}
            }
            if (!p.Weight.HasValue && Regex.IsMatch(s,@"^(?:包装重量|包裝重量|packageweight|shippingweight)")) {
                Match m=Regex.Match(s,@"(\d+(?:\.\d+)?)\s*(kg|千克|公斤)"); decimal factor=1m;
                if(!m.Success){m=Regex.Match(s,@"(\d+(?:\.\d+)?)\s*(g|克)");factor=.001m;}
                if(!m.Success){m=Regex.Match(s,@"(\d+(?:\.\d+)?)\s*(pounds?|lbs?|磅)");factor=.45359237m;}
                if(!m.Success){m=Regex.Match(s,@"(\d+(?:\.\d+)?)\s*(oz|ounces?|盎司)");factor=.028349523125m;}
                if(m.Success && Number(m.Groups[1].Value)>0){p.Weight=Number(m.Groups[1].Value)*factor;p.WeightSource=original;}
            }
        }
        return p;
    }
}
internal partial class MainForm:Form {
    readonly Color ink=Color.FromArgb(27,43,65), muted=Color.FromArgb(100,116,139), teal=Color.FromArgb(15,118,110);
    TextBox weight,l,w,h,divisor,rate,exchangeInput,raw,fbaPrice,fbaSpecialFee; TextBox actualResult,volumeResult,billResult,feeResult,usdResult,fbaFeeResult;
    TextBox weightImperial,lengthImperial,widthImperial,heightImperial; Label message,basis,source,exchangeHint,fbaDetail,fbaNote,fbaSeasonHint; ComboBox mode,fbaSeason; CheckBox fbaFuel; FbaResult currentFba; int fbaSeasonSaved=-1;bool fbaFuelSaved=true; PictureBox picture; Panel paste; Button pasteButton,fileButton,captureButton,widthUp,widthDown; Label fbaTierLabel,pieceCountLabel; int pieceCount=1;decimal? unitWidth,unitWeight; ToolTip widthTips=new ToolTip(); bool updating,busy,capturing; decimal? lastFee,lastUsd;
    string[] prices={"12","55",""}; string expressDivisor="",exchangeSaved="6.7"; string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KumaAmazonCalculator","settings.txt"); int previousMode;
    public MainForm(bool testing=false) {
        testingSession=testing;
        Text="Kuma亚马逊运费计算器";Font=new Font("Microsoft YaHei UI",10);ForeColor=ink;BackColor=Color.FromArgb(243,246,250);ClientSize=new Size(1000,896);AutoScroll=true;AutoScrollMinSize=new Size(1000,896);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;AutoScaleMode=AutoScaleMode.Dpi;StartPosition=FormStartPosition.CenterScreen;KeyPreview=true;
        if(!testing)LoadSettings();
        LabelAt(this,"亚马逊运费计算器",24,16,740,40,21,ink,true);
        LabelAt(this,"包装重量与材积重取大计费 · 截图 / 粘贴，即时识别",26,61,880,25,10,muted,false);
        Panel left=Card(22,98,280,360);LabelAt(left,"01  截图识别",16,12,248,28,12,ink,true);
        paste=new RoundedPanel{Location=new Point(16,48),Size=new Size(248,118),BackColor=Color.FromArgb(245,248,251),TabStop=true,AllowDrop=true,AccessibleName="截图粘贴框"};left.Controls.Add(paste);
        picture=new PictureBox{Location=new Point(5,5),Size=new Size(238,108),SizeMode=PictureBoxSizeMode.Zoom,BackColor=paste.BackColor};paste.Controls.Add(picture);
        Label hint=new Label{Text="Ctrl + V 粘贴截图\r\n或点击下方按钮框选屏幕",TextAlign=ContentAlignment.MiddleCenter,Location=new Point(5,5),Size=new Size(238,108),ForeColor=muted,BackColor=paste.BackColor,Font=new Font("Microsoft YaHei UI",9)};paste.Controls.Add(hint);
        hint.Click+=delegate{paste.Focus();};picture.Click+=delegate{paste.Focus();};
        DragEventHandler enter=delegate(object sender,DragEventArgs e){if(e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};
        DragEventHandler drop=async delegate(object sender,DragEventArgs e){string[] files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files!=null&&files.Length>0)await OpenImage(files[0]);};
        foreach(Control target in new Control[]{paste,picture,hint}){target.AllowDrop=true;target.DragEnter+=enter;target.DragDrop+=drop;}
        pasteButton=ButtonAt(left,"粘贴图片",16,178,119,true);pasteButton.Click+=async delegate{await PasteImage();};
        captureButton=ButtonAt(left,"点击截图",145,178,119,false);captureButton.Click+=async delegate{await CaptureScreen();};
        fileButton=ButtonAt(left,"选择图片文件",16,222,248,false);fileButton.Height=29;fileButton.Font=new Font("Microsoft YaHei UI",9);
        fileButton.Click+=async delegate{using(OpenFileDialog d=new OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*"})if(d.ShowDialog()==DialogResult.OK)await OpenImage(d.FileName);};
        source=LabelAt(left,"识别后请核对包装数据，可在右侧修改。",16,260,248,33,9,muted,false);
        Panel rawFrame=new RoundedPanel{Location=new Point(16,300),Size=new Size(248,45),BackColor=Color.FromArgb(248,250,252),Radius=8};left.Controls.Add(rawFrame);
        raw=new TextBox{Location=new Point(7,6),Size=new Size(234,32),Multiline=true,ScrollBars=ScrollBars.Vertical,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=rawFrame.BackColor,Font=new Font("Microsoft YaHei UI",8),AccessibleName="OCR识别原文"};rawFrame.Controls.Add(raw);
        Panel right=Card(318,98,660,360);LabelAt(right,"02  包装信息与运输设置",18,12,610,28,12,ink,true);
        LabelAt(right,"包装信息",18,48,145,22,9,muted,true);
        weight=Field(right,"包装重量 · KG",18,76,146,"");
        l=Field(right,"包装长 · CM",178,76,146,"");w=Field(right,"包装宽 · CM",338,76,146,"");h=Field(right,"包装高 · CM",498,76,146,"");
        w.Width=98;
        widthUp=ButtonAt(w.Parent,"▴",114,5,24,false);widthUp.Height=18;widthUp.Font=new Font("Microsoft YaHei UI",9);widthUp.AccessibleName="增加一件";
        widthDown=ButtonAt(w.Parent,"▾",114,24,24,false);widthDown.Height=18;widthDown.Font=new Font("Microsoft YaHei UI",9);widthDown.AccessibleName="减少一件";
        widthTips.SetToolTip(widthUp,"增加一件：宽度和重量按单件原值联动");widthTips.SetToolTip(widthDown,"减少一件：最低为零件");
        pieceCountLabel=RightLabel(right,"1 件",440,76,44);pieceCountLabel.AccessibleName="包装件数";
        widthUp.Click+=delegate{ChangePieceCount(1);};widthDown.Click+=delegate{ChangePieceCount(-1);};
        weightImperial=ImperialReading(right,"≈ — 磅",18,155,138);
        lengthImperial=ImperialReading(right,"≈ — 英寸",178,155,138);
        widthImperial=ImperialReading(right,"≈ — 英寸",338,155,138);
        heightImperial=ImperialReading(right,"≈ — 英寸",498,155,138);
        Panel separator=new Panel{Location=new Point(18,197),Size=new Size(626,1),BackColor=Color.FromArgb(235,240,245)};right.Controls.Add(separator);
        LabelAt(right,"运输设置",18,210,145,22,9,muted,true);
        LabelAt(right,"运输方式",18,238,146,25,10,muted,false);
        Panel modeFrame=new RoundedPanel{Location=new Point(18,267),Size=new Size(146,46),BackColor=Color.White,Radius=9};right.Controls.Add(modeFrame);
        mode=new ComboBox{Location=new Point(8,8),Size=new Size(130,32),DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,Font=new Font("Microsoft YaHei UI",13),AccessibleName="运输方式"};mode.Items.AddRange(new object[]{"海运","空运","快递"});modeFrame.Controls.Add(mode);
        rate=Field(right,"运费单价 · 元/KG",178,238,146,prices[0]);
        divisor=Field(right,"材积重除数",338,238,146,"6000");
        exchangeInput=Field(right,"汇率",498,238,146,exchangeSaved);SetStored(exchangeInput,exchangeSaved);
        exchangeHint=RightLabel(right,"1 美元 = 6.7 元",498,316,138);
        mode.SelectedIndexChanged+=delegate{prices[previousMode]=RawText(rate);if(previousMode==2)expressDivisor=RawText(divisor);updating=true;SetStored(rate,prices[mode.SelectedIndex]);divisor.ReadOnly=mode.SelectedIndex!=2;divisor.BackColor=mode.SelectedIndex==2?Color.White:Color.FromArgb(244,247,250);divisor.Parent.BackColor=divisor.BackColor;divisor.Parent.Invalidate();SetStored(divisor,mode.SelectedIndex==0?"6000":mode.SelectedIndex==1?"5000":expressDivisor);previousMode=mode.SelectedIndex;updating=false;Recalculate();};mode.SelectedIndex=0;
        Panel result=Card(22,472,956,116);
        actualResult=ResultBox(result,"包装重量 · KG",18,170,false);volumeResult=ResultBox(result,"材积重 · KG",204,170,false);billResult=ResultBox(result,"计费重量 · KG",390,170,true);feeResult=ResultBox(result,"头程运费 · 元",576,170,true);usdResult=ResultBox(result,"头程运费 · 美元",762,174,true);
        basis=LabelAt(result,"",20,95,916,19,8,muted,false);
        Panel fbaCard=Card(22,602,956,202);
        LabelAt(fbaCard,"03  FBA 配送费",18,12,230,28,12,ink,true);
        LabelAt(fbaCard,"美国站",280,16,200,23,9,muted,false);
        Button advanced=ButtonAt(fbaCard,"更多",866,10,68,false);advanced.Height=29;advanced.Font=new Font("Microsoft YaHei UI",9);advanced.Click+=delegate{OpenFbaSettings();};
        fbaPrice=Field(fbaCard,"商品售价 · USD",18,52,146,"");
        LabelAt(fbaCard,"淡旺季费率",178,52,146,25,10,muted,false);
        Panel seasonFrame=new RoundedPanel{Location=new Point(178,81),Size=new Size(146,46),BackColor=Color.White,Radius=9};fbaCard.Controls.Add(seasonFrame);
        fbaSeason=new ComboBox{Location=new Point(8,8),Size=new Size(130,32),DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,Font=new Font("Microsoft YaHei UI",11),AccessibleName="FBA 淡旺季费率"};fbaSeason.Items.AddRange(new object[]{"2026 非旺季","2026 旺季"});seasonFrame.Controls.Add(fbaSeason);
        fbaSeasonHint=RightLabel(fbaCard,"",178,128,146);fbaSeasonHint.Font=new Font("Microsoft YaHei UI",8);
        Panel hiddenSettings=new Panel{Visible=false};fbaCard.Controls.Add(hiddenSettings);
        fbaFuel=new CheckBox{Visible=false,Checked=fbaFuelSaved};hiddenSettings.Controls.Add(fbaFuel);
        fbaSpecialFee=Field(hiddenSettings,"特殊处理费 · USD",0,0,146,"");
        LabelAt(fbaCard,"尺寸分档",378,52,180,25,10,muted,false);
        fbaTierLabel=LabelAt(fbaCard,"—",378,87,180,41,12,ink,true);
        Panel fbaValue=new Panel{Location=new Point(578,40),Size=new Size(346,103),BackColor=Color.White};fbaCard.Controls.Add(fbaValue);
        fbaFeeResult=ResultBox(fbaValue,"FBA 运费 · 美元",0,174,true);
        fbaDetail=LabelAt(fbaCard,"",18,156,916,23,9,ink,false);
        fbaNote=LabelAt(fbaCard,"",18,181,916,18,8,muted,false);
        fbaSeason.SelectedIndex=1;
        fbaSeason.SelectedIndexChanged+=delegate{RefreshFba();};fbaFuel.CheckedChanged+=delegate{RefreshFba();};
        Button copy=ButtonAt(this,"复制计算结果",22,818,160,true);copy.Click+=delegate{if(lastFee.HasValue&&lastUsd.HasValue){Clipboard.SetText(Summary());message.Text="计算结果已复制。";}else message.Text="请先完善有效数据。";};
        Button clear=ButtonAt(this,"清空数据",194,818,166,false);clear.Click+=delegate{if(busy||capturing)return;ResetPieceCount(null,null);foreach(TextBox box in new[]{weight,l,w,h,fbaPrice,fbaSpecialFee})box.Clear();raw.Clear();source.Text="识别后请核对包装数据，可在右侧修改。";if(picture.Image!=null){picture.Image.Dispose();picture.Image=null;}hint.Visible=true;Recalculate();};
        message=LabelAt(this,"",25,866,950,24,9,muted,false);
        foreach(TextBox box in new[]{weight,l,w,h,divisor,rate,exchangeInput,fbaPrice,fbaSpecialFee})box.TextChanged+=delegate(object sender,EventArgs e){TextBox boxInput=(TextBox)sender;if(!updating)boxInput.Tag=null;int pos=boxInput.SelectionStart;string normalized=boxInput.Text.Replace('。','.').Replace('．','.');if(normalized!=boxInput.Text){boxInput.Text=normalized;boxInput.SelectionStart=Math.Min(pos,boxInput.Text.Length);}if(!updating){UpdateUnitFromEdit(boxInput);Recalculate();}};
        foreach(TextBox box in new[]{weight,l,w,h,divisor,rate,exchangeInput,fbaPrice,fbaSpecialFee})box.Leave+=delegate(object sender,EventArgs e){TextBox input=(TextBox)sender;decimal number;if(Read(input,out number,true)){SetReading(input,number);Recalculate();}};
        KeyDown+=async delegate(object sender,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.V&&Clipboard.ContainsImage()){e.SuppressKeyPress=true;await PasteImage();}};
        Shown+=delegate{Rectangle area=Screen.FromControl(this).WorkingArea;if(Height>area.Height){Height=area.Height;Top=area.Top;}};
        FormClosed+=delegate{widthTips.Dispose();};
        FormClosing+=delegate{if(!testing){ConfirmRateChange();SaveSettings();}};Recalculate();
        InitializeProfit();
    }
    Panel Card(int x,int y,int width,int height){Panel p=new RoundedPanel{Location=new Point(x,y),Size=new Size(width,height),BackColor=Color.White};Controls.Add(p);return p;}
    Label LabelAt(Control c,string text,int x,int y,int width,int height,float size,Color color,bool bold){Label b=new Label{Text=text,Location=new Point(x,y),Size=new Size(width,height),Font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=color,TextAlign=ContentAlignment.MiddleLeft};c.Controls.Add(b);return b;}
    Button ButtonAt(Control c,string text,int x,int y,int width,bool primary){Button b=new RoundedButton{Text=text,Location=new Point(x,y),Size=new Size(width,35),BackColor=primary?teal:Color.White,ForeColor=primary?Color.White:ink,Cursor=Cursors.Hand,Font=new Font(c.Font,c.Font.Style|FontStyle.Bold)};b.FlatAppearance.BorderColor=primary?teal:Color.FromArgb(205,218,231);c.Controls.Add(b);return b;}
    TextBox Field(Control c,string name,int x,int y,int width,string text){LabelAt(c,name,x,y,width,25,10,muted,false).TextAlign=ContentAlignment.MiddleCenter;Panel frame=new RoundedPanel{Location=new Point(x,y+29),Size=new Size(width,46),BackColor=Color.White,Radius=9};c.Controls.Add(frame);TextBox b=new TextBox{Location=new Point(10,8),Size=new Size(width-20,32),BorderStyle=BorderStyle.None,Font=new Font("Microsoft YaHei UI",15,FontStyle.Bold),TextAlign=HorizontalAlignment.Center,Text=text,AccessibleName=name};frame.Controls.Add(b);return b;}
    TextBox ResultBox(Control c,string name,int x,int width,bool green){LabelAt(c,name,x,13,width,27,10,muted,false).TextAlign=ContentAlignment.MiddleCenter;TextBox t=new TextBox{Location=new Point(x,44),Size=new Size(width,45),BorderStyle=BorderStyle.None,ReadOnly=true,TextAlign=HorizontalAlignment.Center,BackColor=Color.White,ForeColor=green?teal:ink,Font=new Font("Microsoft YaHei UI",25,FontStyle.Bold),Text="—",AccessibleName=name};c.Controls.Add(t);return t;}
    Label RightLabel(Control parent,string text,int x,int y,int width){Label label=LabelAt(parent,text,x,y,width,24,9,muted,false);label.TextAlign=ContentAlignment.MiddleRight;return label;}
    static decimal Up(decimal value){decimal whole=decimal.Truncate(value);return whole+decimal.Ceiling((value-whole)*100m)/100m;}
    static string Reading(decimal value){return Up(value).ToString("0.##",CultureInfo.InvariantCulture);}
    void SetReading(TextBox box,decimal value){bool previous=updating;updating=true;box.Tag=value;box.Text=Reading(value);updating=previous;}
    void SetStored(TextBox box,string text){decimal value;if(decimal.TryParse(text,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out value))SetReading(box,value);else{box.Tag=null;box.Text=text;}}
    string RawText(TextBox box){return box.Tag is decimal?((decimal)box.Tag).ToString(CultureInfo.InvariantCulture):box.Text;}
    string ImperialLength(decimal cm){return "≈ "+Reading(cm/2.54m)+" 英寸";}
    bool Read(TextBox box,out decimal value,bool zero){if(box.Tag is decimal&&box.Text==Reading((decimal)box.Tag))value=(decimal)box.Tag;else if(!decimal.TryParse(box.Text,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out value))return false;return zero?value>=0:value>0;}
    void Recalculate(){
        if(actualResult==null)return;actualResult.ForeColor=volumeResult.ForeColor=ink;lastFee=lastUsd=null;actualResult.Text=volumeResult.Text=billResult.Text=feeResult.Text=usdResult.Text="—";basis.Text="";
        decimal a,length,width,height,d,r,fx;
        bool validWeight=Read(weight,out a,false),validSize=Read(l,out length,false)&Read(w,out width,false)&Read(h,out height,false),validDivisor=Read(divisor,out d,false),validRate=Read(rate,out r,true),validFx=Read(exchangeInput,out fx,false);
        exchangeHint.Text=validFx?"1 美元 = "+Reading(fx)+" 元":"汇率须大于 0";
        weightImperial.Text="≈ — 磅";lengthImperial.Text=widthImperial.Text=heightImperial.Text="≈ — 英寸";
        try {
            if(validWeight){actualResult.Text=Reading(a);weightImperial.Text="≈ "+Reading(a/.45359237m)+" 磅";}
            if(length>0)lengthImperial.Text=ImperialLength(length);if(width>0)widthImperial.Text=ImperialLength(width);if(height>0)heightImperial.Text=ImperialLength(height);
            decimal volume=0;if(validSize&&validDivisor){volume=Model.Volume(length,width,height,d);volumeResult.Text=Reading(volume);}
            if(validWeight&&validSize&&validDivisor){
                decimal bill=Math.Max(a,volume);actualResult.ForeColor=a<volume?Color.FromArgb(168,179,191):ink;volumeResult.ForeColor=volume<a?Color.FromArgb(168,179,191):ink;billResult.Text=Reading(bill);
                if(validRate){lastFee=a>=volume?a*r:(length*width*height*r)/d;feeResult.Text=Reading(lastFee.Value);if(validFx){lastUsd=lastFee.Value/fx;usdResult.Text=Reading(lastUsd.Value);}else basis.Text="";}else basis.Text="请填写有效运费单价（可为 0）。";
            }else basis.Text="";
        }catch(OverflowException){basis.Text="数值过大，请检查输入。";lastFee=lastUsd=null;billResult.Text=feeResult.Text=usdResult.Text="—";}
        RefreshFba();
    }
    void ResetPieceCount(decimal? width,decimal? mass){pieceCount=1;unitWidth=width;unitWeight=mass;if(pieceCountLabel!=null)pieceCountLabel.Text="1 件";}
    void UpdateUnitFromEdit(TextBox box){
        if(box!=w&&box!=weight)return;
        decimal value;bool valid=Read(box,out value,false);
        if(pieceCount==0&&valid){
            pieceCount=1;
            if(box==w){unitWidth=value;if(unitWeight.HasValue)SetReading(weight,unitWeight.Value);}
            else {unitWeight=value;if(unitWidth.HasValue)SetReading(w,unitWidth.Value);}
        }else{
            decimal? perUnit=valid&&pieceCount>0?(decimal?)(value/pieceCount):null;
            if(box==w)unitWidth=perUnit;else unitWeight=perUnit;
        }
        pieceCountLabel.Text=pieceCount.ToString(CultureInfo.InvariantCulture)+" 件";
    }
    void ChangePieceCount(int change){
        if(busy||capturing)return;
        if(!unitWidth.HasValue||unitWidth<=0||!unitWeight.HasValue||unitWeight<=0){message.Text="请先填写有效包装宽度和重量。";return;}
        try{
            int next=change<0?Math.Max(0,pieceCount-1):checked(pieceCount+1);
            decimal nextWidth=unitWidth.Value*next,nextWeight=unitWeight.Value*next;
            SetReading(w,nextWidth);SetReading(weight,nextWeight);pieceCount=next;pieceCountLabel.Text=pieceCount.ToString(CultureInfo.InvariantCulture)+" 件";Recalculate();message.Text="";
        }catch(OverflowException){message.Text="包装件数或数值过大，请检查输入。";}
    }
    void OpenFbaSettings(){
        using(Form dialog=new Form{Text="FBA 更多设置",Font=Font,BackColor=BackColor,ForeColor=ink,ClientSize=new Size(440,285),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false}){
            CheckBox fuel=new CheckBox{Location=new Point(24,20),Size=new Size(392,32),Text="计入燃油附加费 3.5%",Checked=fbaFuel.Checked};dialog.Controls.Add(fuel);
            TextBox extra=Field(dialog,"特殊处理费 · USD",24,70,230,"");SetStored(extra,RawText(fbaSpecialFee));
            bool special=currentFba!=null&&currentFba.SpecialHandling;extra.Enabled=special;
            LabelAt(dialog,"特殊处理费仅用于特殊大号商品，其他分档不计入。",24,154,392,48,9,muted,false);
            Button save=ButtonAt(dialog,"保存",184,225,110,true),cancel=ButtonAt(dialog,"取消",306,225,110,false);cancel.DialogResult=DialogResult.Cancel;
            dialog.AcceptButton=save;dialog.CancelButton=cancel;
            save.Click+=delegate{
                decimal amount;if(special&&!Read(extra,out amount,true)){MessageBox.Show(dialog,"请填写不小于 0 的特殊处理费。","请核对费用",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
                if(special&&Read(extra,out amount,true))SetReading(fbaSpecialFee,amount);
                fbaFuel.Checked=fuel.Checked;Recalculate();dialog.DialogResult=DialogResult.OK;dialog.Close();
            };
            dialog.ShowDialog(this);
        }
    }
    static int DefaultFbaSeason(){DateTime date=DateTime.UtcNow.AddHours(8);return date>=new DateTime(2026,10,15)&&date<new DateTime(2027,1,15)?1:0;}
    void SetFbaFont(float size){if(Math.Abs(fbaFeeResult.Font.Size-size)<.01f)return;Font old=fbaFeeResult.Font;fbaFeeResult.Font=new Font("Microsoft YaHei UI",size,FontStyle.Bold);old.Dispose();}
    void RefreshFba(){try{RefreshFbaCore();}finally{SyncProfit();}}
    void RefreshFbaCore(){
        if(fbaFeeResult==null)return;currentFba=null;fbaFeeResult.Text="—";fbaTierLabel.Text="—";SetFbaFont(25);
        fbaSeasonHint.Text=fbaSeason.SelectedIndex==1?"2026.10.15–2027.01.14":"2026.01.15–10.14";
        fbaNote.Text="";fbaNote.ForeColor=muted;
        fbaSpecialFee.ReadOnly=true;fbaSpecialFee.BackColor=Color.FromArgb(244,247,250);fbaSpecialFee.Parent.BackColor=fbaSpecialFee.BackColor;fbaSpecialFee.Parent.Invalidate();
        decimal kg,length,width,height,price,extra;
        if(!(Read(weight,out kg,false)&Read(l,out length,false)&Read(w,out width,false)&Read(h,out height,false))){fbaDetail.Text="";return;}
        if(!Read(fbaPrice,out price,false)){fbaDetail.Text="请填写商品售价（USD），以确定售价档位。";return;}
        try{
            bool hasExtra=Read(fbaSpecialFee,out extra,true);currentFba=FbaModel.Calculate(kg,length,width,height,price,fbaSeason.SelectedIndex==1,fbaFuel.Checked,hasExtra?(decimal?)extra:null);
            fbaSpecialFee.ReadOnly=!currentFba.SpecialHandling;fbaSpecialFee.BackColor=currentFba.SpecialHandling?Color.White:Color.FromArgb(244,247,250);fbaSpecialFee.Parent.BackColor=fbaSpecialFee.BackColor;fbaSpecialFee.Parent.Invalidate();
            fbaTierLabel.Text=currentFba.Name;
            if(currentFba.Total.HasValue)fbaFeeResult.Text=Reading(currentFba.Total.Value);
            else {SetFbaFont(17);fbaFeeResult.Text="待补处理费";}
            string total=currentFba.Total.HasValue?"$"+Reading(currentFba.Total.Value):"已知 $"+Reading(currentFba.KnownFee);
            fbaDetail.Text="FBA运费 "+total+" = 基础费用 $"+Reading(currentFba.BaseFee)+" + 税费燃油费 $"+Reading(currentFba.FuelFee)+(currentFba.SpecialHandling&&hasExtra?" + 特殊处理费 $"+Reading(extra):"")+"（美元）";
            if(currentFba.SpecialHandling){fbaNote.Text=currentFba.Total.HasValue?"已计入手填特殊处理费；请以亚马逊实测包装及账单为准。":"超过 96 英寸或长加围长超过 130 英寸，需补填特殊处理费；已知部分 $"+Reading(currentFba.KnownFee)+"。";fbaNote.ForeColor=Color.FromArgb(166,95,32);}
        }catch(OverflowException){currentFba=null;fbaFeeResult.Text="—";fbaDetail.Text="包装数值过大，请检查输入。";}
    }
    async Task CaptureScreen(Func<Bitmap> grab=null,Func<Bitmap,Rectangle,Bitmap> select=null){
        if(busy||capturing)return;capturing=true;pasteButton.Enabled=fileButton.Enabled=captureButton.Enabled=false;
        Bitmap selected=null;Rectangle bounds=SystemInformation.VirtualScreen;
        try{
            Hide();await Task.Delay(180);
            using(Bitmap screenshot=grab==null?GrabDesktop(bounds):grab()){
                if(select!=null)selected=select(screenshot,bounds);
                else using(var overlay=new CaptureOverlay(screenshot,bounds)){if(overlay.ShowDialog()==DialogResult.OK)selected=overlay.SelectedImage;}
            }
            if(selected==null)message.Text="已取消截图，原有包装数据已保留。";
        }catch(Exception e){message.Text="截图失败："+e.Message;}
        finally{capturing=false;if(!IsDisposed){Show();WindowState=FormWindowState.Normal;Activate();pasteButton.Enabled=fileButton.Enabled=captureButton.Enabled=true;}}
        if(selected!=null){using(selected){if(!IsDisposed)await Recognize(selected);}}
    }
    static Bitmap GrabDesktop(Rectangle bounds){Bitmap screenshot=new Bitmap(bounds.Width,bounds.Height,System.Drawing.Imaging.PixelFormat.Format24bppRgb);try{using(Graphics graphics=Graphics.FromImage(screenshot))graphics.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size,CopyPixelOperation.SourceCopy);return screenshot;}catch{screenshot.Dispose();throw;}}
    async Task PasteImage(){if(busy||capturing)return;try{if(!Clipboard.ContainsImage()){message.Text="剪贴板中没有图片，请先复制截图。";return;}using(Image i=Clipboard.GetImage())await Recognize(i);}catch(Exception e){message.Text="无法粘贴图片："+e.Message;}}
    async Task OpenImage(string path){if(busy||capturing)return;try{using(Image i=Image.FromFile(path))await Recognize(i);}catch(Exception e){message.Text="无法打开图片："+e.Message;}}
    async Task Recognize(Image image){
        if(busy)return;busy=true;pasteButton.Enabled=fileButton.Enabled=captureButton.Enabled=false;
        if(picture.Image!=null)picture.Image.Dispose();picture.Image=new Bitmap(image);foreach(Control c in paste.Controls)if(c is Label)c.Visible=false;
        foreach(TextBox t in new[]{weight,l,w,h,fbaPrice,fbaSpecialFee})t.Clear();raw.Clear();source.Text="正在使用 RapidOCR 离线识别…";message.Text="正在识别，请稍候…";
        try{
            string text;using(Bitmap clone=new Bitmap(image))text=await Task.Run(()=>Ocr.Read(clone));raw.Text=text;Package p=Model.Parse(text);
            updating=true;TextBox[] boxes={weight,l,w,h,fbaPrice};decimal?[] values={p.Weight,p.Length,p.Width,p.Height,p.Price};for(int i=0;i<boxes.Length;i++){if(values[i].HasValue)SetReading(boxes[i],values[i].Value);else{boxes[i].Tag=null;boxes[i].Text="";}}ResetPieceCount(p.Width,p.Weight);updating=false;
            source.Text=p.Weight.HasValue&&p.Length.HasValue?(p.Price.HasValue?"已识别价格和包装数据，请核对。":"已识别包装数据（KG / CM），请核对。"):"部分数据未识别，请核对原图并在右侧补填。";
            message.Text=p.Weight.HasValue&&p.Length.HasValue?"":"未识别的字段已留空，不会沿用上张图片的数据。";Recalculate();
        }catch(Exception e){source.Text="识别失败，可手动填写右侧数据。";message.Text="识别失败："+e.Message.Split('\r','\n')[0];raw.Text=e.Message;}
        finally{updating=false;busy=false;pasteButton.Enabled=fileButton.Enabled=captureButton.Enabled=true;}
    }
    string FbaSummary(){return "\r\nFBA 配送费（美国站）：$ "+fbaFeeResult.Text+"\r\n商品售价：$ "+fbaPrice.Text+"；"+fbaSeason.Text+"\r\n"+fbaTierLabel.Text+" · "+fbaDetail.Text+"\r\n"+fbaNote.Text;}
    string Summary(){return "亚马逊运费计算\r\n包装件数："+pieceCount.ToString(CultureInfo.InvariantCulture)+" 件\r\n运输方式："+mode.Text+"\r\n包装尺寸："+l.Text+" × "+w.Text+" × "+h.Text+" CM\r\n包装重量："+weight.Text+" KG\r\n材积重除数："+divisor.Text+"\r\n材积重："+volumeResult.Text+" KG\r\n计费重量："+billResult.Text+" KG\r\n运费单价："+rate.Text+" 元/KG\r\n头程运费：¥ "+feeResult.Text+"\r\n头程运费：$ "+usdResult.Text+"\r\n汇率：1 美元 = "+exchangeInput.Text+" 元"+FbaSummary()+"\r\n显示最多两位小数，有尾数进位；计算保留原始精度。";}
    void SaveShare(string path){
        using(Bitmap b=new Bitmap(1120,850))using(Graphics g=Graphics.FromImage(b)){
            g.Clear(BackColor);g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using(Font title=new Font("Microsoft YaHei UI",25,FontStyle.Bold))using(Font body=new Font("Microsoft YaHei UI",16))using(Font big=new Font("Microsoft YaHei UI",32,FontStyle.Bold))using(Brush dark=new SolidBrush(ink))using(Brush green=new SolidBrush(teal)){
                g.DrawString("亚马逊运费计算",title,dark,35,25);
                using(var card=Shapes.Round(new RectangleF(30,95,1060,285),16))g.FillPath(Brushes.White,card);
                g.DrawString(mode.Text+"  |  单价 "+rate.Text+" 元/KG  |  材积重除数 "+divisor.Text+"  |  "+pieceCount.ToString(CultureInfo.InvariantCulture)+" 件",body,dark,52,116);
                g.DrawString("包装尺寸    "+l.Text+" × "+w.Text+" × "+h.Text+" CM",body,dark,52,170);
                using(var actualInk=new SolidBrush(actualResult.ForeColor))g.DrawString("包装重量    "+actualResult.Text+" KG",body,actualInk,52,220);
                using(var volumeInk=new SolidBrush(volumeResult.ForeColor))g.DrawString("材积重        "+volumeResult.Text+" KG",body,volumeInk,52,270);
                g.DrawString("计费重量    "+billResult.Text+" KG",body,green,52,320);
                using(var card=Shapes.Round(new RectangleF(30,400,1060,140),16))g.FillPath(Brushes.White,card);
                g.DrawString("头程运费 · 人民币",body,dark,52,416);g.DrawString("¥ "+feeResult.Text,big,green,52,456);
                g.DrawString("头程运费 · 美元",body,dark,420,416);g.DrawString("$ "+usdResult.Text,big,green,420,456);
                g.DrawString("汇率",body,dark,780,416);g.DrawString("1 美元 = "+exchangeInput.Text+" 元",body,dark,780,470);
                using(var card=Shapes.Round(new RectangleF(30,560,1060,180),16))g.FillPath(Brushes.White,card);
                g.DrawString("FBA 配送费 · 美元",body,dark,52,576);g.DrawString("$ "+fbaFeeResult.Text,big,green,52,616);
                g.DrawString(fbaSeason.Text+"  |  售价 $"+fbaPrice.Text,body,dark,420,576);
                using(var detailFont=new Font("Microsoft YaHei UI",12)){g.DrawString(fbaTierLabel.Text+" · "+fbaDetail.Text,detailFont,dark,new RectangleF(52,684,1010,38));g.DrawString(fbaNote.Text,detailFont,dark,new RectangleF(420,620,640,56));}
                g.DrawString("读数最多两位小数，有尾数进位；计算保留原始精度。",body,dark,50,782);
                b.Save(path,System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
    void LoadSettings(){try{string[] s=File.ReadAllLines(settings);if(s.Length>=4){for(int i=0;i<3;i++)prices[i]=s[i];expressDivisor=s[3];if(s.Length>=5){decimal fx;if(decimal.TryParse(s[4],NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out fx)&&fx>0)exchangeSaved=s[4];}if(s.Length>=6){int season;if(int.TryParse(s[5],out season)&&season>=0&&season<=1)fbaSeasonSaved=season;}if(s.Length>=7){bool fuel;if(bool.TryParse(s[6],out fuel))fbaFuelSaved=fuel;}}}catch(IOException){}catch(UnauthorizedAccessException){}}
    void SaveSettings(){prices[mode.SelectedIndex]=RawText(rate);if(mode.SelectedIndex==2)expressDivisor=RawText(divisor);exchangeSaved=RawText(exchangeInput);try{Directory.CreateDirectory(Path.GetDirectoryName(settings));string temporary=settings+".tmp";File.WriteAllLines(temporary,new[]{testingSession||persistedPrices==null?prices[0]:persistedPrices[0],testingSession||persistedPrices==null?prices[1]:persistedPrices[1],testingSession||persistedPrices==null?prices[2]:persistedPrices[2],expressDivisor,exchangeSaved,fbaSeason.SelectedIndex.ToString(CultureInfo.InvariantCulture),fbaFuel.Checked.ToString()});File.Copy(temporary,settings,true);File.Delete(temporary);}catch(IOException){MessageBox.Show("运费设置无法保存，请检查当前用户的本地应用数据目录是否可写。","设置未保存");}catch(UnauthorizedAccessException){MessageBox.Show("运费设置无法保存，请检查当前用户的本地应用数据目录是否可写。","设置未保存");}}
    public async Task VerifyHorizontal(string folder,string imagePath){
        Show();Application.DoEvents();
        if(fbaDetail.Text!="")throw new Exception("Initial FBA helper text removed");
        using(Image image=Image.FromFile(imagePath)){Clipboard.SetImage(image);await PasteImage();}
        decimal mass,length,width,height;
        if(fbaPrice.Text!="17.99"||!Read(weight,out mass,false)||mass!=.76203m||!Read(l,out length,false)||length!=25.908m||!Read(w,out width,false)||width!=25.654m||!Read(h,out height,false)||height!=12.446m)throw new Exception("Horizontal reference fields: "+raw.Text);
        if(weight.Text!="0.77"||l.Text!="25.91"||w.Text!="25.66"||h.Text!="12.45"||message.Text!="")throw new Exception("Horizontal rounded readouts and no success warning");
        fbaSeason.SelectedIndex=1;fbaFuel.Checked=true;
        if(fbaFeeResult.Text!="8.03"||fbaDetail.Text!="FBA运费 $8.03 = 基础费用 $7.75 + 税费燃油费 $0.28（美元）")throw new Exception("Standard FBA numeric equation: "+fbaDetail.Text);
        fbaFuel.Checked=false;if(fbaDetail.Text!="FBA运费 $7.75 = 基础费用 $7.75 + 税费燃油费 $0（美元）")throw new Exception("No-fuel equation");fbaFuel.Checked=true;
        using(Bitmap bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(Path.Combine(folder,"horizontal-preview.png"));}SaveShare(Path.Combine(folder,"horizontal-share.png"));
        string english="Color: White Price: $17.99 Item Weight: 1 pounds (453.59 g) Product Dimensions: 9.1 x 4.6 x 9.7 inches Package Weight: 1.68 pounds (762.03 g) Package Dimensions: 10.2 x 10.1 x 4.9 inches";
        Package parsed=Model.Parse(english);if(parsed.Price!=17.99m||parsed.Weight!=.76203m||parsed.Length!=25.908m||parsed.Width!=25.654m||parsed.Height!=12.446m)throw new Exception("English horizontal fields isolated by label");
        Package reordered=Model.Parse("商品尺寸:9.1x4.6x9.7 inches 包装重量:1.68 pounds(762.03g) 包装尺寸:10.2x10.1x4.9 inches 上架时间:2022-03-22 价格:$17.99");
        if(reordered.Weight!=.76203m||reordered.Length!=25.908m||reordered.Price!=17.99m)throw new Exception("Same-line product dimensions cannot override package data");
        Package wrapped=Model.Parse("包装重量：\n1.68 pounds (762.03g)\n包装尺寸：\n10.2 x 10.1 x 4.9 inches");if(wrapped.Weight!=.76203m||wrapped.Length!=25.908m)throw new Exception("Wrapped label and value");
        widthUp.PerformClick();if(pieceCount!=2||!Read(weight,out mass,false)||mass!=1.52406m||!Read(w,out width,false)||width!=51.308m)throw new Exception("Horizontal OCR supports linked count");widthDown.PerformClick();
        File.WriteAllText(Path.Combine(folder,"horizontal-verification.txt"),"PASS: real horizontal reference clipboard OCR; same-line product dimensions and package weight isolated; price $17.99, package 0.76203 KG and 25.908 x 25.654 x 12.446 CM; FBA $8.03 = $7.75 + $0.28 peak; no-fuel equation; price can appear after other metadata; English horizontal and wrapped labels; removed initial/helper success texts; linked piece count preserved; UI/share rendered.");Close();
    }
    void VerifyAdvancedSettings(bool save){
        bool previous=fbaFuel.Checked,handled=false;
        using(var timer=new Timer{Interval=60}){
            timer.Tick+=delegate{
                Form dialog=null;foreach(Form open in Application.OpenForms)if(open.Text=="FBA 更多设置"){dialog=open;break;}
                if(dialog==null)return;timer.Stop();CheckBox option=null;Button action=null;
                foreach(Control control in dialog.Controls){if(control is CheckBox)option=(CheckBox)control;if(control is Button&&control.Text==(save?"保存":"取消"))action=(Button)control;}
                if(option==null||action==null){dialog.Close();return;}option.Checked=!previous;handled=true;action.PerformClick();
            };
            timer.Start();OpenFbaSettings();timer.Stop();
        }
        if(!handled||fbaFuel.Checked!=(save?!previous:previous))throw new Exception("Advanced FBA settings save/cancel");
    }
    public async Task VerifyPriceWidth(string folder,string imagePath){
        Show();Application.DoEvents();
        using(Image image=Image.FromFile(imagePath)){Clipboard.SetImage(image);await PasteImage();}
        fbaSeason.SelectedIndex=1;fbaFuel.Checked=true;
        decimal rawWidth,rawWeight;
        if(fbaPrice.Text!="29.77"||l.Text!="13.72"||w.Text!="11.94"||h.Text!="6.1"||weight.Text!="0.14")throw new Exception("Price screenshot extraction: "+raw.Text);
        if(!Read(w,out rawWidth,false)||rawWidth!=11.938m||!Read(weight,out rawWeight,false)||rawWeight!=.13154m)throw new Exception("Screenshot original precision / metric weight preferred");
        if(fbaFeeResult.Text!="4.36"||!lastFee.HasValue||fbaFuel.Visible||fbaSpecialFee.Visible||Text.Contains("头程运费计算器"))throw new Exception("FBA price integration / hidden settings / renamed window");
        VerifyAdvancedSettings(true);if(fbaFeeResult.Text!="4.21")throw new Exception("Hidden fuel setting still adjustable");VerifyAdvancedSettings(false);if(fbaFuel.Checked)throw new Exception("Cancel does not apply fuel change");fbaFuel.Checked=true;
        decimal initialFee=lastFee.Value;
        using(Bitmap bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(Path.Combine(folder,"price-preview.png"));}SaveShare(Path.Combine(folder,"price-share.png"));
        widthUp.PerformClick();if(pieceCount!=2||w.Text!="23.88"||!Read(w,out rawWidth,false)||rawWidth!=23.876m||!Read(weight,out rawWeight,false)||rawWeight!=.26308m||lastFee!=initialFee*2||fbaFeeResult.Text!="5.07")throw new Exception("Two pieces scale original width and weight together");
        using(Bitmap bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(Path.Combine(folder,"width-doubled-preview.png"));}
        widthDown.PerformClick();if(w.Text!="11.94"||lastFee!=initialFee||fbaFeeResult.Text!="4.36")throw new Exception("Width down halves value exactly");
        widthUp.PerformClick();widthUp.PerformClick();if(pieceCount!=3||!Read(w,out rawWidth,false)||rawWidth!=35.814m||!Read(weight,out rawWeight,false)||rawWeight!=.39462m||lastFee!=initialFee*3)throw new Exception("Three pieces use original values times three, not four");
        widthDown.PerformClick();widthDown.PerformClick();if(pieceCount!=1||!Read(w,out rawWidth,false)||rawWidth!=11.938m||!Read(weight,out rawWeight,false)||rawWeight!=.13154m)throw new Exception("Repeated count changes preserve original values");
        widthDown.PerformClick();if(pieceCount!=0||w.Text!="0"||weight.Text!="0"||lastFee.HasValue||fbaFeeResult.Text!="—")throw new Exception("Single piece decrements to zero width and weight");
        widthDown.PerformClick();if(pieceCount!=0||w.Text!="0"||weight.Text!="0")throw new Exception("Piece count never negative");
        widthUp.PerformClick();if(pieceCount!=1||!Read(w,out rawWidth,false)||rawWidth!=11.938m||!Read(weight,out rawWeight,false)||rawWeight!=.13154m||fbaFeeResult.Text!="4.36")throw new Exception("Zero to one restores original package");
        widthUp.PerformClick();widthUp.PerformClick();w.Text="36";widthUp.PerformClick();if(pieceCount!=4||w.Text!="48"||!Read(weight,out rawWeight,false)||rawWeight!=.52616m)throw new Exception("Manual total width edit updates per-piece base");
        weight.Text="0.8";widthDown.PerformClick();if(pieceCount!=3||w.Text!="36"||weight.Text!="0.6")throw new Exception("Manual weight edit updates per-piece base");
        ResetPieceCount(11.938m,.13154m);SetReading(w,11.938m);SetReading(weight,.13154m);Recalculate();
        w.Clear();widthUp.PerformClick();if(w.Text!=""||lastFee.HasValue)throw new Exception("Empty width stays invalid");SetReading(w,11.938m);Recalculate();
        if(Model.Parse("价格: $1,299.99").Price!=1299.99m||Model.Parse("Price: US$29.77").Price!=29.77m)throw new Exception("USD price formats");
        if(Model.Parse("评分:4.6(56,386)\n上架时间:2017-02-09\n商品尺寸:4.6x2.3x5.3 inches").Price.HasValue||Model.Parse("价格:¥29.77").Price.HasValue)throw new Exception("Ignore ratings, dates and non-USD currency");
        using(Bitmap full=new Bitmap(imagePath))using(Bitmap noPrice=full.Clone(new Rectangle(0,20,full.Width,full.Height-20),System.Drawing.Imaging.PixelFormat.Format24bppRgb)){await Recognize(noPrice);}
        if(fbaPrice.Text!=""||fbaFeeResult.Text!="—"||pieceCount!=1||unitWidth!=11.938m||unitWeight!=.13154m)throw new Exception("Next screenshot resets product price and piece bases");
        File.WriteAllText(Path.Combine(folder,"price-width-verification.txt"),"PASS: real reference screenshot price/package OCR; linked piece count 0,1,2,3,4 uses original width and weight times count; zero is minimum; zero-to-one restores originals; repeated steps preserve precision; manual edits update unit bases; screenshot resets count/bases/price; head freight and FBA update; invalid input; advanced settings save/cancel; compact season layout and previews/share rendered.");Close();
    }
    public async Task Verify(string folder,string imagePath){
        Show();Application.DoEvents();
        FbaModel.Verify(Path.Combine(folder,"fba-model-verification.txt"));
        if(mode.SelectedIndex!=0||divisor.Text!="6000"||rate.Text!="12")throw new Exception("Sea defaults");
        using(Image i=Image.FromFile(imagePath)){Clipboard.SetImage(i);await PasteImage();}
        if(raw.Text.Length==0||weight.Text!="2.27"||l.Text!="20.58"||w.Text!="20.32"||h.Text!="19.05")throw new Exception("Screenshot OCR: "+raw.Text+" / "+weight.Text+" / "+l.Text);
        if(feeResult.Text!="27.24"||billResult.Text!="2.27"||usdResult.Text!="4.07"||volumeResult.Text!="1.33"||exchangeInput.Text!="6.7")throw new Exception("Sea / USD / rounded weight calculation");
        if(Reading(1.23001m)!="1.24"||Reading(1.23m)!="1.23"||Reading(1.001m)!="1.01"||Reading(1.999m)!="2"||Reading(0)!="0")throw new Exception("Ceiling to two decimals");
        decimal preciseLength;if(!Read(l,out preciseLength,false)||preciseLength!=20.574m)throw new Exception("Raw OCR precision preserved");Recalculate();if(feeResult.Text!="27.24")throw new Exception("No intermediate rounding drift");
        exchangeInput.Text="7";if(usdResult.Text!="3.9")throw new Exception("Live exchange change");exchangeInput.Text="0";if(lastUsd.HasValue||usdResult.Text!="—"||feeResult.Text!="27.24")throw new Exception("Zero exchange validation");exchangeInput.Text="6.7";
        if(volumeResult.ForeColor==ink||actualResult.ForeColor!=ink||!weightImperial.Text.Contains("5.01 磅")||lengthImperial.Text.Contains("英尺")||!lengthImperial.Text.Contains("8.1 英寸"))throw new Exception("Weight emphasis / imperial conversions");
        using(Bitmap sample=new Bitmap(imagePath)){
            using(Bitmap crop=CaptureOverlay.VerifyCrop(sample,true)){if(crop.Size!=sample.Size||crop.GetPixel(20,20)!=sample.GetPixel(20,20))throw new Exception("Reverse capture crop");}
            CaptureOverlay.VerifyCancel(sample);
            if(CaptureOverlay.Selection(new Point(100,100),new Point(-20,-30),new Size(80,60))!=new Rectangle(0,0,80,60))throw new Exception("Clamped capture selection");
            await CaptureScreen(delegate{if(Visible)throw new Exception("Calculator must hide before capture");return new Bitmap(sample);},delegate(Bitmap shot,Rectangle screenBounds){return CaptureOverlay.VerifyCrop(shot,false);});
            if(!Visible||capturing||busy||feeResult.Text!="27.24")throw new Exception("Capture and recognize / restore");
            await CaptureScreen(delegate{return new Bitmap(sample);},delegate{return null;});
            if(!Visible||weight.Text!="2.27"||capturing)throw new Exception("Capture cancel keeps data / restores calculator");
            await CaptureScreen(delegate{throw new Exception("Simulated capture error");});
            if(!Visible||capturing||weight.Text!="2.27")throw new Exception("Capture error restoration");
        }
        using(Form fixture=new Form{FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Location=new Point(Screen.PrimaryScreen.WorkingArea.Left+40,Screen.PrimaryScreen.WorkingArea.Top+40),ClientSize=picture.Image.Size,TopMost=true,ShowInTaskbar=false})
        using(Bitmap fixtureImage=new Bitmap(imagePath)){
            fixture.BackgroundImage=fixtureImage;fixture.BackgroundImageLayout=ImageLayout.None;fixture.Show();Application.DoEvents();
            await CaptureScreen(null,delegate(Bitmap screenshot,Rectangle desktopBounds){Rectangle crop=new Rectangle(fixture.Left-desktopBounds.Left,fixture.Top-desktopBounds.Top,fixture.ClientSize.Width,fixture.ClientSize.Height);return screenshot.Clone(crop,System.Drawing.Imaging.PixelFormat.Format24bppRgb);});
            if(!Visible||feeResult.Text!="27.24"||weight.Text!="2.27")throw new Exception("Actual CopyFromScreen fixture OCR");fixture.Close();
        }
        if(fbaPrice.Text!=""||fbaFeeResult.Text!="—")throw new Exception("New screenshot clears product-specific FBA price");
        fbaPrice.Text="20";fbaSeason.SelectedIndex=1;fbaFuel.Checked=true;
        if(fbaFeeResult.Text!="8.52"||currentFba.Tier!=FbaTier.LargeStandard)throw new Exception("FBA UI peak sample");
        fbaSeason.SelectedIndex=0;if(fbaFeeResult.Text!="7.96")throw new Exception("FBA season switch");
        fbaSeason.SelectedIndex=1;fbaFuel.Checked=false;if(fbaFeeResult.Text!="8.23")throw new Exception("FBA fuel switch");fbaFuel.Checked=true;
        divisor.Text="9999";exchangeInput.Text="7";if(fbaFeeResult.Text!="8.52")throw new Exception("FBA independent of first-leg divisor and FX");divisor.Text="6000";exchangeInput.Text="6.7";
        SetReading(weight,5m*FbaModel.KgPerLb);SetReading(l,97m*2.54m);SetReading(w,3m*2.54m);SetReading(h,2m*2.54m);Recalculate();
        if(fbaSpecialFee.ReadOnly||fbaFeeResult.Text!="待补处理费"||currentFba.Total.HasValue)throw new Exception("Special handling UI incomplete state");
        fbaSpecialFee.Text="10";if(fbaFeeResult.Text!="41.66")throw new Exception("Special handling manual amount");
        SetReading(weight,2.27m);SetReading(l,20.574m);SetReading(w,20.32m);SetReading(h,19.05m);Recalculate();fbaSpecialFee.Clear();
        if(fbaFeeResult.Text!="8.52"||!fbaSpecialFee.ReadOnly)throw new Exception("Normal tier ignores special handling amount");
        message.Text="已识别包装重量与尺寸，请核对后使用。";
        using(Bitmap b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(0,0,Width,Height));b.Save(Path.Combine(folder,"preview.png"));}SaveShare(Path.Combine(folder,"share.png"));
        mode.SelectedIndex=1;if(rate.Text!="55"||divisor.Text!="5000"||feeResult.Text!="124.85")throw new Exception("Air defaults");
        l.Text="100";w.Text="100";h.Text="100";if(feeResult.Text!="11000"||billResult.Text!="200")throw new Exception("Volumetric branch");
        if(actualResult.ForeColor==ink||volumeResult.ForeColor!=ink)throw new Exception("Actual weight dims when smaller");weight.Text="200";if(actualResult.ForeColor!=ink||volumeResult.ForeColor!=ink)throw new Exception("Equal weights remain emphasized");weight.Text="2.27";
        mode.SelectedIndex=2;if(rate.Text!=""||divisor.Text!=""||lastFee.HasValue)throw new Exception("Express must require inputs");
        rate.Text="30";divisor.Text="6000";if(feeResult.Text!="5000")throw new Exception("Express custom");
        rate.Text="31";mode.SelectedIndex=0;rate.Text="13";mode.SelectedIndex=2;if(rate.Text!="31"||divisor.Text!="6000")throw new Exception("Per-mode values");
        weight.Text="-1";if(lastFee.HasValue)throw new Exception("Negative validation");weight.Text="1。25";if(weight.Text!="1.25")throw new Exception("Chinese decimal");divisor.Text="0";if(lastFee.HasValue)throw new Exception("Zero divisor");
        Package productOnly=Model.Parse("商品重量: 1.3 pounds (589.67 g)\n商品尺寸: 3.7 x 3.7 x 7 inches");if(productOnly.Weight.HasValue||productOnly.Length.HasValue)throw new Exception("Product fields must be ignored");
        Package metric=Model.Parse("包装重量:500 g\n包装尺寸:200×100×50 mm");if(metric.Weight!=.5m||metric.Length!=20m||metric.Width!=10m||metric.Height!=5m)throw new Exception("Metric conversions");
        Package imperial=Model.Parse("Package Weight:5 pounds\nPackage Dimensions:8.1 x 8 x 7.5 inches");if(imperial.Weight!=2.26796185m||imperial.Length!=20.574m)throw new Exception("Imperial conversions");
        string originalSettings=settings;settings=Path.Combine(folder,"portable-settings-test.txt");exchangeInput.Text="7.2";fbaSeason.SelectedIndex=0;fbaFuel.Checked=false;SaveSettings();
        using(MainForm next=new MainForm(true)){next.settings=settings;next.LoadSettings();if(next.prices[0]!="13"||next.prices[2]!="31"||next.expressDivisor!="0"||next.exchangeSaved!="7.2"||next.fbaSeasonSaved!=0||next.fbaFuelSaved)throw new Exception("Portable settings persistence");}settings=originalSettings;
        File.WriteAllText(Path.Combine(folder,"verification.txt"),"PASS: RapidOCR clipboard and actual CopyFromScreen fixture OCR; calculator hides before capture and restores on success/cancel/failure; reverse/clamped crop selection and Esc cancellation; rounded compact UI rendered; KG/CM labels; Chinese pounds/inches; ceiling readings to two decimals with original precision preserved; CNY/USD conversion, editable exchange and invalid exchange handling; smaller weight dims dynamically, equal weights retain emphasis; sea/air/express and exact billing; invalid input; settings save/reload; share rendering. Manual multi-monitor interaction not tested.");
        Close();
    }
}
internal static class Program {
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetProcessDPIAware();
    [STAThread]static int Main(string[] args){SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length==3&&args[0]=="--ocr"){try{var timer=Stopwatch.StartNew();using(Image image=Image.FromFile(args[1]))File.WriteAllText(args[2],Ocr.Read(image),Encoding.UTF8);File.WriteAllText(args[2]+".time",timer.Elapsed.TotalSeconds.ToString(CultureInfo.InvariantCulture));return 0;}catch(Exception e){File.WriteAllText(args[2],e.ToString());return 1;}}
        if(args.Length==2&&args[0]=="--guide-capture"){Directory.CreateDirectory(args[1]);using(MainForm f=new MainForm(true))f.CaptureGuide(args[1]);return 0;}
        if(args.Length==3&&args[0]=="--verify-lifecycle"){
            Directory.CreateDirectory(args[1]);int code=0;
            using(MainForm f=new MainForm(true)){
                f.Shown+=delegate{f.BeginInvoke(new Action(delegate{try{f.PrepareLifecycleCheck(args[2],args[1]);}catch(Exception e){File.WriteAllText(Path.Combine(args[1],"lifecycle-"+args[2]+"-error.txt"),e.ToString());code=1;f.Close();}}));};
                Application.Run(f);
            }
            if(code==0)File.WriteAllText(Path.Combine(args[1],"lifecycle-"+args[2]+"-pass.txt"),"PASS: native close exits the main message loop, with dropdown "+args[2]+"; popup resources disposed.");return code;
        }
        if(args.Length==2&&args[0]=="--verify-integration"){Directory.CreateDirectory(args[1]);try{using(MainForm f=new MainForm(true))f.VerifyIntegration(args[1]);return 0;}catch(Exception e){File.WriteAllText(Path.Combine(args[1],"integration-error.txt"),e.ToString());return 1;}}
        if(args.Length==3&&(args[0]=="--verify"||args[0]=="--verify-price"||args[0]=="--verify-horizontal")){
            MainForm f=new MainForm(true);int code=0;f.Shown+=async delegate{try{if(args[0]=="--verify-horizontal")await f.VerifyHorizontal(args[1],args[2]);else if(args[0]=="--verify-price")await f.VerifyPriceWidth(args[1],args[2]);else await f.Verify(args[1],args[2]);}catch(Exception e){File.WriteAllText(Path.Combine(args[1],args[0]=="--verify-horizontal"?"horizontal-verification.txt":args[0]=="--verify-price"?"price-width-verification.txt":"verification.txt"),e.ToString());code=1;f.Close();}};Application.Run(f);return code;
        }Application.Run(new MainForm());return 0;
    }
}
}
