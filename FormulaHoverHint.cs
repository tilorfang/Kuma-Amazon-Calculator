using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AmazonProfitCalculatorV2 {
internal sealed class FormulaHoverHint:IDisposable {
    readonly ToolTip tip;
    readonly Control anchor;
    const int Width=540,Padding=16;
    static readonly string[] Headings={"整套售价", "头程运费（美元）", "FBA 运费（美元）"};
    static readonly string[] Bodies={
        "1 件：单件售价\r\n2 件：单件售价 × 2 × 90%\r\n3 件：单件售价 × 3 × 80%\r\n4 件起：默认留空，手动填写整套售价。",
        "计费重量 = 整套包装重量与整套材积重中的较大值\r\n头程运费 = 计费重量（KG）× 运费单价（元/KG）÷ 汇率（元/美元）\r\n整套包装按单件宽度、重量 × 件数计算，长、高不变。",
        "FBA 配送费 = 基础配送费 + 燃油附加费 + 特殊处理费\r\n按整套包装、整套售价与淡旺季重新计算；特殊处理费仅在适用时计入。"
    };
    internal FormulaHoverHint(Control anchor){
        this.anchor=anchor;
        string text="";for(int i=0;i<Headings.Length;i++)text+=(i>0?"\r\n\r\n":"")+Headings[i]+"\r\n"+Bodies[i];
        anchor.AccessibleDescription=text;
        tip=new ToolTip{OwnerDraw=true,IsBalloon=false,InitialDelay=350,ReshowDelay=100,AutoPopDelay=30000,ShowAlways=true,UseAnimation=true,UseFading=true};
        tip.SetToolTip(anchor,text);
        tip.Popup+=delegate(object sender,PopupEventArgs e){e.ToolTipSize=Measure();};
        tip.Draw+=delegate(object sender,DrawToolTipEventArgs e){Draw(e.Graphics,e.Bounds.Size);};
        anchor.Click+=delegate{tip.Show(text,anchor,0,-Measure().Height-8,30000);};
        anchor.Leave+=delegate{tip.Hide(anchor);};
    }
    static Size TextSize(string text,Font font){return TextRenderer.MeasureText(text,font,new Size(Width-Padding*2,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPadding);}
    static Size Measure(){
        using(Font body=new Font("Microsoft YaHei UI",10F))using(Font heading=new Font("Microsoft YaHei UI",10F,FontStyle.Bold)){
            int height=Padding*2;
            for(int i=0;i<Headings.Length;i++)height+=TextSize(Headings[i],heading).Height+5+TextSize(Bodies[i],body).Height+(i<Headings.Length-1?14:0);
            return new Size(Width,height);
        }
    }
    static void Draw(Graphics graphics,Size size){
        graphics.Clear(Color.FromArgb(253,254,255));
        using(Pen border=new Pen(Color.FromArgb(207,219,232)))graphics.DrawRectangle(border,0,0,size.Width-1,size.Height-1);
        using(Font body=new Font("Microsoft YaHei UI",10F))using(Font heading=new Font("Microsoft YaHei UI",10F,FontStyle.Bold)){
            int y=Padding;
            for(int i=0;i<Headings.Length;i++){
                int headHeight=TextSize(Headings[i],heading).Height;
                TextRenderer.DrawText(graphics,Headings[i],heading,new Rectangle(Padding,y,Width-Padding*2,headHeight),Color.FromArgb(37,99,177),TextFormatFlags.WordBreak|TextFormatFlags.NoPadding);y+=headHeight+5;
                int bodyHeight=TextSize(Bodies[i],body).Height;
                TextRenderer.DrawText(graphics,Bodies[i],body,new Rectangle(Padding,y,Width-Padding*2,bodyHeight),Color.FromArgb(38,54,74),TextFormatFlags.WordBreak|TextFormatFlags.NoPadding);y+=bodyHeight+14;
            }
        }
    }
    internal void SavePreview(string path){Size size=Measure();using(Bitmap bitmap=new Bitmap(size.Width,size.Height)){using(Graphics graphics=Graphics.FromImage(bitmap))Draw(graphics,size);bitmap.Save(path);}}
    public void Dispose(){tip.Dispose();}
}
}
