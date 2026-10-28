using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace AmazonProfitCalculatorV2 {
    internal sealed class PackQuote { internal decimal? Freight, Delivery; internal string Note; }
    internal sealed partial class MainForm {
        internal bool Embedded;
        internal Func<int,decimal,PackQuote> PackCalculator;
        bool sharedFxValid=true;
        FormulaHoverHint formulaHint;
        internal event EventHandler LayoutUpdated;
        internal void Embed() {
            Embedded=true; TopLevel=false; FormBorderStyle=FormBorderStyle.None;
            AutoScroll=false; rateToggle.Visible=false; ratePanel.Visible=false;
            rateSummary.Visible=false;
            Panel page=(Panel)Controls[0];
            Control reset=page.Controls["reset"];page.Controls.Remove(reset);reset.Dispose();
            Control share=page.Controls["share"];page.Controls.Remove(share);share.Dispose();
            WidenCostColumn((Panel)page.Controls["entry"]);
            UnifyProfitFonts(page);
            CenterProfitControls(page);
            packs.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;
            foreach(Control c in page.Controls)if(c is Label&&c.Top==14)c.SetBounds(20,14,940,38);
            packs.Columns[4].ReadOnly=packs.Columns[5].ReadOnly=true;
            packs.Columns[5].HeaderText="FBA运费 $";
            foreach(Control c in packPanel.Controls)if(c is Label) {
                if(c.Top<30)c.Visible=false;else {c.Text="2 件九折、3 件八折；4 件起请填写整套售价，整套成本按单件成本 × 件数计算。";c.Top=10;}
            }

            var formulaLink=new LinkLabel{Text="公式说明",Location=new Point(834,234),Size=new Size(94,26),Font=new Font("Microsoft YaHei UI",9F),LinkColor=Color.FromArgb(37,99,177),ActiveLinkColor=Color.FromArgb(29,78,150),LinkBehavior=LinkBehavior.HoverUnderline,TextAlign=ContentAlignment.MiddleRight,AccessibleName="公式说明",TabStop=true};
            packPanel.Controls.Add(formulaLink);formulaHint=new FormulaHoverHint(formulaLink);
            Disposed+=delegate{formulaHint.Dispose();};
            packs.Top=40;packs.Height=184;
            Arrange(page);
        }
        void WidenCostColumn(Panel entry) {
            int[] left={14,139,313,438,563,688,813};int[] width={111,160,111,111,111,111,111};
            foreach(Control c in entry.Controls) {
                int column=Math.Max(0,Math.Min(6,(c.Left-14)/132));
                c.Left=left[column];c.Width=width[column];
            }
            for(int i=0;i<fields.Length;i++)fields[i].Width=width[i]-18;
        }
        decimal DefaultPackSale(decimal unitPrice,decimal quantity) {
            decimal discount=quantity==2?.9m:quantity==3?.8m:1m;
            return AmazonFreight.FbaModel.CeilingMoney(unitPrice*quantity*discount);
        }
        int NextPackQuantity() {
            int next=1;decimal quantity;
            foreach(DataGridViewRow row in packs.Rows)if(Parse(CellText(row,0),1000000m,out quantity)&&quantity>=1&&quantity==decimal.Truncate(quantity))next=Math.Max(next,(int)quantity+1);
            return next;
        }
        internal void VerifyPackDiscountAndAdd() {
            bool wasVisible=packPanel.Visible;packPanel.Visible=true;Arrange((Panel)Controls[0]);
            if(DefaultPackSale(20m,2)!=36m||DefaultPackSale(20m,3)!=48m||DefaultPackSale(20m,4)!=80m||DefaultPackSale(29.77m,2)!=53.59m)throw new Exception("Pack discounts and upward cents");
            foreach(int quantity in new[]{4,5}) {
                foreach(Control c in packPanel.Controls)if(c is Button&&c.Text=="+ 添加多件装")((Button)c).PerformClick();
                packs.EndEdit();RefreshAll();DataGridViewRow row=packs.Rows[packs.Rows.Count-1];
                if(Convert.ToString(row.Cells[0].Value)!=quantity.ToString()||Convert.ToString(row.Cells[1].Value)!=""||Convert.ToString(row.Cells[2].Value)!=(13.4m*quantity).ToString("0.00##########################",CultureInfo.InvariantCulture))throw new Exception("Added packs increment quantity with blank price and default cost");
                if(Convert.ToString(row.Cells[4].Value)==""||Convert.ToString(row.Cells[5].Value)!="—"||Convert.ToString(row.Cells[6].Value)!="—"||Convert.ToString(row.Cells[7].Value)!="—")throw new Exception("Blank price keeps freight/cost but waits for FBA and profit");
            }
            SetRowPrice(3,65m);PackQuote manual=PackCalculator(4,65m);CheckRowFee(3,manual.Freight.Value,manual.Delivery.Value);
            if(Convert.ToString(packs.Rows[3].Cells[7].Value)=="—")throw new Exception("Fourth pack manual price calculates margin");
            ResetRowPrice(3);if(Convert.ToString(packs.Rows[3].Cells[1].Value)!=""||Convert.ToString(packs.Rows[3].Cells[5].Value)!="—")throw new Exception("Clear fourth pack returns blank default");
            packs.Rows.RemoveAt(packs.Rows.Count-1);packs.Rows.RemoveAt(packs.Rows.Count-1);RefreshAll();
            packPanel.Visible=wasVisible;Arrange((Panel)Controls[0]);
        }
        internal void CheckCostCaption() {
            Control entry=((Panel)Controls[0]).Controls["entry"];
            bool found=false;
            foreach(Control c in entry.Controls)if(c is Label&&c.Text=="单件成本（人民币）") {
                found=true;if(c.Height!=25||TextRenderer.MeasureText(c.Text,c.Font).Width>c.Width)throw new Exception("Single-line cost caption must fit without smaller font");
            }
            if(!found)throw new Exception("Full single-line cost caption");
        }
        void UnifyProfitFonts(Panel page) {
            foreach(Control c in page.Controls)if(c is Label&&c.Text=="亚马逊利润计算器")c.Font=new Font("Microsoft YaHei UI",21F,FontStyle.Bold);
            foreach(TextBox field in fields)field.Font=new Font("Microsoft YaHei UI",15F,FontStyle.Bold);
            foreach(string name in new[]{"entry","results"})foreach(Control c in page.Controls[name].Controls) {
                if(c is Label&&c!=status)c.Font=new Font("Microsoft YaHei UI",10F,c.Font.Style);
            }
            profit.Font=margin.Font=total.Font=new Font("Microsoft YaHei UI",25F,FontStyle.Bold);
            packs.ColumnHeadersDefaultCellStyle.Font=new Font("Microsoft YaHei UI",10F,FontStyle.Bold);
        }
        static void CenterProfitControls(Control page) {
            foreach(Control c in page.Controls)if(c is Label)((Label)c).TextAlign=ContentAlignment.MiddleLeft;
            foreach(string name in new[]{"entry","results"}) {
                Control card=page.Controls[name];
                foreach(Control c in card.Controls) {
                    Label label=c as Label;if(label!=null)label.TextAlign=ContentAlignment.MiddleCenter;
                    TextBox box=c as TextBox;if(box!=null)box.TextAlign=HorizontalAlignment.Center;
                    foreach(Control inner in c.Controls)if(inner is TextBox)((TextBox)inner).TextAlign=HorizontalAlignment.Center;
                }
            }
            foreach(Control c in page.Controls["results"].Controls)if(c is Label&&c.Top>=85)((Label)c).TextAlign=ContentAlignment.MiddleLeft;
        }
        internal string FontDetails() {
            Panel page=(Panel)Controls[0];Panel entry=(Panel)page.Controls["entry"];
            Control title=null;foreach(Control c in page.Controls)if(c is Label&&c.Text=="亚马逊利润计算器")title=c;
            return "Profit title: "+title.Font+"\r\nProfit inputs: "+fields[0].Font+"\r\nProfit field labels: "+entry.Controls[0].Font+"\r\nProfit result: "+profit.Font+"\r\nProfit total: "+total.Font+"\r\nProfit table: "+packs.DefaultCellStyle.Font+"\r\nProfit table header: "+packs.ColumnHeadersDefaultCellStyle.Font+"\r\nProfit input frame height: "+fields[0].Parent.Height+"; column pitch: "+(fields[1].Parent.Left-fields[0].Parent.Left)+"; column gap: "+(fields[1].Parent.Left-fields[0].Parent.Right)+"; label to frame top: "+(fields[0].Parent.Top-entry.Controls[0].Top);
        }
        internal void SetLinked(bool linked) {
            foreach(int i in new[]{0,2,3}) fields[i].ReadOnly=linked;
            rateToggle.Enabled=false;
            ratePanel.Visible=false;
        }
        internal void Import(decimal? price, decimal? freight, decimal? delivery, decimal? fx) {
            updating=true;
            fields[0].Text=Value(price); fields[2].Text=Value(freight); fields[3].Text=Value(delivery);
            if(fx.HasValue&&fx>0) { exchange=fx.Value; rateInput.Text=Value(fx); }
            sharedFxValid=fx.HasValue&&fx>0;
            updating=false; RefreshAll();
            if(!fx.HasValue) { profit.Text=margin.Text=total.Text="—";status.Text="请在运费计算器填写有效汇率。"; }
        }
        static string Value(decimal? value) { return value.HasValue?value.Value.ToString("0.############################",CultureInfo.InvariantCulture):""; }
        internal void PackageCaption(int count) {
            foreach(Control control in ((Panel)Controls[0]).Controls["entry"].Controls)
                if(control is Label && (control.Text=="单件成本（人民币）"||control.Text=="单件成本"||control.Text=="单件成本\r\n（人民币）")) {control.Text="单件成本（人民币）";control.Top=14;control.Height=25;}
            foreach(Control control in ((Panel)Controls[0]).Controls["results"].Controls)
                if(control is Label && control.Text=="单件利润 · USD") control.Text="当前利润";
        }
        internal void CheckLinked(decimal price,decimal freight,decimal delivery,decimal fx) {
            if(fields[0].Text!=Value(price)||fields[2].Text!=Value(freight)||fields[3].Text!=Value(delivery)||exchange!=fx)
                throw new Exception("Linked source fields mismatch");
            fields[1].Text="13.4";
            Result expected=MathModel.Calculate(price,13.4m,1,fx,freight,delivery,15m,20m,5m);
            if(profit.Text!="$"+Money(expected.Profit)) throw new Exception("Linked profit mismatch: "+profit.Text);
        }
        internal void CheckEmpty() { if(profit.Text!="—"||fields[3].Text!="") throw new Exception("Incomplete source must not show stale profit"); }
        internal void SetSharedRate(decimal? fx) {
            sharedFxValid=fx.HasValue&&fx.Value>0;
            if(sharedFxValid) { exchange=fx.Value;rateInput.Text=Value(fx); }
            RefreshAll();
        }
        internal void CheckSharedRate(decimal fx) { if(exchange!=fx||rateToggle.Visible||ratePanel.Visible)throw new Exception("Shared exchange only"); }
        internal void CheckManual(decimal fx) { fields[0].Text="100";fields[1].Text="134";fields[2].Text="5";fields[3].Text="10";RefreshAll();if(profit.Text!="$"+Money(45m-134m/fx))throw new Exception("Manual formula uses freight exchange"); }
        internal void CheckInvalidRate() { if(profit.Text!="—"||total.Text!="—")throw new Exception("Invalid shared rate"); }
        int PackContentHeight() {
            int rowsHeight=0;foreach(DataGridViewRow row in packs.Rows)if(row.Visible)rowsHeight+=row.Height;
            packs.Height=Math.Max(64,Math.Min(160,packs.ColumnHeadersHeight+rowsHeight+2));
            int buttonTop=packs.Bottom+8;
            foreach(Control c in packPanel.Controls) {
                if(c is Button)c.Top=buttonTop;
                if(c is LinkLabel&&c.Text=="公式说明")c.Top=buttonTop+2;
            }
            return buttonTop+40;
        }
        void UpdatePackSpacing() {
            int height=PackContentHeight();
            if(packPanel.Height!=height)Arrange((Panel)Controls[0]);
        }
        void ArrangeEmbedded(Panel page) {
            page.Width=964;
            page.Controls["entry"].SetBounds(20,64,940,130);
            page.Controls["results"].SetBounds(20,206,940,112);
            packToggle.SetBounds(20,330,259,34);
            packPanel.SetBounds(20,376,940,PackContentHeight());
            page.Controls["foot"].SetBounds(21,packPanel.Visible?packPanel.Bottom+12:381,940,25);
            page.Height=packPanel.Visible?page.Controls["foot"].Bottom+10:416;
            ClientSize=new Size(980,page.Height);
            if(LayoutUpdated!=null)LayoutUpdated(this,EventArgs.Empty);
        }
        internal void CheckPackAutomation() {
            fields[1].Text="0";if(fields[1].ForeColor!=red)throw new Exception("Zero cost red");
            fields[1].Text="13.4";if(fields[1].ForeColor==red)throw new Exception("Nonzero cost normal");
            for(int i=0;i<3;i++)SetRowPrice(i,20m);
            RefreshAll();
            if(!packs.Columns[4].ReadOnly||!packs.Columns[5].ReadOnly)throw new Exception("Automatic fees read only");
            CheckRowFee(0,2.37m,8.03m);
            CheckRowFee(1,4.73m,11.65m);
            packs.Rows[2].Cells[0].Value="";RefreshAll();
            if(Convert.ToString(packs.Rows[2].Cells[4].Value)!=""||Convert.ToString(packs.Rows[2].Cells[5].Value)!="—")throw new Exception("Empty count clears auto fees");
            packs.Rows[2].Cells[0].Value="3";RefreshAll();
        }
        internal void ResetRowPrice(int index) {
            PackOverrides edits=packs.Rows[index].Tag as PackOverrides;
            if(edits!=null)edits.Price=null;
            RefreshAll();
        }
        internal void VerifyDefaultPackFees() {
            fields[0].Text="20";
            for(int i=0;i<3;i++)ResetRowPrice(i);
            CheckAutomaticRows(20m);
            fields[0].Text="21";CheckAutomaticRows(21m);
            // Use the actual grid editor to verify the same path as user typing.
            packs.CurrentCell=packs.Rows[1].Cells[1];packs.BeginEdit(true);
            ((TextBox)packs.EditingControl).Text="8";packs.EndEdit();RefreshAll();
            PackQuote low=PackCalculator(2,8m);CheckRowFee(1,low.Freight.Value,low.Delivery.Value);
            fields[0].Text="22";
            if(Convert.ToString(packs.Rows[1].Cells[1].Value)!="8")throw new Exception("Manual price retained on linked price change");
            SetRowPrice(1,60m);
            PackQuote high=PackCalculator(2,60m);CheckRowFee(1,high.Freight.Value,high.Delivery.Value);
            if(low.Delivery==high.Delivery)throw new Exception("Row sale price must change FBA band");
            packs.CurrentCell=packs.Rows[1].Cells[1];packs.BeginEdit(true);
            ((TextBox)packs.EditingControl).Text="";packs.EndEdit();RefreshAll();
            CheckAutomaticRows(22m);
            fields[0].Text="";RefreshAll();
            if(Convert.ToString(packs.Rows[0].Cells[5].Value)!="—"||Convert.ToString(packs.Rows[0].Cells[7].Value)!="—")throw new Exception("Missing all prices clears FBA and margin");
            fields[0].Text="20";CheckAutomaticRows(20m);
        }
        void CheckAutomaticRows(decimal unitPrice) {
            for(int i=0;i<3;i++) {
                decimal price=AmazonFreight.FbaModel.CeilingMoney(unitPrice*(i+1)*(i==1?.9m:i==2?.8m:1m));
                PackQuote quote=PackCalculator(i+1,price);
                CheckRowFee(i,quote.Freight.Value,quote.Delivery.Value);
                if(Convert.ToString(packs.Rows[i].Cells[1].Value)!=Value(price))throw new Exception("Default pack sale not displayed");
                Result expected=MathModel.Calculate(price,13.4m*(i+1),1,exchange,quote.Freight.Value,quote.Delivery.Value,15m,20m,5m);
                string expectedMargin=(expected.Margin.Value*100m).ToString("0.00",CultureInfo.InvariantCulture)+"%";
                if(Convert.ToString(packs.Rows[i].Cells[6].Value)!=Money(expected.Profit)||Convert.ToString(packs.Rows[i].Cells[7].Value)!=expectedMargin)throw new Exception("Default pack profit/margin mismatch");
            }
        }
        internal void SetRowPrice(int row,decimal price) {
            PackOverrides edits=packs.Rows[row].Tag as PackOverrides;
            if(edits==null)packs.Rows[row].Tag=edits=new PackOverrides();
            edits.Price=Value(price);packs.Rows[row].Cells[1].Value=Value(price);RefreshAll();
        }
        internal void CheckRowFee(int row,decimal freight,decimal delivery) {
            if(Convert.ToString(packs.Rows[row].Cells[4].Value)!=Value(freight)||Convert.ToString(packs.Rows[row].Cells[5].Value)!=Value(delivery))throw new Exception("Pack fee mismatch: row "+row+" freight="+packs.Rows[row].Cells[4].Value+" FBA="+packs.Rows[row].Cells[5].Value);
        }
        internal void ExpandPacks() { packToggle.PerformClick(); }
        internal void SaveFormulaPreview(string folder){formulaHint.SavePreview(Path.Combine(folder,"formula-explanation.png"));}
    }
}

namespace AmazonFreight {
    internal partial class MainForm {
        AmazonProfitCalculatorV2.MainForm profitForm;
        Panel freightBody,calculatorPage;
        Button profitToggle;
        CheckBox profitLinked;
        Label profitLinkNote;
        bool profitExpanded;
        void InitializeProfit() {
            Text="Kuma亚马逊运费计算器";
            ClientSize=new Size(1020,896);
            freightBody=new Panel{Location=Point.Empty,Size=new Size(1000,922),BackColor=BackColor};
            Control[] original=new Control[Controls.Count];Controls.CopyTo(original,0);
            foreach(Control c in original) freightBody.Controls.Add(c);
            calculatorPage=new Panel{Location=Point.Empty,Size=new Size(1000,922),BackColor=BackColor};Controls.Add(calculatorPage);calculatorPage.Controls.Add(freightBody);
            SimplifyOcrLayout();
            profitForm=new AmazonProfitCalculatorV2.MainForm();profitForm.Embed();profitForm.PackCalculator=CalculatePack;
            profitForm.Location=new Point(10,930);calculatorPage.Controls.Add(profitForm);
            profitForm.LayoutUpdated+=delegate{ArrangeIntegration();};
            
            profitToggle=ButtonAt(freightBody,"▾ 展开利润计算器",790,844,188,true);
            profitToggle.Click+=delegate{profitExpanded=!profitExpanded;ArrangeIntegration();if(profitExpanded)ScrollControlIntoView(profitForm);else SetPageScroll(0);};
            profitLinked=new CheckBox{Text="联动运费数据",Checked=true,Location=new Point(20,48),Size=new Size(155,27),Font=new Font("Microsoft YaHei UI",10F),Visible=false};profitForm.Controls[0].Controls.Add(profitLinked);
            profitLinkNote=LabelAt(profitForm.Controls[0],"",185,51,755,26,9,muted,false);profitLinkNote.Visible=false;
            profitLinked.CheckedChanged+=delegate{profitForm.SetLinked(profitLinked.Checked);SyncProfit();};


            persistedPrices=(string[])prices.Clone();

            BoldButtons(this);
            SetupMacScroll();
            profitForm.SetLinked(true);SyncProfit();ArrangeIntegration();
            FormClosed+=delegate{profitForm.Dispose();};
        }
        void ArrangeIntegration() {
            if(profitToggle==null)return;
            profitForm.Visible=profitExpanded;
            if(profitExpanded&&!profitForm.IsHandleCreated)profitForm.Show();
            profitLinked.Visible=profitLinkNote.Visible=false;
            profitToggle.Text=profitExpanded?"▴ 收起利润计算器":"▾ 展开利润计算器";
            Rectangle area=Screen.FromControl(this).WorkingArea;
            ClientSize=new Size(Math.Min(1020,area.Width-16),Math.Min(896,area.Height-40));
            if(Right>area.Right)Left=Math.Max(area.Left,area.Right-Width);
            int contentHeight=profitExpanded?profitForm.Bottom+12:freightBody.Bottom;
            calculatorPage.Size=new Size(1000,contentHeight);
            AutoScrollMinSize=Size.Empty;AutoScroll=false;
            UpdateMacScroll();
            profitToggle.BringToFront();
        }
        void SimplifyOcrLayout() {
            Control oldOcr=paste.Parent;
            oldOcr.Visible=false;
            Button[] buttons={pasteButton,captureButton,fileButton};
            for(int i=0;i<buttons.Length;i++) {
                freightBody.Controls.Add(buttons[i]);buttons[i].SetBounds(562+i*140,40,136,40);
                buttons[i].Font=new Font("Microsoft YaHei UI",10F);
            }
            fileButton.Text="选择图片";
            foreach(Control c in freightBody.Controls) {
                if(c is Label&&c.Top==16)c.Width=540;
                if(c is Label&&c.Top==61)c.Visible=false;
                if(c is Button&&c.Text=="清空数据")c.Left=194;
            }
            Panel packaging=(Panel)weight.Parent.Parent;
            packaging.SetBounds(22,98,956,360);
            foreach(Control c in packaging.Controls) {
                c.Left=18+(int)Math.Round((c.Left-18)*1.45);
                if(c.Width==146||c.Width==138)c.Width=(int)Math.Round(c.Width*1.45);
                else if(c.Width>600)c.Width=Math.Min(920,(int)Math.Round(c.Width*1.45));
                if(c is Label&&c.Text.Contains("包装信息与运输设置"))c.Text="01  包装信息与运输设置";
            }
            foreach(TextBox field in new[]{weight,l,w,h,rate,divisor,exchangeInput})field.Width=field.Parent.Width-20;
            w.Width=w.Parent.Width-48;widthUp.Left=widthDown.Left=w.Parent.Width-32;
            mode.Width=mode.Parent.Width-16;
            pieceCountLabel.Visible=false;
            ApplyVisualRefinement(packaging);
        }
        void SyncProfit() {
            if(profitForm==null||profitLinked==null)return;
            decimal price,fx;
            decimal? sharedRate=Read(exchangeInput,out fx,false)?(decimal?)fx:null;
            profitForm.PackageCaption(pieceCount);
            profitForm.SetSharedRate(sharedRate);
            profitLinkNote.Text=profitLinked.Checked?"售价和运费自动联动；汇率统一使用运费设置。":"售价和运费手动填写；汇率统一使用运费设置。";
            if(!profitLinked.Checked)return;
            profitForm.Import(Read(fbaPrice,out price,true)?(decimal?)price:null,lastUsd.HasValue?(decimal?)Up(lastUsd.Value):null,
                currentFba!=null?currentFba.Total:null,Read(exchangeInput,out fx,false)?(decimal?)fx:null);
            profitForm.PackageCaption(pieceCount);
        }
        AmazonProfitCalculatorV2.PackQuote CalculatePack(int count,decimal price) {
            var quote=new AmazonProfitCalculatorV2.PackQuote();
            decimal mass,width,length,height,d,r,fx,extra;
            if(count<1||!Read(l,out length,false)||!Read(h,out height,false))return quote;
            if(pieceCount==0) { if(!unitWidth.HasValue||!unitWeight.HasValue)return quote; width=unitWidth.Value;mass=unitWeight.Value; }
            else { if(!Read(w,out width,false)||!Read(weight,out mass,false))return quote;width/=pieceCount;mass/=pieceCount; }
            try {
                width*=count;mass*=count;
                if(Read(divisor,out d,false)&&Read(rate,out r,true)&&Read(exchangeInput,out fx,false)) {
                    decimal volume=Model.Volume(length,width,height,d);
                    decimal fee=mass>=volume?mass*r:(length*width*height*r)/d;
                    quote.Freight=Up(fee/fx);
                }
                if(price>0) quote.Delivery=FbaModel.Calculate(mass,length,width,height,price,fbaSeason.SelectedIndex==1,fbaFuel.Checked,Read(fbaSpecialFee,out extra,true)?(decimal?)extra:null).Total;
                quote.Note=price<=0?"请填写上方单件售价或本行整套售价，以计算 FBA 配送费。":quote.Delivery.HasValue?null:"需补填特殊处理费。";
            }catch(OverflowException){quote.Note="数值过大，请检查件数或包装。";}catch(ArgumentOutOfRangeException){quote.Note="请检查包装尺寸、重量和售价。";}
            return quote;
        }
        void VerifyPackAgainstFreight() {
            for(int transport=0;transport<3;transport++) {
                mode.SelectedIndex=transport;if(transport==2){divisor.Text="4000";rate.Text="31";}
                for(int n=1;n<=3;n++) {
                    while(pieceCount<n)widthUp.PerformClick();while(pieceCount>n)widthDown.PerformClick();
                    profitForm.CheckRowFee(n-1,Up(lastUsd.Value),currentFba.Total.Value);
                }
            }
            mode.SelectedIndex=0;
            while(pieceCount>2)widthDown.PerformClick();
            profitForm.SetRowPrice(1,8m);fbaPrice.Text="8";
            profitForm.CheckRowFee(1,Up(lastUsd.Value),currentFba.Total.Value);
            fbaSeason.SelectedIndex=0;fbaFuel.Checked=false;
            profitForm.CheckRowFee(1,Up(lastUsd.Value),currentFba.Total.Value);
            fbaSeason.SelectedIndex=1;fbaFuel.Checked=true;fbaPrice.Text="20";profitForm.SetRowPrice(1,20m);
            if(ClientSize.Width<1000||ClientSize.Width>1020||ClientSize.Height>896)throw new Exception("Restored window bounds");
        }
        public void VerifyIntegration(string folder) {
            Show();Application.DoEvents();
            if(profitForm.Visible||freightBody.Top!=0)throw new Exception("Default collapsed");
            SetReading(weight,.76203m);SetReading(l,25.908m);SetReading(w,25.654m);SetReading(h,12.446m);
            ResetPieceCount(25.654m,.76203m);fbaPrice.Text="17.99";fbaSeason.SelectedIndex=1;fbaFuel.Checked=true;exchangeInput.Text="6.7";Recalculate();
            profitToggle.PerformClick();Application.DoEvents();
            if(!profitForm.Visible||profitForm.Top<freightBody.Bottom||!pageBar.Visible)throw new Exception("Expand below freight with vertical scrolling");
            if(paste.Parent.Visible||pasteButton.Parent!=freightBody||pasteButton.Left>=captureButton.Left||captureButton.Left>=fileButton.Left||fileButton.Text!="选择图片"||profitToggle.Top<780)throw new Exception("Three top-right OCR buttons and lower profit toggle");
            VerifyNewUi(folder);
            VerifyButtonsAndCaption(folder);
            profitForm.CheckLinked(17.99m,2.47m,8.03m,6.7m);
            Snapshot(folder,"integrated-expanded.png");
            SetPageScroll(0);Snapshot(folder,"freight-top.png");
            widthUp.PerformClick();profitForm.CheckLinked(17.99m,Up(lastUsd.Value),currentFba.Total.Value,6.7m);
            exchangeInput.Text="7";profitForm.CheckLinked(17.99m,Up(lastUsd.Value),currentFba.Total.Value,7m);
            fbaPrice.Clear();profitForm.CheckEmpty();
            profitLinked.Checked=false;profitForm.CheckManual(7m);fbaPrice.Text="20";profitForm.CheckManual(7m);
            exchangeInput.Text="6.7";profitForm.CheckSharedRate(6.7m);profitForm.CheckManual(6.7m);
            exchangeInput.Text="0";profitForm.CheckInvalidRate();exchangeInput.Text="7";profitForm.CheckSharedRate(7m);
            profitLinked.Checked=true;profitForm.CheckLinked(20m,Up(lastUsd.Value),currentFba.Total.Value,7m);
            profitForm.CheckPackAutomation();
            VerifyPackAgainstFreight();
            profitForm.VerifyDefaultPackFees();
            profitForm.VerifyPackDiscountAndAdd();
            profitForm.ExpandPacks();Application.DoEvents();ScrollControlIntoView(profitForm);Snapshot(folder,"integrated-packs.png");profitForm.SaveFormulaPreview(folder);
            profitToggle.PerformClick();if(profitForm.Visible||freightBody.Top!=0)throw new Exception("Collapse restores freight");
            Snapshot(folder,"integrated-collapsed.png");
            profitToggle.PerformClick();if(!profitForm.Visible)throw new Exception("Reopen");
            string old=AmazonProfitCalculatorV2.Settings.FilePath;
            AmazonProfitCalculatorV2.Settings.FilePath=Path.Combine(folder,"profit-test-settings.txt");
            File.WriteAllText(AmazonProfitCalculatorV2.Settings.FilePath,"6.7");
            using(var originalProfit=new AmazonProfitCalculatorV2.MainForm())originalProfit.Verify(folder);
            AmazonProfitCalculatorV2.Settings.FilePath=old;
            File.WriteAllText(Path.Combine(folder,"integration-verification.txt"),"PASS: expand/collapse/reopen; profit below freight with working vertical scroll; original font/control sizes restored; OCR simplified to three top-right buttons; toggle below FBA on right; one shared exchange in both linked and manual mode; invalid exchange clears results; default pack price, FBA, profit and margin consistently use unit sale times quantity; live source price updates; real grid manual price edit survives source updates; clearing price restores automatic calculation; missing all prices clears FBA and margin; matching title/label/input/result fonts; removed package/transport subtitles and positive-input/pack-count helper; centered titles/readings; standard-width transport list with mouse/keyboard selection; removed package-width count marker; 2-pack 10 percent and 3-pack 20 percent discounts; added pack counts 4/5 keep blank sales and default costs; head freight remains visible; FBA/profit wait for manually entered price; clearing reverts to blank; share buttons removed; bold button labels; single-line cost caption; close-only rate save prompt; selectable imperial readings; removed link/rate/reset UI; custom Mac-style wheel/drag scrollbar with no native bars; rate-save yes/no decisions; original-size window; zero cost red; automated per-unit width/mass pack quotes match freight UI for sea/air/express, 1/2/3 pieces, FBA price bands, seasons and fuel; live price/freight/FBA/exchange; package quantity avoids double freight multiplication; missing source clears result; manual mode; re-link; original profit calculator regression including multipack overrides, decimal caret, exchange and sharing; isolated settings; visual snapshots.");
            Close();
        }
        void Snapshot(string folder,string name) {
            using(Bitmap b=new Bitmap(Width,Height)) { DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(Path.Combine(folder,name)); }
        }
    }
}
