using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

internal sealed class AudioPcmFormat
{
    public int channels,bits,format;public double rate,duration;
    public int FrameBytes {get{return channels*(bits/8);}}
}
internal sealed class SpectrogramData
{
    public int columns,bands,fftSize,hop;public double rate,duration,minFrequency=20,maxFrequency;
    public byte[] levels;
    public double Frequency(double row){return minFrequency*Math.Pow(maxFrequency/minFrequency,row/bands);}
    public double Time(int column){return column*hop/rate;}
}
internal static class AudioSpectrogram
{
    public const int Version=1,MaxColumns=24000,Bands=192;
    public static string SourceKey(string file)
    {var info=new FileInfo(file);using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(info.FullName.ToUpperInvariant()+"|"+info.Length+"|"+info.LastWriteTimeUtc.Ticks+"|"+Version))).Replace("-","").Substring(0,24);}
    public static SpectrogramData Load(string file,string cacheDirectory,CancellationToken cancel,IProgress<int> progress=null)
    {
        cancel.ThrowIfCancellationRequested();
        string target=Path.Combine(cacheDirectory,"spectrum-"+SourceKey(file)+".rufspec");
        if(File.Exists(target)){try{return Read(target);}catch(IOException){}catch(InvalidDataException){}}
        Builder builder=null;long frames=0;var clock=System.Diagnostics.Stopwatch.StartNew();
        AudioAnalyzer.Decode(file,format=>builder=new Builder(format.rate,format.channels,format.duration),(format,buffer,count)=>{
            cancel.ThrowIfCancellationRequested();int bytes=format.bits/8;
            for(int i=0;i<count;i++){for(int c=0;c<format.channels;c++)builder.AddChannel(c,AudioAnalyzer.Sample(buffer,i*format.FrameBytes+c*bytes,format.format));builder.EndFrame();}frames+=count;
            if(progress!=null&&clock.ElapsedMilliseconds>150){progress.Report((int)Math.Min(99,frames/format.rate/format.duration*100));clock.Restart();}
        },cancel);
        var data=builder.Finish();cancel.ThrowIfCancellationRequested();Directory.CreateDirectory(cacheDirectory);string temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{Write(temporary,data);if(File.Exists(target))File.Replace(temporary,target,null);else File.Move(temporary,target);}finally{if(File.Exists(temporary))File.Delete(temporary);}
        if(progress!=null)progress.Report(100);return data;
    }
    internal static void Write(string file,SpectrogramData data)
    {using(var writer=new BinaryWriter(File.Create(file))){writer.Write(Encoding.ASCII.GetBytes("RUFSPC01"));writer.Write(data.columns);writer.Write(data.bands);writer.Write(data.fftSize);writer.Write(data.hop);writer.Write(data.rate);writer.Write(data.duration);writer.Write(data.minFrequency);writer.Write(data.maxFrequency);writer.Write(data.levels);}}
    internal static SpectrogramData Read(string file)
    {
        using(var reader=new BinaryReader(File.OpenRead(file))){if(Encoding.ASCII.GetString(reader.ReadBytes(8))!="RUFSPC01")throw new InvalidDataException("频谱缓存版本无效");
            var d=new SpectrogramData{columns=reader.ReadInt32(),bands=reader.ReadInt32(),fftSize=reader.ReadInt32(),hop=reader.ReadInt32(),rate=reader.ReadDouble(),duration=reader.ReadDouble(),minFrequency=reader.ReadDouble(),maxFrequency=reader.ReadDouble()};
            if(d.columns<1||d.columns>MaxColumns||d.bands!=Bands||d.hop<1||d.rate<=0||d.duration<=0||d.maxFrequency<=d.minFrequency)throw new InvalidDataException("频谱缓存格式无效");d.levels=reader.ReadBytes(d.columns*d.bands);if(d.levels.Length!=d.columns*d.bands)throw new InvalidDataException("频谱缓存不完整");return d;}
    }
    internal sealed class Builder
    {
        private readonly double rate;private readonly int channels,size,hop,capacity;private readonly double[][] ring;private readonly double[] real,imag,power,window;private readonly byte[] levels;
        private long samples,realFrames,next;private int write,column;private readonly double normalization;
        public Builder(double rate,int channels,double duration)
        {
            this.rate=rate;this.channels=channels;size=512;while(size<rate/12&&size<8192)size*=2;
            hop=Math.Max(1,(int)Math.Ceiling(rate*Math.Max(.02,duration/(MaxColumns-1))));capacity=Math.Min(MaxColumns,Math.Max(1,(int)Math.Ceiling(duration*rate/hop)+1));
            ring=new double[channels][];for(int c=0;c<channels;c++)ring[c]=new double[size];real=new double[size];imag=new double[size];power=new double[size/2+1];window=new double[size];
            double sum=0;for(int i=0;i<size;i++){window[i]=.5-.5*Math.Cos(2*Math.PI*i/(size-1));sum+=window[i];}normalization=4/(sum*sum*channels);levels=new byte[capacity*Bands];
            samples=size/2;write=size/2;next=size;
        }
        public void AddChannel(int channel,double value){ring[channel][write]=value;}
        public void EndFrame(){realFrames++;Advance();}
        private void Advance(){write=(write+1)%size;samples++;if(samples>=next){if(column<capacity)Transform();next+=hop;}}
        private void Transform()
        {
            Array.Clear(power,0,power.Length);
            for(int c=0;c<channels;c++){Array.Clear(imag,0,size);for(int i=0;i<size;i++)real[i]=ring[c][(write+i)%size]*window[i];AudioAnalyzer.Fft(real,imag);for(int i=0;i<power.Length;i++)power[i]+=(real[i]*real[i]+imag[i]*imag[i])*normalization;}
            double min=20,max=rate/2,step=rate/size;
            for(int row=0;row<Bands;row++){
                double lo=min*Math.Pow(max/min,row/(double)Bands),hi=min*Math.Pow(max/min,(row+1)/(double)Bands),value=0;
                if(hi-lo<step){double f=Math.Sqrt(lo*hi)/step;int k=Math.Min(power.Length-2,(int)f);double u=f-k;value=power[k]*(1-u)+power[k+1]*u;}
                else for(int k=Math.Max(1,(int)Math.Ceiling(lo/step));k<Math.Min(power.Length,(int)Math.Ceiling(hi/step));k++)value=Math.Max(value,power[k]);
                double db=10*Math.Log10(Math.Max(1e-16,value));levels[column*Bands+row]=(byte)Math.Max(0,Math.Min(255,(db+90)/90*255));
            }column++;
        }
        public SpectrogramData Finish()
        {
            if(realFrames==0)throw new InvalidDataException("无法读取歌曲的音频数据");int count=Math.Min(capacity,Math.Max(1,(int)Math.Ceiling(realFrames/(double)hop)));
            while(column<count){for(int c=0;c<channels;c++)ring[c][write]=0;Advance();}
            var bytes=new byte[count*Bands];Array.Copy(levels,bytes,bytes.Length);return new SpectrogramData{columns=count,bands=Bands,fftSize=size,hop=hop,rate=rate,duration=realFrames/rate,maxFrequency=rate/2,levels=bytes};
        }
    }
}
