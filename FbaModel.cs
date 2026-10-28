using System;
using System.Globalization;
using System.IO;

namespace AmazonFreight {
internal enum FbaTier { SmallStandard,LargeStandard,SmallBulky,LargeBulky,Extra50,Extra70,Extra150,ExtraOver150 }
internal sealed class FbaResult {
    public FbaTier Tier;public decimal ActualLb,DimensionalLb,ChargeLb,BaseFee,FuelFee,KnownFee;public decimal? Total;public bool SpecialHandling;
    public string Name {get{return new[]{"小号标准尺寸","大号标准尺寸","小号大件","大号大件","超大件 0–50 磅","超大件 50–70 磅","超大件 70–150 磅","超大件超过 150 磅"}[(int)Tier];}}
}
internal static class FbaModel {
    public const decimal KgPerLb=.45359237m,CmPerInch=2.54m;
    // US, non-apparel, 2026. Columns: <$10 / $10-$50 inclusive / >$50.
    // PDF rules-1 pages 1-6 (non-peak) and 6-9 (peak), excludes fuel surcharge.
    static readonly decimal[,] SmallOff={
        {2.43m,3.32m,3.58m},{2.49m,3.42m,3.68m},{2.56m,3.45m,3.71m},{2.66m,3.54m,3.80m},
        {2.77m,3.68m,3.94m},{2.82m,3.78m,4.04m},{2.92m,3.91m,4.17m},{2.95m,3.96m,4.22m}};
    static readonly decimal[,] SmallPeak={
        {2.62m,3.51m,3.77m},{2.68m,3.61m,3.87m},{2.76m,3.65m,3.91m},{2.86m,3.74m,4.00m},
        {2.98m,3.89m,4.15m},{3.03m,3.99m,4.25m},{3.14m,4.13m,4.39m},{3.17m,4.18m,4.44m}};
    static readonly decimal[,] LargeOff={
        {2.91m,3.73m,3.99m},{3.13m,3.95m,4.21m},{3.38m,4.20m,4.46m},{3.78m,4.60m,4.86m},
        {4.22m,5.04m,5.30m},{4.60m,5.42m,5.68m},{4.75m,5.57m,5.83m},{5.00m,5.82m,6.08m},
        {5.10m,5.92m,6.18m},{5.28m,6.10m,6.36m},{5.44m,6.26m,6.52m},{5.85m,6.67m,6.93m}};
    static readonly decimal[,] LargePeak={
        {3.15m,3.97m,4.23m},{3.39m,4.21m,4.47m},{3.66m,4.48m,4.74m},{4.07m,4.89m,5.15m},
        {4.52m,5.34m,5.60m},{4.91m,5.73m,5.99m},{5.07m,5.89m,6.15m},{5.33m,6.15m,6.41m},
        {5.47m,6.29m,6.55m},{5.67m,6.49m,6.75m},{5.84m,6.66m,6.92m},{6.26m,7.08m,7.34m}};
    static decimal Pick(bool peak,int band,decimal offLow,decimal offOther,decimal peakLow,decimal peakOther){return peak?(band==0?peakLow:peakOther):(band==0?offLow:offOther);}
    public static decimal CeilingMoney(decimal value){decimal whole=decimal.Truncate(value);return whole+decimal.Ceiling((value-whole)*100m)/100m;}
    public static FbaResult Calculate(decimal kg,decimal lcm,decimal wcm,decimal hcm,decimal price,bool peak,bool fuel,decimal? specialFee){
        if(kg<=0||lcm<=0||wcm<=0||hcm<=0||price<=0||specialFee<0)throw new ArgumentOutOfRangeException();
        decimal[] sides={lcm/CmPerInch,wcm/CmPerInch,hcm/CmPerInch};Array.Sort(sides);
        decimal s=sides[0],m=sides[1],l=sides[2],girth=l+2*(m+s),actual=kg/KgPerLb;
        bool small=l<=15&&m<=12&&s<=.75m&&actual<=1;
        // Current Amazon 2026 FBA help: min width and height 2 inches for all non-small-standard dimensional calculations.
        decimal dim=small?l*m*s/139m:l*Math.Max(m,2)*Math.Max(s,2)/139m;
        decimal shipping=Math.Max(actual,dim);FbaTier tier;
        if(small)tier=FbaTier.SmallStandard;
        else if(l<=18&&m<=14&&s<=8&&shipping<=20)tier=FbaTier.LargeStandard;
        else if(l<=37&&m<=28&&s<=20&&girth<=130&&shipping<=50)tier=FbaTier.SmallBulky;
        else if(l<=59&&m<=33&&s<=33&&girth<=130&&shipping<=50)tier=FbaTier.LargeBulky;
        else tier=shipping<=50?FbaTier.Extra50:shipping<=70?FbaTier.Extra70:shipping<=150?FbaTier.Extra150:FbaTier.ExtraOver150;
        int band=price<10?0:price<=50?1:2;decimal charge,fee;
        if(tier==FbaTier.SmallStandard){charge=decimal.Ceiling(actual*8)/8;fee=(peak?SmallPeak:SmallOff)[Math.Max(0,(int)(charge*8)-1),band];}
        else if(tier==FbaTier.LargeStandard){
            charge=decimal.Ceiling(shipping*4)/4;
            if(charge<=3)fee=(peak?LargePeak:LargeOff)[Math.Max(0,(int)(charge*4)-1),band];
            else fee=(peak?new[]{6.69m,7.51m,7.77m}:new[]{6.15m,6.97m,7.23m})[band]+(charge-3)*4*.08m;
        }else{
            charge=decimal.Ceiling(tier==FbaTier.ExtraOver150?actual:shipping);
            switch(tier){
                case FbaTier.SmallBulky:fee=Pick(peak,band,6.78m,7.55m,7.82m,8.59m)+Math.Max(0,charge-1)*.38m;break;
                case FbaTier.LargeBulky:fee=Pick(peak,band,8.58m,9.35m,9.62m,10.39m)+Math.Max(0,charge-1)*.38m;break;
                case FbaTier.Extra50:fee=Pick(peak,band,25.56m,26.33m,28.29m,29.06m)+Math.Max(0,charge-1)*.38m;break;
                case FbaTier.Extra70:fee=Pick(peak,band,36.55m,37.32m,39.36m,40.13m)+Math.Max(0,charge-51)*.75m;break;
                case FbaTier.Extra150:fee=Pick(peak,band,50.55m,51.32m,54.97m,55.74m)+Math.Max(0,charge-71)*.75m;break;
                default:fee=Pick(peak,band,194.18m,194.95m,202.69m,203.46m)+Math.Max(0,charge-151)*.19m;break;
            }
        }
        bool special=tier>=FbaTier.Extra50&&tier<=FbaTier.Extra150&&(l>96||girth>130);
        decimal surcharge=fuel?fee*.035m:0,known=fee+surcharge;
        decimal specialAmount=special?CeilingMoney(specialFee.GetValueOrDefault()):0;
        return new FbaResult{Tier=tier,ActualLb=actual,DimensionalLb=dim,ChargeLb=charge,BaseFee=fee,FuelFee=surcharge,KnownFee=known,SpecialHandling=special,Total=special&&!specialFee.HasValue?(decimal?)null:CeilingMoney(known+specialAmount)};
    }
    static FbaResult Imperial(decimal pounds,decimal l,decimal w,decimal h,decimal price,bool peak=false,bool fuel=false,decimal? extra=null){return Calculate(pounds*KgPerLb,l*CmPerInch,w*CmPerInch,h*CmPerInch,price,peak,fuel,extra);}
    static void Check(bool condition,string name){if(!condition)throw new Exception("FBA: "+name);}
    public static void Verify(string path){
        var phone=Imperial(2.88m/16,13.8m,9,.7m,9.99m);Check(phone.Tier==FbaTier.SmallStandard&&phone.BaseFee==2.49m&&phone.ChargeLb==.25m,"Amazon mobile case non-peak");
        Check(Imperial(2.88m/16,13.8m,9,.7m,9.99m,true).BaseFee==2.68m,"Amazon mobile case peak");
        var iron=Imperial(3.35m,12.6m,6.6m,5.5m,20);Check(iron.Tier==FbaTier.LargeStandard&&iron.BaseFee==7.13m&&iron.ChargeLb==3.5m,"Amazon iron non-peak");
        Check(Imperial(3.35m,12.6m,6.6m,5.5m,20,true).BaseFee==7.67m,"Amazon iron peak");
        Check(Imperial(3.35m,12.6m,6.6m,5.5m,20,true,true).Total==7.94m,"Fuel 3.5 percent and upward currency rounding");
        var cot=Imperial(7.9m,24,7.5m,6,60);Check(cot.Tier==FbaTier.SmallBulky&&cot.BaseFee==10.21m,"Amazon cot non-peak");
        Check(Imperial(7.9m,24,7.5m,6,60,true).BaseFee==11.25m,"Amazon cot peak");
        Check(Imperial(.1m,10,5,1,9).BaseFee==3.38m,"Minimum width and height of 2 inches for large standard");
        for(int peak=0;peak<2;peak++)for(int band=0;band<3;band++){
            decimal price=new[]{9.99m,10m,50.01m}[band];
            for(int n=1;n<=8;n++){
                var at=Imperial(n/8m,4,3,.5m,price,peak==1);Check(at.Tier==FbaTier.SmallStandard&&at.BaseFee==(peak==1?SmallPeak:SmallOff)[n-1,band],"All small oz brackets");
                if(n<8)Check(Imperial(n/8m+.000001m,4,3,.5m,price,peak==1).BaseFee==(peak==1?SmallPeak:SmallOff)[n,band],"Small bracket just above edge");
            }
            for(int n=1;n<=12;n++)Check(Imperial(n/4m,4,3,1,price,peak==1).BaseFee==(peak==1?LargePeak:LargeOff)[n-1,band],"All large 4 oz brackets");
        }
        Check(Imperial(.1m,4,3,.5m,50).BaseFee==3.32m&&Imperial(.1m,4,3,.5m,50.01m).BaseFee==3.58m,"50 dollar inclusive boundary");
        Check(Imperial(1,15,12,.75m,10).Tier==FbaTier.SmallStandard,"Small size max inclusive");
        Check(Imperial(1,15,12,.750001m,10).Tier==FbaTier.LargeStandard,"Thickness edge");
        Check(Imperial(20,18,14,8,10).Tier==FbaTier.LargeStandard&&Imperial(20.000001m,18,14,8,10).Tier==FbaTier.SmallBulky,"20 lb boundary");
        Check(Imperial(50,37,10,2,10).Tier==FbaTier.SmallBulky&&Imperial(50,37.000001m,10,2,10).Tier==FbaTier.LargeBulky,"37 inch boundary");
        Check(Imperial(50,59,10,2,10).Tier==FbaTier.LargeBulky&&Imperial(50,59.000001m,10,2,10).Tier==FbaTier.Extra50,"59 inch boundary");
        Check(Imperial(50,60,10,2,10).Tier==FbaTier.Extra50&&Imperial(50.000001m,60,10,2,10).Tier==FbaTier.Extra70,"50 lb boundary");
        Check(Imperial(70,60,10,2,10).Tier==FbaTier.Extra70&&Imperial(70.000001m,60,10,2,10).Tier==FbaTier.Extra150,"70 lb boundary");
        Check(Imperial(150,60,10,2,10).Tier==FbaTier.Extra150&&Imperial(150.000001m,60,10,2,10).Tier==FbaTier.ExtraOver150,"150 lb boundary");
        Check(Imperial(51,60,10,2,10).BaseFee==37.32m&&Imperial(51.000001m,60,10,2,10).BaseFee==38.07m,"Extra 51 lb base boundary");
        Check(Imperial(71,60,10,2,10).BaseFee==51.32m&&Imperial(71.000001m,60,10,2,10).BaseFee==52.07m,"Extra 71 lb base boundary");
        Check(Imperial(151,60,10,2,10).BaseFee==194.95m&&Imperial(151.000001m,60,10,2,10).BaseFee==195.14m,"Extra 151 lb base boundary");
        var hugeDim=Imperial(151,100,30,30,10);Check(hugeDim.Tier==FbaTier.ExtraOver150&&hugeDim.ChargeLb==151,"150+ fee uses actual weight only");
        var special=Imperial(5,97,3,2,10);Check(special.SpecialHandling&&!special.Total.HasValue,"Unknown special fee never presented as complete");
        var supplied=Imperial(5,97,3,2,10,false,true,10);Check(supplied.Total==CeilingMoney(supplied.KnownFee+10),"Manual special handling fee");
        var fractional=Imperial(5,97,3,2,10,false,true,.001m);Check(fractional.Total==fractional.BaseFee+CeilingMoney(fractional.FuelFee)+.01m,"Currency equation uses cent-precision special fee");
        Check(!Imperial(100,60,20,15,10).SpecialHandling&&Imperial(100,60,20,15.001m,10).SpecialHandling,"Girth 130 inch boundary");
        var sample=Calculate(2.27m,20.574m,20.32m,19.05m,20,true,true,null);Check(sample.Tier==FbaTier.LargeStandard&&sample.ChargeLb==5.25m&&sample.BaseFee==8.23m&&sample.Total==8.52m,"User sample raw OCR precision");
        File.WriteAllText(path,"PASS: Amazon examples; every small/large price/season weight bracket; 10/50 USD boundaries; 2026 tier thresholds; 20/50/70/150 lb and 37/59 inch edges; min 2 inches; 51/71/151 lb bases; actual-only 150+; girth and special handling; fuel surcharge; original sample $8.52 peak including fuel.");
    }
}
}
