using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace AmazonFreight {
// Horizontal screenshot pipeline using RapidOCR's PP-OCRv6 small ONNX models.
// Pre/post-processing is implemented with System.Drawing, without Python/OpenCV.
internal static class Ocr {
    static readonly object Gate=new object();
    static InferenceSession detector,recognizer;
    static string[] characters;
    static string Root {get{return AppDomain.CurrentDomain.BaseDirectory;}}
    static void EnsureLoaded() {
        if(recognizer!=null)return;
        string det=Path.Combine(Root,"models","PP-OCRv6_det_small.onnx"),rec=Path.Combine(Root,"models","PP-OCRv6_rec_small.onnx");
        if(!File.Exists(det)||!File.Exists(rec))throw new FileNotFoundException("缺少 OCR 模型。请完整解压便携包，不要仅复制 EXE。");
        using(SessionOptions options=new SessionOptions()) {
            options.IntraOpNumThreads=Math.Max(1,Math.Min(4,Environment.ProcessorCount));options.InterOpNumThreads=1;
            InferenceSession newDet=null,newRec=null;
            try{
                newDet=new InferenceSession(det,options);newRec=new InferenceSession(rec,options);
                List<string> chars=new List<string>();chars.Add("");chars.AddRange(newRec.ModelMetadata.CustomMetadataMap["character"].Split('\n'));chars.Add(" ");
                characters=chars.ToArray();detector=newDet;recognizer=newRec;
            }catch{if(newDet!=null)newDet.Dispose();if(newRec!=null)newRec.Dispose();throw;}
        }
    }
    public static string Read(Image image) {
        lock(Gate) {
            try {EnsureLoaded();}
            catch(FileNotFoundException){throw;}
            catch(Exception e){throw new Exception("OCR 运行库无法加载，请确认使用 64 位 Windows 且便携包已完整解压。",e);}
            if(image.Width>10000||image.Height>10000)throw new Exception("截图过大，请仅截取包装参数区域。");
            using(Bitmap source=new Bitmap(image.Width,image.Height,PixelFormat.Format24bppRgb)) {
                using(Graphics g=Graphics.FromImage(source)){g.Clear(Color.White);g.DrawImage(image,0,0,image.Width,image.Height);}
                List<Rectangle> boxes=Detect(source);
                List<string> lines=new List<string>();
                foreach(Rectangle box in boxes){string line=Recognize(source,box);if(!string.IsNullOrWhiteSpace(line))lines.Add(line);}
                return string.Join(Environment.NewLine,lines.ToArray());
            }
        }
    }
    static Bitmap Resize(Bitmap image,int width,int height,Rectangle area){
        Bitmap b=new Bitmap(width,height,PixelFormat.Format24bppRgb);
        using(Graphics g=Graphics.FromImage(b)){g.Clear(Color.White);g.InterpolationMode=InterpolationMode.Bilinear;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(image,new Rectangle(0,0,width,height),area,GraphicsUnit.Pixel);}
        return b;
    }
    static float[] Pixels(Bitmap image,bool detection,int targetWidth){
        int width=image.Width,height=image.Height,plane=targetWidth*height;
        float[] data=new float[3*plane];
        BitmapData bits=image.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format24bppRgb);
        try{
            byte[] row=new byte[Math.Abs(bits.Stride)];
            for(int y=0;y<height;y++){
                Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),row,0,row.Length);
                for(int x=0;x<width;x++)for(int channel=0;channel<3;channel++){
                    float f=row[x*3+channel]/255f;
                    // RapidOCR processes OpenCV BGR images; PP-OCRv6 normalizes to [-1,1].
                    data[channel*plane+y*targetWidth+x]=(f-.5f)/.5f;
                }
            }
        }finally{image.UnlockBits(bits);}
        return data;
    }
    static List<Rectangle> Detect(Bitmap source){
        double ratio=Math.Max(1.0,736.0/Math.Min(source.Width,source.Height));
        ratio=Math.Min(ratio,2048.0/Math.Max(source.Width,source.Height));
        int width=Math.Max(32,(int)Math.Round(source.Width*ratio/32)*32),height=Math.Max(32,(int)Math.Round(source.Height*ratio/32)*32);
        float[] values;
        using(Bitmap image=Resize(source,width,height,new Rectangle(0,0,source.Width,source.Height))){values=Pixels(image,true,width);}
        DenseTensor<float> input=new DenseTensor<float>(values,new int[]{1,3,height,width});
        using(var outputs=detector.Run(new[]{NamedOnnxValue.CreateFromTensor(detector.InputMetadata.Keys.First(),input)})){
            Tensor<float> result=outputs.First().AsTensor<float>();
            int[] dims=result.Dimensions.ToArray();int mh=dims[2],mw=dims[3];float[] map=result.ToArray();
            bool[] visited=new bool[mw*mh];int[] queue=new int[mw*mh];List<Rectangle> boxes=new List<Rectangle>();
            for(int seed=0;seed<map.Length;seed++){
                if(visited[seed]||map[seed]<.3f)continue;
                int head=0,tail=1;queue[0]=seed;visited[seed]=true;
                int xmin=seed%mw,xmax=xmin,ymin=seed/mw,ymax=ymin;double score=0;
                while(head<tail){
                    int p=queue[head++],x=p%mw,y=p/mw;score+=map[p];xmin=Math.Min(xmin,x);xmax=Math.Max(xmax,x);ymin=Math.Min(ymin,y);ymax=Math.Max(ymax,y);
                    if(x>0)Visit(p-1,map,visited,queue,ref tail);if(x+1<mw)Visit(p+1,map,visited,queue,ref tail);if(y>0)Visit(p-mw,map,visited,queue,ref tail);if(y+1<mh)Visit(p+mw,map,visited,queue,ref tail);
                }
                int bw=xmax-xmin+1,bh=ymax-ymin+1;
                if(bw<3||bh<3||tail<10||score/tail<.5)continue;
                double expansion=bw*bh*1.6/(2.0*(bw+bh));
                int x1=Math.Max(0,(int)Math.Floor((xmin-expansion)*source.Width/mw)),y1=Math.Max(0,(int)Math.Floor((ymin-expansion)*source.Height/mh));
                int x2=Math.Min(source.Width,(int)Math.Ceiling((xmax+1+expansion)*source.Width/mw)),y2=Math.Min(source.Height,(int)Math.Ceiling((ymax+1+expansion)*source.Height/mh));
                if(x2-x1>=3&&y2-y1>=3)boxes.Add(Rectangle.FromLTRB(x1,y1,x2,y2));
                if(boxes.Count>500)throw new Exception("截图内容过多，请仅截取包装重量和尺寸。");
            }
            boxes=boxes.OrderBy(b=>b.Top+b.Height/2).ThenBy(b=>b.Left).ToList();
            // Join same-baseline fragments so label and numeric value stay on one line.
            List<Rectangle> merged=new List<Rectangle>();
            foreach(Rectangle box in boxes){
                int index=merged.FindIndex(b=>Math.Abs((b.Top+b.Height/2)-(box.Top+box.Height/2))<Math.Min(b.Height,box.Height)*.45 && Math.Max(b.Left,box.Left)-Math.Min(b.Right,box.Right)<Math.Max(b.Height,box.Height)*3);
                if(index>=0)merged[index]=Rectangle.Union(merged[index],box);else merged.Add(box);
            }
            return merged.OrderBy(b=>b.Top+b.Height/2).ThenBy(b=>b.Left).ToList();
        }
    }
    static void Visit(int p,float[] map,bool[] visited,int[] queue,ref int tail){if(!visited[p]&&map[p]>=.3f){visited[p]=true;queue[tail++]=p;}}
    static string Recognize(Bitmap source,Rectangle box){
        const int height=48;int resizedWidth=Math.Max(1,(int)Math.Ceiling(height*(double)box.Width/box.Height));
        if(resizedWidth>6000)return "";
        int width=Math.Max(320,resizedWidth);float[] data;
        using(Bitmap image=Resize(source,resizedWidth,height,box)){data=Pixels(image,false,width);}
        DenseTensor<float> input=new DenseTensor<float>(data,new int[]{1,3,height,width});
        using(var outputs=recognizer.Run(new[]{NamedOnnxValue.CreateFromTensor(recognizer.InputMetadata.Keys.First(),input)})){
            Tensor<float> result=outputs.First().AsTensor<float>();int[] dims=result.Dimensions.ToArray();int steps=dims[1],count=dims[2];float[] scores=result.ToArray();
            if(count!=characters.Length)throw new Exception("识别模型与字符字典不匹配。");
            StringBuilder line=new StringBuilder();int previous=-1;double sum=0;int tokens=0;
            for(int t=0;t<steps;t++){
                int best=0,offset=t*count;float confidence=scores[offset];
                for(int i=1;i<count;i++)if(scores[offset+i]>confidence){confidence=scores[offset+i];best=i;}
                if(best!=0&&best!=previous){line.Append(characters[best]);sum+=confidence;tokens++;}previous=best;
            }
            return tokens>0&&sum/tokens>=.5?line.ToString():"";
        }
    }
}
}
