using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AmazonFreight {
internal partial class MainForm {
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
    internal void PrepareLifecycleCheck(string scenario,string folder){
        RoundedComboFace selector=null;foreach(Control c in mode.Parent.Controls)if(c is RoundedComboFace)selector=(RoundedComboFace)c;
        if(selector==null)throw new Exception("Transport selector missing");
        if(scenario=="double"){
            profitToggle.PerformClick();SetPageScroll(0);Application.DoEvents();
            bool expanded=profitExpanded;int offset=pageOffset;FormWindowState state=WindowState;
            selector.VerifyDoubleClickInteraction();
            if(!Visible||WindowState!=state||profitExpanded!=expanded||pageOffset!=offset)throw new Exception("Double click changed the calculator window or expanded page");
        }else if(scenario=="open")selector.OpenForCloseCheck();
        else if(scenario=="closed")selector.VerifyChoices(Path.Combine(folder,"lifecycle-dropdown.png"));
        else throw new Exception("Unknown lifecycle scenario");
        File.WriteAllText(Path.Combine(folder,"lifecycle-"+scenario+"-started.txt"),"Closing via native window close command.");
        if(!PostMessage(Handle,0x112,new IntPtr(0xf060),IntPtr.Zero))throw new Exception("Native close command failed");
    }
    internal void CaptureGuide(string folder){
        Show();Application.DoEvents();
        mode.SelectedIndex=0;rate.Text="12";exchangeInput.Text="6.7";
        ResetPieceCount(11.938m,.13154m);SetReading(weight,.13154m);SetReading(l,13.716m);SetReading(w,11.938m);SetReading(h,6.096m);
        fbaPrice.Text="29.77";fbaSeason.SelectedIndex=1;fbaFuel.Checked=true;Recalculate();
        profitForm.SetGuideCost(13.4m);SetPageScroll(0);Snapshot(folder,"guide-freight.png");
        profitToggle.PerformClick();profitForm.ExpandPacks();Application.DoEvents();ScrollControlIntoView(profitForm);
        Snapshot(folder,"guide-profit.png");profitForm.SaveFormulaPreview(folder);Close();
    }
}
}
namespace AmazonProfitCalculatorV2 {
internal sealed partial class MainForm {
    internal void SetGuideCost(decimal cost){fields[1].Text=cost.ToString(System.Globalization.CultureInfo.InvariantCulture);}
}
}
