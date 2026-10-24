using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

// Decode with the same FMOD runtime as the editor/game. Analysis never plays audio.
internal static class AudioAnalyzer
{
    const string Dll="runtime\\fmodex.dll";
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int FMOD_System_Create(out IntPtr system);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int FMOD_System_SetOutput(IntPtr system,int output);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int FMOD_System_Init(IntPtr system,int channels,uint flags,IntPtr extra);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int FMOD_System_CreateSound(IntPtr system,byte[] file,uint mode,IntPtr info,out IntPtr sound);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int FMOD_System_Release(IntPtr system);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int FMOD_Sound_Release(IntPtr sound);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int FMOD_Sound_GetLength(IntPtr sound,out uint length,uint unit);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int FMOD_Sound_GetFormat(IntPtr sound,out int type,out int format,out int channels,out int bits);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int FMOD_Sound_GetDefaults(IntPtr sound,out float frequency,out float volume,out float pan,out int priority);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int FMOD_Sound_ReadData(IntPtr sound,byte[] buffer,uint length,out uint read);
    static void Check(int error){if(error!=0)throw new InvalidOperationException("歌曲解析失败（FMOD "+error+"）");}
    internal static double Sample(byte[] data,int p,int format)
    {
        switch(format){case 1:return (data[p]-128)/128.0;case 2:return (short)(data[p]|data[p+1]<<8)/32768.0;
        case 3:int v=data[p]|data[p+1]<<8|data[p+2]<<16;if((v&0x800000)!=0)v|=unchecked((int)0xff000000);return v/8388608.0;
        case 4:return BitConverter.ToInt32(data,p)/2147483648.0;case 5:return BitConverter.ToSingle(data,p);default:throw new InvalidOperationException("音乐解码未返回 PCM 数据");}
    }
    internal static void Decode(string file,Action<AudioPcmFormat> begin,Action<AudioPcmFormat,byte[],int> consume,System.Threading.CancellationToken cancel=default(System.Threading.CancellationToken))
    {
        if(!File.Exists(file))throw new FileNotFoundException("找不到歌曲",file);IntPtr system=IntPtr.Zero,sound=IntPtr.Zero;
        try{
            Check(FMOD_System_Create(out system));Check(FMOD_System_SetOutput(system,2));Check(FMOD_System_Init(system,8,0,IntPtr.Zero));
            // FMOD Ex 4.44 uses UTF-16 with FMOD_UNICODE.
            Check(FMOD_System_CreateSound(system,System.Text.Encoding.Unicode.GetBytes(Path.GetFullPath(file)+"\0"),0x01000000|0x1|0x8|0x40|0x80|0x2000|0x4000,IntPtr.Zero,out sound));
            uint ms;int type,format,channels,bits,priority;float frequency,volume,pan;
            Check(FMOD_Sound_GetLength(sound,out ms,1));Check(FMOD_Sound_GetFormat(sound,out type,out format,out channels,out bits));Check(FMOD_Sound_GetDefaults(sound,out frequency,out volume,out pan,out priority));
            if(channels<1||frequency<=40||bits<8||format<1||format>5||ms==0)throw new InvalidOperationException("不支持这首歌曲的解码格式");
            var info=new AudioPcmFormat{channels=channels,bits=bits,format=format,rate=frequency,duration=ms/1000.0};begin(info);var buffer=new byte[(65536/info.FrameBytes)*info.FrameBytes];long frames=0;
            while(true){cancel.ThrowIfCancellationRequested();uint received;int error=FMOD_Sound_ReadData(sound,buffer,(uint)buffer.Length,out received);int count=(int)received/info.FrameBytes;if(count>0){consume(info,buffer,count);frames+=count;}if(received==0||error!=0)break;}
            if(frames==0)throw new InvalidOperationException("无法读取歌曲的音频数据");
        }finally{if(sound!=IntPtr.Zero)FMOD_Sound_Release(sound);if(system!=IntPtr.Zero)FMOD_System_Release(system);}
    }
    public static WaveData Analyze(string file)
    {
        WaveData result=null;List<float> samples=null;long frame=0;double sum=0,next=0;int sumCount=0;
        Decode(file,info=>{result=new WaveData{duration=info.duration,peaks=new double[Math.Min(24000,Math.Max(2000,(int)(info.duration*100)))]};samples=new List<float>((int)(Math.Min(300,info.duration)*11025)+1024);next=info.rate/11025.0;},(info,buffer,count)=>{
            int bytes=info.bits/8;
            for(int i=0;i<count;i++,frame++){
                double mono=0,peak=0;for(int c=0;c<info.channels;c++){double value=Sample(buffer,i*info.FrameBytes+c*bytes,info.format);mono+=value;peak=Math.Max(peak,Math.Abs(value));}mono/=info.channels;
                int bin=Math.Min(result.peaks.Length-1,(int)(frame/info.rate/info.duration*result.peaks.Length));result.peaks[bin]=Math.Max(result.peaks[bin],Math.Min(1,peak));
                if(frame/info.rate<300){sum+=mono;sumCount++;if(frame+1>=next){samples.Add((float)(sum/Math.Max(1,sumCount)));sum=0;sumCount=0;next+=info.rate/11025.0;}}
            }
        });Detect(samples.ToArray(),11025,result);return result;
    }
    internal static void Fft(double[] real,double[] imag)
    {
        int n=real.Length;for(int i=1,j=0;i<n;i++){int bit=n>>1;for(;(j&bit)!=0;bit>>=1)j^=bit;j^=bit;if(i<j){double t=real[i];real[i]=real[j];real[j]=t;}}
        for(int len=2;len<=n;len*=2){double a=-2*Math.PI/len,wr0=Math.Cos(a),wi0=Math.Sin(a);for(int start=0;start<n;start+=len){double wr=1,wi=0;for(int j=0;j<len/2;j++){int p=start+j,q=p+len/2;double r=real[q]*wr-imag[q]*wi,im=real[q]*wi+imag[q]*wr;real[q]=real[p]-r;imag[q]=imag[p]-im;real[p]+=r;imag[p]+=im;double next=wr*wr0-wi*wi0;wi=wr*wi0+wi*wr0;wr=next;}}}
    }
    static double Correlation(double[] onset,double lag)
    {int step=(int)lag;double f=lag-step,a=0,b=0,c=0;for(int i=step+1;i<onset.Length;i++){double x=onset[i],y=onset[i-step]*(1-f)+onset[i-step-1]*f;a+=x*y;b+=x*x;c+=y*y;}return a/Math.Max(1e-12,Math.Sqrt(b*c));}
    internal static void Detect(float[] samples,int rate,WaveData result)
    {
        const int size=512,hop=128;int count=(samples.Length-size)/hop;if(count<40)return;
        var flux=new double[count];var real=new double[size];var imag=new double[size];var previous=new double[size/2];
        for(int frame=0;frame<count;frame++)
        {Array.Clear(imag,0,size);for(int j=0;j<size;j++)real[j]=samples[frame*hop+j]*(.5-.5*Math.Cos(2*Math.PI*j/(size-1)));Fft(real,imag);double value=0;for(int bin=2;bin<size/2;bin++){double magnitude=Math.Log(1+Math.Sqrt(real[bin]*real[bin]+imag[bin]*imag[bin])*10);value+=Math.Max(0,magnitude-previous[bin])/Math.Sqrt(bin);previous[bin]=magnitude;}flux[frame]=value;}
        var onset=new double[count];double energy=0;
        for(int i=0;i<count;i++){double local=0;int n=0;for(int j=Math.Max(0,i-12);j<=Math.Min(count-1,i+12);j++){local+=flux[j];n++;}onset[i]=Math.Max(0,flux[i]-local/n);energy+=onset[i]*onset[i];}
        if(energy<1e-5)return;double hz=rate/(double)hop,best=-1,bpm=0,raw=0;
        for(double candidate=60;candidate<=220;candidate+=.2)
        {double lag=60/candidate*hz,c=Correlation(onset,lag),score=c+.35*Correlation(onset,lag*2)+.15*Correlation(onset,lag*3);score*=.9+.1*Math.Exp(-Math.Pow((candidate-125)/65,2));if(score>best){best=score;bpm=candidate;raw=c;}}
        // Refine tempo using phase coherence across the song, so rounding does not accumulate drift.
        double coherent=-1,refined=bpm;for(double candidate=Math.Max(60,bpm-1);candidate<=Math.Min(220,bpm+1);candidate+=.01)
        {double x=0,y=0,w=2*Math.PI*candidate/60;for(int i=0;i<count;i++){double angle=(i*hop+size/2.0)/rate*w;x+=onset[i]*Math.Cos(angle);y+=onset[i]*Math.Sin(angle);}double strength=x*x+y*y;if(strength>coherent){coherent=strength;refined=candidate;}}
        bpm=refined;raw=Correlation(onset,60/bpm*hz);double period=60/bpm,bestPhase=0,phaseScore=-1;
        for(double phase=0;phase<period;phase+=.002)
        {double score=0;for(int i=0;i<count;i++){double time=(i*hop+size/2.0)/rate,delta=(time-phase)/period;delta=(delta-Math.Round(delta))*period;score+=onset[i]*Math.Exp(-delta*delta/(2*.02*.02));}if(score>phaseScore){phaseScore=score;bestPhase=phase;}}
        result.bpm=Math.Round(bpm,2);result.offsetMs=Math.Round(bestPhase*1000);result.confidence=Math.Max(0,Math.Min(1,raw));
    }
}
