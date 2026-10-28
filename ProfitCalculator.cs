using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace AmazonProfitCalculatorV2
{
    internal sealed class Result
    {
        public decimal CostUsd, Commission, Advertising, Returns, Total, Profit;
        public decimal? Margin;
    }

    internal static class MathModel
    {
        public static Result Calculate(decimal price, decimal unitCostCny, int quantity, decimal exchange, decimal freight, decimal delivery, decimal commission, decimal advertising, decimal returns)
        {
            if (exchange <= 0 || quantity < 1) throw new ArgumentOutOfRangeException();
            Result r = new Result();
            r.CostUsd = unitCostCny * quantity / exchange;
            r.Commission = price * commission / 100m;
            r.Advertising = price * advertising / 100m;
            r.Returns = price * returns / 100m;
            r.Total = r.CostUsd + freight * quantity + delivery + r.Commission + r.Advertising + r.Returns;
            r.Profit = price - r.Total;
            r.Margin = price == 0 ? (decimal?)null : r.Profit / price;
            return r;
        }
    }

    internal static class Settings
    {
        public static string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KumaAmazonCalculator", "exchange-rate.txt");
        public static decimal Load()
        {
            decimal value;
            try { if (decimal.TryParse(File.ReadAllText(FilePath), NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value > 0 && value <= 1000000m) return value; }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            return 6.7m;
        }
        public static void Save(decimal rate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, rate.ToString(CultureInfo.InvariantCulture));
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
            else File.Move(temporary, FilePath);
        }
    }

    internal sealed class RoundedSurface : Panel
    {
        public RoundedSurface() { DoubleBuffered = true; BackColor = Color.White; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            const int d = 14;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                using (Pen pen = new Pen(Color.FromArgb(212, 222, 233))) e.Graphics.DrawPath(pen, path);
            }
        }
    }

    internal sealed class PackOverrides
    {
        public string CostCny, Freight, Price;
    }

    internal sealed partial class MainForm : Form
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

        private static void VerifyDecimalCaret(TextBox box)
        {
            box.Focus();
            box.Text = "1234"; box.Select(2, 0);
            SendMessage(box.Handle, 0x0102, (IntPtr)'.', IntPtr.Zero);
            if (box.Text != "12.34" || box.SelectionStart != 3) throw new Exception("English decimal caret: " + box.Text + " / " + box.SelectionStart);
            SendMessage(box.Handle, 0x0102, (IntPtr)'5', IntPtr.Zero);
            if (box.Text != "12.534" || box.SelectionStart != 4) throw new Exception("Typing after English decimal: " + box.Text + " / " + box.SelectionStart);
            box.Text = "1234"; box.Select(2, 0);
            SendMessage(box.Handle, 0x0102, (IntPtr)'。', IntPtr.Zero);
            if (box.Text != "12.34" || box.SelectionStart != 3) throw new Exception("Chinese decimal caret: " + box.Text + " / " + box.SelectionStart);
            SendMessage(box.Handle, 0x0102, (IntPtr)'5', IntPtr.Zero);
            if (box.Text != "12.534") throw new Exception("Typing after Chinese decimal: " + box.Text);
            box.Text = "1234"; box.Select(2, 0); box.SelectedText = "。";
            if (box.Text != "12.34" || box.SelectionStart != 3) throw new Exception("Pasted Chinese decimal caret: " + box.Text + " / " + box.SelectionStart);
            SendMessage(box.Handle, 0x0102, (IntPtr)'5', IntPtr.Zero);
            if (box.Text != "12.534" || box.SelectionStart != 4) throw new Exception("Typing after pasted Chinese decimal");
            box.Text = "12.34"; box.Select(1, 0);
            SendMessage(box.Handle, 0x0102, (IntPtr)'.', IntPtr.Zero);
            if (box.Text != "12.34" || box.SelectionStart != 1) throw new Exception("Duplicate decimal must not move caret");
            box.Select(2, 1);
            SendMessage(box.Handle, 0x0102, (IntPtr)'。', IntPtr.Zero);
            if (box.Text != "12.34" || box.SelectionStart != 3) throw new Exception("Replace selected decimal in place");
        }
        private readonly Color ink = Color.FromArgb(27, 43, 65), muted = Color.FromArgb(100, 116, 139), teal = Color.FromArgb(15, 118, 110), red = Color.FromArgb(190, 40, 55);
        private readonly TextBox[] fields = new TextBox[7];
        private readonly TextBox[] feeAmounts = new TextBox[3];
        private TextBox rateInput;
        private Label rateSummary, rateMessage, status;
        private TextBox costUsd, profit, margin, total;
        private Panel ratePanel, packPanel;
        private Button rateToggle, packToggle;
        private DataGridView packs;
        private decimal exchange;
        private bool updating;
        private bool packInteger;
        private string packLastText = "";
        private string packInitialText = "";

        public MainForm()
        {
            exchange = Settings.Load();
            Text = "亚马逊利润计算器";
            Font = new Font("Microsoft YaHei UI", 10F);
            BackColor = Color.FromArgb(243, 246, 250);
            ForeColor = ink;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(980, 460);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScroll = true;

            Panel page = new Panel { Location = Point.Empty, Size = new Size(964, 460) };
            Controls.Add(page);
            Label(page, "亚马逊利润计算器", 20, 14, 470, 38, 20, ink, true);
            rateSummary = Label(page, "", 23, 71, 670, 25, 9, muted, false);
            rateToggle = Button(page, "▸ 修改汇率", 826, 61, 133, 33);
            rateToggle.Click += delegate { ratePanel.Visible = !ratePanel.Visible; rateToggle.Text = ratePanel.Visible ? "▾ 收起汇率" : "▸ 修改汇率"; Arrange(page); };

            ratePanel = Card(page, 20, 103, 940, 56);
            Label(ratePanel, "1 USD =", 12, 17, 82, 27, 10, ink, false);
            rateInput = Input(ratePanel, exchange.ToString("0.####", CultureInfo.InvariantCulture), 95, 6, 110);
            Label(ratePanel, "CNY", 216, 17, 48, 27, 10, muted, false);
            Button save = Button(ratePanel, "保存汇率", 273, 9, 104, 35);
            save.Click += delegate { SaveRate(); };
            rateInput.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { SaveRate(); e.SuppressKeyPress = true; } };
            rateMessage = Label(ratePanel, "保存后下次打开继续使用", 397, 17, 525, 28, 9, muted, false);
            ratePanel.Visible = false;

            Panel entry = Card(page, 20, 108, 940, 130);
            entry.Name = "entry";
            string[] names = { "售价 · USD", "单件成本（人民币）", "头程运费 · USD", "派送运费 · USD", "佣金 · %", "广告费 · %", "退货费用 · %" };
            for (int i = 0; i < fields.Length; i++)
            {
                int x = 14 + i * 132;
                Label(entry, names[i], x, 14, 127, 25, 9, ink, i == 1);
                fields[i] = Input(entry, i == 4 ? "15" : i == 5 ? "20" : i == 6 ? "5" : "0", x, 43, 118);
                fields[i].AccessibleName = names[i];
                fields[i].TabIndex = i;
                fields[i].TextChanged += delegate(object sender, EventArgs e) { RefreshKeepingCaret((TextBox)sender); };
                if (i >= 4) feeAmounts[i - 4] = CopyableResult(entry, "", x, 96, 127, 26, 9, muted, false);
            }
            costUsd = CopyableResult(entry, "", 146, 96, 130, 28, 9, teal, true);
            Label(entry, "每件售价", 14, 96, 120, 26, 9, muted, false);

            Panel results = Card(page, 20, 250, 940, 112);
            results.Name = "results";
            Label(results, "单件利润 · USD", 16, 12, 290, 25, 9, muted, false);
            profit = CopyableResult(results, "", 13, 39, 310, 46, 27, teal, true);
            Label(results, "利润率", 338, 12, 260, 25, 9, muted, false);
            margin = CopyableResult(results, "", 334, 39, 270, 46, 27, teal, true);
            Label(results, "费用合计", 657, 12, 263, 25, 9, muted, false);
            total = CopyableResult(results, "", 653, 43, 270, 41, 21, ink, true);
            status = Label(results, "", 17, 87, 905, 23, 9, muted, false);
            for (int i = 0; i < 2; i++) results.Controls.Add(new Panel { BackColor = Color.FromArgb(220, 229, 236), Location = new Point(319 + i * 320, 16), Size = new Size(1, 70) });

            packToggle = Button(page, "▸ 拓展：多件装定价对比", 20, 374, 259, 34);
            packToggle.Name = "packToggle";
            Button share = Button(page, "保存分享图", 302, 374, 158, 34);
            share.Name = "share";
            share.BackColor = teal; share.ForeColor = Color.White;
            share.Click += delegate
            {
                using (SaveFileDialog dialog = new SaveFileDialog { Title = "保存利润测算分享图", Filter = "PNG 图片|*.png", FileName = "亚马逊利润测算.png", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    try { SaveShareImage(dialog.FileName); }
                    catch (Exception error) { MessageBox.Show(this, "保存失败：" + error.Message, "保存分享图", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
            };
            packToggle.Click += delegate { packPanel.Visible = !packPanel.Visible; packToggle.Text = packPanel.Visible ? "▾ 拓展：多件装定价对比" : "▸ 拓展：多件装定价对比"; Arrange(page); };
            Button reset = Button(page, "清空 / 恢复默认比例", 750, 374, 210, 34);
            reset.Name = "reset";
            reset.Click += delegate
            {
                updating = true;
                for (int i = 0; i < 7; i++) fields[i].Text = i == 4 ? "15" : i == 5 ? "20" : i == 6 ? "5" : "0";
                packs.Rows.Clear(); AddPack(1); AddPack(2); AddPack(3);
                updating = false;
                RefreshAll();
            };

            packPanel = Card(page, 20, 420, 940, 270);
            Label(packPanel, "成本、头程默认按件数计算，也可直接修改；人民币成本按默认汇率折算美元。", 12, 10, 916, 24, 9, ink, false);
            Label(packPanel, "填写件数、售价、成本及运费，即时计算；费用比例沿用上方设置。", 12, 35, 916, 24, 9, muted, false);
            packs = new DataGridView { Location = new Point(12, 64), Size = new Size(916, 160), BackgroundColor = Color.White, BorderStyle = BorderStyle.None, AllowUserToAddRows = false, AllowUserToDeleteRows = true, AllowUserToResizeRows = false, AllowUserToResizeColumns = false, AllowUserToOrderColumns = false, AllowDrop = false, RowHeadersWidth = 25, RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, SelectionMode = DataGridViewSelectionMode.CellSelect, MultiSelect = false, EnableHeadersVisualStyles = false, Font = new Font("Microsoft YaHei UI", 9F) };
            packs.ColumnHeadersDefaultCellStyle.BackColor = ink;
            packs.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            packs.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            packs.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            packs.GridColor = Color.FromArgb(218, 226, 234);
            packs.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            packs.DefaultCellStyle.Font = new Font("Microsoft YaHei UI", 10F);
            packs.ColumnHeadersHeight = 32;
            packs.RowTemplate.Height = 30;
            packs.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            packs.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 235, 245);
            packs.DefaultCellStyle.SelectionForeColor = ink;
            packs.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            packs.MultiSelect = true;
            ContextMenuStrip copyMenu = new ContextMenuStrip();
            copyMenu.Items.Add("复制选中内容", null, delegate
            {
                DataObject data = packs.GetClipboardContent();
                if (data != null) Clipboard.SetDataObject(data, true);
            });
            packs.ContextMenuStrip = copyMenu;
            packs.CellMouseDown += delegate(object sender, DataGridViewCellMouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0 && !packs.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected)
                    packs.CurrentCell = packs.Rows[e.RowIndex].Cells[e.ColumnIndex];
            };
            string[] headers = { "每套件数", "整套售价 $", "整套成本(人民币)", "整套成本 $", "头程运费$", "派送运费$", "每套利润 $", "利润率" };
            int[] widths = { 80, 100, 154, 110, 110, 110, 112, 97 };
            for (int i = 0; i < headers.Length; i++)
            {
                int index = packs.Columns.Add("c" + i, headers[i]);
                packs.Columns[index].SortMode = DataGridViewColumnSortMode.NotSortable;
                packs.Columns[index].ReadOnly = i == 3 || i >= 6;
                packs.Columns[index].Resizable = DataGridViewTriState.False;
                packs.Columns[index].Width = widths[i];
                if (packs.Columns[index].ReadOnly) packs.Columns[index].DefaultCellStyle.BackColor = Color.FromArgb(241, 246, 249);
                if (i >= 6) { packs.Columns[index].DefaultCellStyle.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold); packs.Columns[index].DefaultCellStyle.BackColor = Color.FromArgb(229, 244, 240); }
            }
            packs.CellEndEdit += delegate(object sender, DataGridViewCellEventArgs e)
            {
                DataGridViewRow row = packs.Rows[e.RowIndex];
                string committed = Convert.ToString(row.Cells[e.ColumnIndex].Value);
                if (committed != packInitialText)
                {
                    PackOverrides edits = row.Tag as PackOverrides;
                    if (edits == null) row.Tag = edits = new PackOverrides();
                    if (Embedded && e.ColumnIndex == 1) edits.Price = string.IsNullOrWhiteSpace(committed) ? null : committed;
                    if (e.ColumnIndex == 2) edits.CostCny = committed;
                    if (e.ColumnIndex == 4) edits.Freight = committed;
                }
                RefreshAll();
            };
            packs.EditingControlShowing += delegate(object sender, DataGridViewEditingControlShowingEventArgs e)
            {
                TextBox editor = e.Control as TextBox;
                if (editor != null)
                {
                    packInteger = packs.CurrentCell.ColumnIndex == 0;
                    packLastText = editor.Text;
                    packInitialText = editor.Text;
                    editor.KeyPress -= PackKeyPress; editor.KeyPress += PackKeyPress;
                    editor.TextChanged -= PackEditorChanged; editor.TextChanged += PackEditorChanged;
                }
            };
            packs.RowsRemoved += delegate { RefreshAll(); };
            packPanel.Controls.Add(packs);
            Button add = Button(packPanel, "+ 添加多件装", 12, 232, 143, 30);
            add.Click += delegate { AddPack(Embedded ? (int?)NextPackQuantity() : null); RefreshAll(); packs.CurrentCell = packs.Rows[packs.Rows.Count - 1].Cells[0]; packs.BeginEdit(true); };
            Button remove = Button(packPanel, "删除当前行", 166, 232, 131, 30);
            remove.Click += delegate { if (packs.CurrentCell != null) packs.Rows.RemoveAt(packs.CurrentCell.RowIndex); };
            packPanel.Visible = false;
            updating = true; AddPack(1); AddPack(2); AddPack(3); updating = false;

            Label foot = Label(page, "利润 = 售价 − 成本 − 头程 − 派送 − 佣金 − 广告 − 退货；利润率 = 利润 ÷ 售价。", 21, 425, 940, 25, 9, muted, false);
            foot.Name = "foot";
            Arrange(page);
            RefreshAll();
        }

        private void Arrange(Panel page)
        {
            if (Embedded) { ArrangeEmbedded(page); return; }
            int offset = ratePanel.Visible ? 62 : 0;
            page.Controls["entry"].Top = 108 + offset;
            page.Controls["results"].Top = 250 + offset;
            packToggle.Top = page.Controls["reset"].Top = 374 + offset;
            page.Controls["share"].Top = packToggle.Top;
            packPanel.Top = 420 + offset;
            page.Controls["foot"].Top = (packPanel.Visible ? 702 : 425) + offset;
            int height = (packPanel.Visible ? 737 : 460) + offset;
            page.Height = height;
            ClientSize = new Size(980, Embedded ? height : Math.Min(height, Math.Max(460, Screen.FromControl(this).WorkingArea.Height - 80)));
            if (LayoutUpdated != null) LayoutUpdated(this, EventArgs.Empty);
        }

        private Panel Card(Control parent, int x, int y, int w, int h)
        {
            Panel panel = new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = Color.White };
            parent.Controls.Add(panel); return panel;
        }
        private static Label Label(Control parent, string text, int x, int y, int w, int h, float size, Color color, bool bold)
        {
            Label label = new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h), Font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color };
            parent.Controls.Add(label); return label;
        }
        private TextBox Input(Control parent, string text, int x, int y, int w)
        {
            RoundedSurface surface = new RoundedSurface { Location = new Point(x, y), Size = new Size(w, 42), BackColor = Color.White };
            TextBox box = new TextBox { Text = text, Location = new Point(9, 9), Size = new Size(w - 18, 26), Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold), TextAlign = HorizontalAlignment.Center, BorderStyle = BorderStyle.None };
            string lastText = text;
            box.KeyPress += delegate(object sender, KeyPressEventArgs e) { NumericKeyPress(box, e, false); };
            box.TextChanged += delegate
            {
                if (box.Text.IndexOf('。') >= 0) { int caret = box.SelectionStart; box.Text = box.Text.Replace('。', '.'); box.SelectionStart = Math.Min(caret, box.Text.Length); return; }
                if (AllowedText(box.Text, false)) lastText = box.Text;
                else
                {
                    int caret = box.SelectionStart;
                    box.Text = lastText;
                    box.SelectionStart = Math.Min(caret, box.Text.Length);
                }
            };
            box.Enter += delegate { box.SelectAll(); };
            surface.Click += delegate { box.Focus(); };
            surface.Controls.Add(box);
            parent.Controls.Add(surface); return box;
        }

        private static TextBox CopyableResult(Control parent, string text, int x, int y, int w, int h, float size, Color color, bool bold)
        {
            TextBox box = new TextBox
            {
                Text = text, Location = new Point(x, y), Size = new Size(w, h),
                Font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = color, BackColor = parent.BackColor, ReadOnly = true,
                BorderStyle = BorderStyle.None, AutoSize = false, ShortcutsEnabled = true,
                Cursor = Cursors.IBeam, TabStop = true
            };
            box.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.A) { box.SelectAll(); e.SuppressKeyPress = true; }
            };
            parent.Controls.Add(box);
            return box;
        }

        private static void ReportText(Graphics g, string text, Rectangle bounds, float pixels, Color color, bool bold, bool right)
        {
            using (Font font = new Font("Microsoft YaHei UI", pixels, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat { Alignment = right ? StringAlignment.Far : StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(text, font, brush, bounds, format);
        }

        private void SaveShareImage(string path)
        {
            RefreshAll();
            System.Collections.Generic.List<DataGridViewRow> rows = new System.Collections.Generic.List<DataGridViewRow>();
            if (packPanel.Visible)
                foreach (DataGridViewRow row in packs.Rows) if (!string.IsNullOrWhiteSpace(CellText(row, 0))) rows.Add(row);
            int height = rows.Count == 0 ? 478 : 578 + rows.Count * 44;
            using (Bitmap bitmap = new Bitmap(1280, height))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.FromArgb(244, 247, 250));
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (Brush white = new SolidBrush(Color.White))
                using (Brush accent = new SolidBrush(teal))
                using (Brush pale = new SolidBrush(Color.FromArgb(227, 244, 238)))
                using (Brush dark = new SolidBrush(ink))
                using (Pen rule = new Pen(Color.FromArgb(219, 228, 236)))
                {
                    g.FillRectangle(accent, 32, 32, 6, 42);
                    ReportText(g, "亚马逊利润测算", new Rectangle(54, 29, 760, 50), 32, ink, true, false);
                    ReportText(g, rateSummary.Text, new Rectangle(54, 83, 1170, 28), 18, muted, false, false);
                    string[] labels = { "售价 · USD", "单件成本（人民币）", "头程运费 · USD", "派送运费 · USD", "佣金比例", "广告费比例", "退货费用比例" };
                    for (int i = 0; i < 7; i++)
                    {
                        int x = 32 + i * 175;
                        g.FillRectangle(white, x, 132, 166, 104);
                        ReportText(g, labels[i], new Rectangle(x + 12, 141, 144, 27), i == 1 ? 14 : 16, muted, false, false);
                        ReportText(g, fields[i].Text + (i >= 4 ? "%" : ""), new Rectangle(x + 12, 169, 144, 33), 27, ink, true, false);
                        if (i == 1) ReportText(g, costUsd.Text, new Rectangle(x + 12, 203, 144, 25), 17, teal, true, false);
                        if (i >= 4) ReportText(g, feeAmounts[i - 4].Text, new Rectangle(x + 12, 203, 144, 25), 17, muted, false, false);
                    }
                    string[] resultLabels = { "单件利润 · USD", "利润率", "费用合计" };
                    TextBox[] resultValues = { profit, margin, total };
                    for (int i = 0; i < 3; i++)
                    {
                        int x = 32 + i * 410;
                        g.FillRectangle(i < 2 ? pale : white, x, 258, 396, 132);
                        ReportText(g, resultLabels[i], new Rectangle(x + 22, 271, 352, 30), 19, muted, false, false);
                        ReportText(g, resultValues[i].Text, new Rectangle(x + 18, 310, 362, 62), 45, resultValues[i].ForeColor, true, false);
                    }
                    if (rows.Count > 0)
                    {
                        ReportText(g, "多件装定价对比", new Rectangle(32, 416, 800, 36), 25, ink, true, false);
                        ReportText(g, "金额按表头币种展示；利润以美元核算，费用比例沿用上方设置。", new Rectangle(32, 456, 1216, 28), 17, muted, false, false);
                        int[] widths = { 110, 140, 190, 155, 155, 155, 170, 141 };
                        int x = 32;
                        for (int i = 0; i < 8; i++)
                        {
                            g.FillRectangle(dark, x, 498, widths[i], 42);
                            ReportText(g, packs.Columns[i].HeaderText, new Rectangle(x + 10, 501, widths[i] - 20, 36), 17, Color.White, true, i != 0);
                            x += widths[i];
                        }
                        for (int r = 0; r < rows.Count; r++)
                        {
                            int y = 540 + r * 44;
                            x = 32;
                            for (int i = 0; i < 8; i++)
                            {
                                g.FillRectangle(i >= 6 ? pale : white, x, y, widths[i], 44);
                                ReportText(g, CellText(rows[r], i), new Rectangle(x + 10, y + 2, widths[i] - 20, 40), 22, rows[r].Cells[i].Style.ForeColor, i == 0 || i >= 6, i != 0);
                                x += widths[i];
                            }
                            g.DrawLine(rule, 32, y + 43, 1248, y + 43);
                        }
                    }
                    ReportText(g, "利润 = 售价 − 成本 − 头程 − 派送 − 佣金 − 广告 − 退货    |    利润率 = 利润 ÷ 售价", new Rectangle(32, height - 34, 1216, 25), 16, muted, false, false);
                }
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        private Button Button(Control parent, string text, int x, int y, int w, int h)
        {
            Button button = new Button { Font = new Font(parent.Font,parent.Font.Style|FontStyle.Bold), Text = text, Location = new Point(x, y), Size = new Size(w, h), FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = ink };
            button.FlatAppearance.BorderColor = Color.FromArgb(207, 218, 230);
            parent.Controls.Add(button); return button;
        }
        private static bool Parse(string text, decimal max, out decimal value)
        {
            return decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value) && value >= 0 && value <= max;
        }
        private static string Money(decimal value) { return value.ToString("N2", CultureInfo.InvariantCulture); }

        private void SaveRate()
        {
            decimal value;
            if (!Parse(rateInput.Text, 1000000m, out value) || value <= 0) { rateMessage.Text = "请输入大于 0 的有效汇率（人民币 / 美元）"; rateMessage.ForeColor = red; return; }
            try { Settings.Save(value); }
            catch (Exception e) { if (!(e is IOException) && !(e is UnauthorizedAccessException)) throw; rateMessage.Text = "汇率保存失败，请重试。"; rateMessage.ForeColor = red; return; }
            exchange = value;
            rateMessage.Text = "已保存，下次打开将继续使用此汇率";
            rateMessage.ForeColor = teal;
            RefreshAll();
        }

        private void AddPack(int? quantity)
        {
            packs.Rows.Add(quantity.HasValue ? quantity.Value.ToString() : "", "0", "—", "—", "—", "0", "—", "—");
        }

        private static bool AllowedText(string value, bool integer)
        {
            bool dot = false;
            foreach (char c in value)
            {
                if (c >= '0' && c <= '9') continue;
                if (c == '.' && !integer && !dot) { dot = true; continue; }
                return false;
            }
            return true;
        }

        private static void NumericKeyPress(TextBox box, KeyPressEventArgs e, bool integer)
        {
            if (char.IsControl(e.KeyChar)) return;
            if (e.KeyChar == '。') e.KeyChar = '.';
            string proposed = box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, e.KeyChar.ToString());
            e.Handled = !AllowedText(proposed, integer);
            if (!e.Handled && e.KeyChar == '.')
            {
                // Insert at the selection explicitly, so input methods and recalculation
                // cannot move the decimal point or the insertion caret to the end.
                int start = box.SelectionStart;
                e.Handled = true;
                box.SelectedText = ".";
                box.Select(start + 1, 0);
            }
        }

        private void RefreshKeepingCaret(TextBox box)
        {
            int start = box.SelectionStart;
            int length = box.SelectionLength;
            RefreshAll();
            if (!box.IsDisposed) box.Select(Math.Min(start, box.TextLength), Math.Min(length, Math.Max(0, box.TextLength - start)));
        }

        private void PackKeyPress(object sender, KeyPressEventArgs e) { NumericKeyPress((TextBox)sender, e, packInteger); }

        private void PackEditorChanged(object sender, EventArgs e)
        {
            TextBox editor = (TextBox)sender;
            if (editor.Text.IndexOf('。') >= 0) { int caret = editor.SelectionStart; editor.Text = editor.Text.Replace('。', '.'); editor.SelectionStart = Math.Min(caret, editor.Text.Length); return; }
            if (!AllowedText(editor.Text, packInteger))
            {
                int caret = editor.SelectionStart;
                editor.Text = packLastText;
                editor.SelectionStart = Math.Min(caret, editor.Text.Length);
                return;
            }
            packLastText = editor.Text;
            RefreshKeepingCaret(editor);
        }

        private string CellText(DataGridViewRow row, int column)
        {
            if (packs.CurrentCell != null && packs.CurrentCell.RowIndex == row.Index && packs.CurrentCell.ColumnIndex == column && packs.IsCurrentCellInEditMode && packs.EditingControl is TextBox)
                return ((TextBox)packs.EditingControl).Text;
            return Convert.ToString(row.Cells[column].Value);
        }

        private void RefreshAll()
        {
            if (updating || profit == null || packs == null) return;
            rateSummary.Text = Embedded ? (sharedFxValid ? "汇率沿用运费设置：1 USD = " + exchange.ToString("0.####", CultureInfo.InvariantCulture) + " CNY" : "请在运费计算器填写有效汇率") : "默认汇率：1 USD = " + exchange.ToString("0.####", CultureInfo.InvariantCulture) + " CNY（人民币）";
            decimal[] v = new decimal[7];
            bool valid = !Embedded || sharedFxValid;
            for (int i = 0; i < 7; i++)
            {
                bool ok = Parse(fields[i].Text, i < 4 ? 100000000m : 100m, out v[i]);
                fields[i].BackColor = ok ? Color.White : Color.FromArgb(255, 235, 236);
                fields[i].Parent.BackColor = fields[i].BackColor;
                fields[i].ForeColor = Embedded && i == 1 && ok && v[i] == 0 ? red : ink;
                valid &= ok;
            }
            if (valid)
            {
                Result r = MathModel.Calculate(v[0], v[1], 1, exchange, v[2], v[3], v[4], v[5], v[6]);
                costUsd.Text = "≈ $" + Money(r.CostUsd);
                profit.Text = "$" + Money(r.Profit);
                margin.Text = r.Margin.HasValue ? (r.Margin.Value * 100m).ToString("0.00", CultureInfo.InvariantCulture) + "%" : "—";
                total.Text = "$" + Money(r.Total);
                profit.ForeColor = margin.ForeColor = r.Profit < 0 ? red : teal;
                total.ForeColor = ink;
                costUsd.ForeColor = teal;
                feeAmounts[0].Text = "$" + Money(r.Commission);
                feeAmounts[1].Text = "$" + Money(r.Advertising);
                feeAmounts[2].Text = "$" + Money(r.Returns);
                foreach (TextBox amount in feeAmounts) amount.ForeColor = muted;
                status.Text = v[0] == 0 ? "售价为 0，利润率无法计算" : r.Profit < 0 ? "当前为亏损" : r.Profit == 0 ? "当前为盈亏平衡" : "";
            }
            else
            {
                profit.Text = margin.Text = total.Text = costUsd.Text = "—";
                foreach (TextBox label in feeAmounts) label.Text = "—";
                status.Text = "请检查标红数字是否正确";
            }
            bool packInputsValid = Embedded ? sharedFxValid && Parse(fields[1].Text,100000000m,out v[1]) && Parse(fields[4].Text,100m,out v[4]) && Parse(fields[5].Text,100m,out v[5]) && Parse(fields[6].Text,100m,out v[6]) : valid;
            foreach (DataGridViewRow row in packs.Rows)
            {
                decimal quantity, price, delivery;
                // Parse all cells separately to keep invalid and unfinished edits out of calculations.
                bool q = Parse(CellText(row, 0), 1000000m, out quantity) && quantity >= 1 && quantity == decimal.Truncate(quantity);
                bool p = Parse(CellText(row, 1), 100000000m, out price);
                bool d = Parse(CellText(row, 5), 100000000m, out delivery);
                bool empty = string.IsNullOrWhiteSpace(CellText(row, 0));
                PackOverrides edits = row.Tag as PackOverrides;
                bool editingPrice = packs.IsCurrentCellInEditMode && packs.CurrentCell == row.Cells[1];
                if (Embedded) {
                    decimal basePrice;
                    string priceText = editingPrice ? CellText(row,1) : edits != null && edits.Price != null ? edits.Price : q && quantity<4 && Parse(fields[0].Text,100000000m,out basePrice) ? Value(DefaultPackSale(basePrice,quantity)) : "";
                    p = Parse(priceText,100000000000000m,out price);
                    if(!editingPrice)row.Cells[1].Value=priceText;
                }
                bool editingCost = packs.IsCurrentCellInEditMode && packs.CurrentCell == row.Cells[2];
                bool editingFreight = packs.IsCurrentCellInEditMode && packs.CurrentCell == row.Cells[4];
                string costText = editingCost ? CellText(row, 2) : edits != null && edits.CostCny != null ? edits.CostCny : packInputsValid && q ? (v[1] * quantity).ToString("0.00##########################", CultureInfo.InvariantCulture) : "";
                string freightText = editingFreight ? CellText(row, 4) : edits != null && edits.Freight != null ? edits.Freight : valid && q ? (v[2] * quantity).ToString("0.00##########################", CultureInfo.InvariantCulture) : "";
                if (Embedded && PackCalculator != null) {
                    PackQuote quote = q ? PackCalculator((int)quantity, p ? price : 0m) : new PackQuote();
                    freightText = quote.Freight.HasValue ? Value(quote.Freight) : "";
                    row.Cells[5].Value = quote.Delivery.HasValue ? Value(quote.Delivery) : "—";
                    d = quote.Delivery.HasValue; delivery = quote.Delivery.GetValueOrDefault();
                    row.ErrorText = quote.Note ?? "";
                }
                decimal totalCostCny, totalFreight;
                bool c = Parse(costText, 100000000000000m, out totalCostCny);
                bool f = Parse(freightText, 100000000000000m, out totalFreight);
                bool waitingPrice=Embedded&&q&&quantity>=4&&!editingPrice&&(edits==null||edits.Price==null);
                bool[] cellOk = { q || empty, p || waitingPrice, c || empty, true, f || empty, d || waitingPrice };
                for (int i = 0; i < 6; i++) row.Cells[i].Style.BackColor = i == 3 ? Color.FromArgb(241, 246, 249) : cellOk[i] ? Color.White : Color.FromArgb(255, 235, 236);
                if (!editingCost) row.Cells[2].Value = costText;
                row.Cells[3].Value = c && (!Embedded || sharedFxValid) ? Money(totalCostCny / exchange) : "—";
                if (!editingFreight) row.Cells[4].Value = freightText;
                if (!packInputsValid || !q || !p || !d || !c || !f) { for (int i = 6; i < 8; i++) row.Cells[i].Value = "—"; row.ErrorText = empty ? "" : waitingPrice ? "请填写整套售价" : "请检查输入及上方参数"; }
                else
                {
                    row.ErrorText = "";
                    Result r = MathModel.Calculate(price, totalCostCny, 1, exchange, totalFreight, delivery, v[4], v[5], v[6]);
                    row.Cells[6].Value = Money(r.Profit);
                    row.Cells[7].Value = r.Margin.HasValue ? (r.Margin.Value * 100m).ToString("0.00", CultureInfo.InvariantCulture) + "%" : "—";
                }
                for (int i = 0; i < 8; i++)
                {
                    decimal number;
                    Color normal = i >= 6 ? teal : ink;
                    string display = CellText(row, i);
                    Color color = normal;
                    if (decimal.TryParse(display.Replace("%", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out number) && ((i == 5 && number == 0) || (i == 7 && number < 0))) color = red;
                    row.Cells[i].Style.ForeColor = row.Cells[i].Style.SelectionForeColor = color;
                    if (packs.CurrentCell == row.Cells[i] && packs.IsCurrentCellInEditMode && packs.EditingControl != null) packs.EditingControl.ForeColor = color;
                }
            }
            if(Embedded)UpdatePackSpacing();
        }

        public void Verify(string folder)
        {
            Show(); Application.DoEvents();
            VerifyDecimalCaret(fields[0]); fields[0].Text = "0";
            VerifyDecimalCaret(rateInput); rateInput.Text = "6.7";
            if (fields[0].ForeColor == red || fields[1].ForeColor == red || profit.ForeColor == red || total.ForeColor == red || feeAmounts[0].ForeColor == red || costUsd.ForeColor == red) throw new Exception("Main zero values normal");
            fields[0].Text = "100"; fields[1].Text = "134"; fields[2].Text = "5"; fields[3].Text = "10";
            if (profit.Text != "$25.00" || margin.Text != "25.00%") throw new Exception("Main UI calculation");
            foreach (TextBox result in new TextBox[] { profit, margin, total, costUsd, feeAmounts[0], feeAmounts[1], feeAmounts[2] })
            {
                if (!result.ReadOnly || !result.ShortcutsEnabled) throw new Exception("Result must be read-only and copyable");
                result.SelectAll();
                if (result.SelectedText != result.Text || result.SelectionLength == 0) throw new Exception("Result text selection");
                result.Select(0, 0);
            }
            using (Bitmap bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(folder, "preview-v2.png")); }
            SaveShareImage(Path.Combine(folder, "share-single.png"));
            packToggle.PerformClick(); rateToggle.PerformClick();
            if (!packPanel.Visible || !ratePanel.Visible) throw new Exception("Expansion toggles");
            if (packs.Rows.Count != 3 || Convert.ToString(packs.Rows[0].Cells[0].Value) != "1" || Convert.ToString(packs.Rows[1].Cells[0].Value) != "2" || Convert.ToString(packs.Rows[2].Cells[0].Value) != "3") throw new Exception("Default quantities 1/2/3");
            string[] expectedHeaders = { "每套件数", "整套售价 $", "整套成本(人民币)", "整套成本 $", "头程运费$", "派送运费$", "每套利润 $", "利润率" };
            if (packs.Columns.Count != 8 || packs.AllowUserToResizeColumns || packs.AllowUserToResizeRows || packs.AllowUserToOrderColumns || packs.AllowDrop) throw new Exception("Fixed eight-column layout");
            for (int i = 0; i < expectedHeaders.Length; i++) if (packs.Columns[i].HeaderText != expectedHeaders[i] || packs.Columns[i].Resizable != DataGridViewTriState.False || packs.Columns[i].SortMode != DataGridViewColumnSortMode.NotSortable) throw new Exception("Column order / resize / sorting");
            if (packs.Rows[0].ReadOnly || packs.Columns[2].ReadOnly || packs.Columns[4].ReadOnly || packs.Columns[5].ReadOnly || Convert.ToString(packs.Rows[0].Cells[1].Value) != "0" || Convert.ToString(packs.Rows[0].Cells[5].Value) != "0") throw new Exception("Editable cost and shipping");
            packs.Rows[0].Cells[1].Value = "90"; packs.Rows[0].Cells[5].Value = "7"; RefreshAll();
            if (Convert.ToString(packs.Rows[0].Cells[6].Value) != "22.00") throw new Exception("Independent first row profit");
            fields[0].Text = "110"; fields[3].Text = "15";
            if (Convert.ToString(packs.Rows[0].Cells[1].Value) != "90" || Convert.ToString(packs.Rows[0].Cells[5].Value) != "7" || Convert.ToString(packs.Rows[0].Cells[6].Value) != "22.00") throw new Exception("Main price / delivery must not overwrite first row");
            fields[0].Text = "100"; fields[3].Text = "10";
            packs.Rows[1].Cells[1].Value = "180"; packs.Rows[1].Cells[5].Value = "12";
            RefreshAll();
            if (Convert.ToString(packs.Rows[1].Cells[6].Value) != "46.00" || Convert.ToString(packs.Rows[1].Cells[4].Value) != "10.00" || Convert.ToString(packs.Rows[1].Cells[2].Value) != "268.00" || Convert.ToString(packs.Rows[1].Cells[7].Value) != "25.56%") throw new Exception("Pack UI / CNY display / USD calculation");
            if (Convert.ToString(packs.Rows[1].Cells[3].Value) != "40.00" || packs.Rows[2].Cells[1].Style.ForeColor == red || packs.Rows[2].Cells[5].Style.ForeColor != red || packs.Rows[2].Cells[6].Style.ForeColor == red) throw new Exception("Dual cost / only zero delivery red");
            packs.ClearSelection(); packs.Rows[1].Cells[3].Selected = true;
            DataObject copiedData = packs.GetClipboardContent();
            if (copiedData == null || !Convert.ToString(copiedData.GetData(DataFormats.UnicodeText)).Contains("40.00")) throw new Exception("Grid dollar conversion copy");
            packs.CurrentCell = packs.Rows[1].Cells[1]; packs.BeginEdit(true);
            TextBox liveEditor = (TextBox)packs.EditingControl;
            VerifyDecimalCaret(liveEditor);
            liveEditor.Text = "200";
            if (!packs.IsCurrentCellInEditMode || Convert.ToString(packs.Rows[1].Cells[6].Value) != "58.00") throw new Exception("Live price without Enter");
            foreach (string invalid in new string[] { "abc", "-20", "1.2.3", "12．5", "1e3", "1,000", " 20" })
            {
                liveEditor.Text = invalid;
                if (liveEditor.Text != "200") throw new Exception("Reject invalid pasted grid text: " + invalid);
            }
            liveEditor.Text = "";
            if (Convert.ToString(packs.Rows[1].Cells[6].Value) != "—") throw new Exception("Incomplete live edit");
            liveEditor.Text = "0";
            if (liveEditor.ForeColor == red || Convert.ToString(packs.Rows[1].Cells[6].Value) != "-62.00" || packs.Rows[1].Cells[6].Style.ForeColor == red) throw new Exception("Live zero price / negative profit normal");
            liveEditor.Text = "180"; packs.EndEdit();
            packs.CurrentCell = packs.Rows[1].Cells[0]; packs.BeginEdit(true);
            ((TextBox)packs.EditingControl).Text = "2.5";
            if (((TextBox)packs.EditingControl).Text != "2") throw new Exception("Integer quantity input");
            ((TextBox)packs.EditingControl).Text = "4";
            if (Convert.ToString(packs.Rows[1].Cells[2].Value) != "536.00" || Convert.ToString(packs.Rows[1].Cells[3].Value) != "80.00" || Convert.ToString(packs.Rows[1].Cells[4].Value) != "20.00") throw new Exception("Live quantity / both costs / shipping");
            ((TextBox)packs.EditingControl).Text = "2"; packs.EndEdit();
            packs.Rows[0].Cells[5].Value = "5"; packs.Rows[0].Cells[1].Value = "50"; RefreshAll();
            if (Convert.ToString(packs.Rows[0].Cells[6].Value) != "0.00" || packs.Rows[0].Cells[6].Style.ForeColor == red || packs.Rows[0].Cells[7].Style.ForeColor == red) throw new Exception("Zero profit / margin normal");
            packs.Rows[0].Cells[1].Value = "40"; RefreshAll();
            if (packs.Rows[0].Cells[7].Style.ForeColor != red || packs.Rows[0].Cells[7].Style.SelectionForeColor != red || packs.Rows[0].Cells[6].Style.ForeColor == red) throw new Exception("Only negative margin red");
            packs.CurrentCell = packs.Rows[0].Cells[5]; packs.BeginEdit(true);
            ((TextBox)packs.EditingControl).Text = "0";
            if (packs.EditingControl.ForeColor != red || packs.Rows[0].Cells[5].Style.ForeColor != red) throw new Exception("Zero delivery live red");
            ((TextBox)packs.EditingControl).Text = "1";
            if (packs.EditingControl.ForeColor == red || packs.Rows[0].Cells[5].Style.ForeColor == red) throw new Exception("Nonzero delivery restores normal");
            packs.EndEdit();
            packs.Rows[0].Cells[5].Value = "7"; packs.Rows[0].Cells[1].Value = "90"; RefreshAll();
            fields[2].Text = "6";
            if (Convert.ToString(packs.Rows[0].Cells[6].Value) != "21.00" || Convert.ToString(packs.Rows[1].Cells[4].Value) != "12.00" || Convert.ToString(packs.Rows[1].Cells[6].Value) != "44.00") throw new Exception("Live shipping update");
            fields[2].Text = "5";
            packs.Rows[1].Cells[0].Value = "4"; RefreshAll();
            if (Convert.ToString(packs.Rows[1].Cells[4].Value) != "20.00" || Convert.ToString(packs.Rows[1].Cells[6].Value) != "-4.00") throw new Exception("Quantity shipping update");
            packs.Rows[1].Cells[0].Value = "2";
            AddPack(null); RefreshAll();
            if (Convert.ToString(packs.Rows[3].Cells[0].Value) != "" || Convert.ToString(packs.Rows[3].Cells[6].Value) != "—" || packs.Rows[3].ErrorText != "") throw new Exception("Blank new quantity");
            fields[0].Text = "bad";
            if (fields[0].Text != "100" || profit.Text != "$25.00") throw new Exception("Reject invalid main input");
            fields[0].Text = "12.34";
            fields[0].Text = "12。34";
            if (fields[0].Text != "12.34") throw new Exception("Chinese decimal normalized");
            fields[0].Text = "12.34.5";
            if (fields[0].Text != "12.34") throw new Exception("Reject duplicate dot");
            fields[0].Text = "100";
            packs.Rows[2].Cells[1].Value = "100"; packs.Rows[2].Cells[5].Value = "5";
            packs.CurrentCell = packs.Rows[2].Cells[2]; packs.BeginEdit(true);
            ((TextBox)packs.EditingControl).Text = "67。00";
            if (((TextBox)packs.EditingControl).Text != "67.00" || Convert.ToString(packs.Rows[2].Cells[3].Value) != "10.00" || Convert.ToString(packs.Rows[2].Cells[6].Value) != "30.00") throw new Exception("Live editable CNY cost / conversion");
            packs.EndEdit();
            packs.CurrentCell = packs.Rows[2].Cells[4]; packs.BeginEdit(true);
            ((TextBox)packs.EditingControl).Text = "8。00";
            if (Convert.ToString(packs.Rows[2].Cells[6].Value) != "37.00") throw new Exception("Live editable total freight");
            packs.EndEdit(); RefreshAll();
            if (Convert.ToString(packs.Rows[2].Cells[2].Value) != "67.00" || Convert.ToString(packs.Rows[2].Cells[4].Value) != "8.00" || Convert.ToString(packs.Rows[2].Cells[6].Value) != "37.00") throw new Exception("Manual values persist across refresh");
            if (!rateSummary.Text.StartsWith("默认汇率") || status.Text != "" || fields[1].AccessibleName != "单件成本（人民币）") throw new Exception("Updated labels / no profitable status");
            Application.DoEvents();
            using (Bitmap bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(folder, "preview-v2-expanded.png")); }
            SaveShareImage(Path.Combine(folder, "share-comparison.png"));
            using (Bitmap exported = new Bitmap(Path.Combine(folder, "share-comparison.png"))) if (exported.Width != 1280 || exported.Height != 710) throw new Exception("Share report dimensions / blank row excluded");
            rateInput.Text = "7.2";
            rateInput.Text = "7。2";
            if (rateInput.Text != "7.2") throw new Exception("Chinese rate decimal normalized");
            SaveRate();
            if (exchange != 7.2m || Settings.Load() != 7.2m) throw new Exception("Rate persistence");
            if (Convert.ToString(packs.Rows[1].Cells[2].Value) != "268.00" || Convert.ToString(packs.Rows[1].Cells[6].Value) != "48.78") throw new Exception("CNY remains constant / USD recalculates after rate change");
            rateInput.Text = "0"; SaveRate();
            if (exchange != 7.2m || Settings.Load() != 7.2m) throw new Exception("Invalid rate persistence");
            using (MainForm next = new MainForm()) if (next.exchange != 7.2m || next.ratePanel.Visible) throw new Exception("Rate reload / collapsed state");
            packs.CurrentCell = packs.Rows[0].Cells[0];
            foreach (Control c in packPanel.Controls) if (c is Button && c.Text == "删除当前行") ((Button)c).PerformClick();
            if (packs.Rows.Count != 3 || Convert.ToString(packs.Rows[0].Cells[0].Value) != "2") throw new Exception("First row can be deleted");
            if (ClientSize.Width != 980 || packToggle.Text.IndexOf("多件装定价对比") < 0) throw new Exception("Compact layout / button title");
            Close();
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--verify")
            {
                Directory.CreateDirectory(args[1]);
                Settings.FilePath = Path.Combine(args[1], "test-settings", "exchange-rate.txt");
                try
                {
                    if (File.Exists(Settings.FilePath)) File.Delete(Settings.FilePath);
                    if (Settings.Load() != 6.7m) throw new Exception("Default rate");
                    Result r = MathModel.Calculate(100m, 134m, 1, 6.7m, 5m, 10m, 15m, 20m, 5m);
                    if (r.Profit != 25m || r.Margin != .25m || r.CostUsd != 20m) throw new Exception("Single calculation");
                    r = MathModel.Calculate(180m, 134m, 2, 6.7m, 5m, 12m, 15m, 20m, 5m);
                    if (r.Profit != 46m || r.CostUsd != 40m) throw new Exception("Pack calculation");
                    r = MathModel.Calculate(0m, 134m, 1, 6.7m, 5m, 10m, 15m, 20m, 5m);
                    if (r.Margin.HasValue || r.Profit != -35m) throw new Exception("Zero price");
                    r = MathModel.Calculate(10m, 134m, 1, 6.7m, 5m, 10m, 15m, 20m, 5m);
                    if (r.Profit != -29m || r.Margin != -2.9m) throw new Exception("Loss");
                    using (MainForm form = new MainForm()) form.Verify(args[1]);
                    File.WriteAllText(Path.Combine(args[1], "verification-v2.txt"), "PASS: native English/Chinese decimal insertion in middle; continued typing; Chinese decimal paste; selected decimal replacement; duplicate decimal preserves caret; verified main/rate/grid editors; live calculations; editable CNY cost/freight/delivery; saved exchange rate; copyable results; fixed table; colors; rendered UI.");
                    return 0;
                }
                catch (Exception e) { File.WriteAllText(Path.Combine(args[1], "verification-v2.txt"), e.ToString()); return 1; }
            }
            Application.Run(new MainForm()); return 0;
        }
    }
}
