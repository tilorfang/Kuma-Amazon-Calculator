using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AmazonFreightSingleExe {
    internal sealed class Entry {
        public string Name,Hash;
        public long Length;
    }
    internal static class Program {
        static readonly Assembly Self=Assembly.GetExecutingAssembly();
        static string Hash(Stream stream){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        static string Quote(string value){
            StringBuilder text=new StringBuilder("\"");int slashes=0;
            foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='\"'){text.Append('\\',slashes*2+1);text.Append(c);}else{text.Append('\\',slashes);text.Append(c);}slashes=0;}
            text.Append('\\',slashes*2);text.Append('"');return text.ToString();
        }
        static List<Entry> ReadManifest(){
            List<Entry> entries=new List<Entry>();
            using(var stream=Self.GetManifestResourceStream("payload.manifest"))using(var reader=new StreamReader(stream,Encoding.UTF8)){
                string line;
                while((line=reader.ReadLine())!=null){if(line.Length==0)continue;string[] fields=line.Split('\t');if(fields.Length!=3)throw new InvalidDataException("内置文件清单损坏。");entries.Add(new Entry{Name=fields[2],Hash=fields[0],Length=long.Parse(fields[1])});}
            }
            return entries;
        }
        static string SafeFile(string root,string name){
            if(Path.IsPathRooted(name))throw new InvalidDataException("无效的内置文件路径。");
            string path=Path.GetFullPath(Path.Combine(root,name.Replace('/',Path.DirectorySeparatorChar)));
            if(!path.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("无效的内置文件路径。");
            return path;
        }
        static bool Valid(string folder,List<Entry> entries){
            if(!Directory.Exists(folder))return false;
            try{foreach(var e in entries){string path=SafeFile(folder,e.Name);if(!File.Exists(path)||new FileInfo(path).Length!=e.Length)return false;using(var f=File.OpenRead(path))if(Hash(f)!=e.Hash)return false;}return true;}
            catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
        }
        static string Extract(){
            string id;using(var manifest=Self.GetManifestResourceStream("payload.manifest"))id=Hash(manifest);
            string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KumaAmazonCalculator","runtime");
            Directory.CreateDirectory(root);
            string target=Path.Combine(root,id);
            var entries=ReadManifest();
            // The lock serializes simultaneous first launches in this user's cache.
            FileStream gate=null;var timer=Stopwatch.StartNew();
            while(gate==null){try{gate=new FileStream(Path.Combine(root,"extract.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){if(timer.Elapsed.TotalSeconds>60)throw new IOException("另一个启动进程仍在准备运行库，请稍后重试。");Thread.Sleep(100);}}
            using(gate){
                if(Valid(target,entries))return target;
                string staging=Path.Combine(root,id+".tmp-"+Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staging);
                try{
                    using(var stream=Self.GetManifestResourceStream("payload.zip"))using(var archive=new ZipArchive(stream,ZipArchiveMode.Read)){
                        foreach(var e in archive.Entries){
                            if(e.FullName.EndsWith("/"))continue;
                            string output=SafeFile(staging,e.FullName);Directory.CreateDirectory(Path.GetDirectoryName(output));
                            using(var input=e.Open())using(var file=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None))input.CopyTo(file);
                        }
                    }
                    if(!Valid(staging,entries))throw new InvalidDataException("内置运行库校验失败，请重新复制完整 EXE。");
                    // Keep a damaged old cache separate; mapped DLLs may belong to a running instance.
                    if(Directory.Exists(target))Directory.Move(target,Path.Combine(root,id+".old-"+Guid.NewGuid().ToString("N")));
                    Directory.Move(staging,target);
                    return target;
                }finally{
                    if(Directory.Exists(staging)){
                        string resolved=Path.GetFullPath(staging);
                        if(string.Equals(Path.GetDirectoryName(resolved),Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(resolved).StartsWith(id+".tmp-",StringComparison.Ordinal))try{Directory.Delete(resolved,true);}catch(IOException){}catch(UnauthorizedAccessException){}
                    }
                }
            }
        }
        [STAThread]static int Main(string[] args){
            try{
                string folder=Extract();
                string[] quoted=new string[args.Length];for(int i=0;i<args.Length;i++)quoted[i]=Quote(args[i]);
                var info=new ProcessStartInfo(Path.Combine(folder,"Kuma亚马逊运费计算器.exe"),string.Join(" ",quoted));
                info.UseShellExecute=false;info.WorkingDirectory=Path.GetDirectoryName(Self.Location);
                using(var child=Process.Start(info)){child.WaitForExit();return child.ExitCode;}
            }catch(Exception e){
                Application.EnableVisualStyles();
                MessageBox.Show("无法启动计算器：\r\n"+e.Message+"\r\n\r\n请确认使用 Windows 10/11 64 位，且当前账户的本地缓存目录可写。","Kuma亚马逊运费计算器",MessageBoxButtons.OK,MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
