using System;
using System.IO;
using System.Runtime.InteropServices;

// Uses the same FMOD Ex runtime supplied with the avoidance engine. All positions are milliseconds.
internal sealed class NativeAudio : IDisposable
{
    private const string Dll="runtime\\fmodex.dll";
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] private static extern int FMOD_System_Create(out IntPtr system);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] private static extern int FMOD_System_GetVersion(IntPtr system,out uint version);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] private static extern int FMOD_System_Init(IntPtr system,int channels,uint flags,IntPtr data);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] private static extern int FMOD_System_CreateSound(IntPtr system,byte[] file,uint mode,IntPtr info,out IntPtr sound);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] private static extern int FMOD_System_PlaySound(IntPtr system,int index,IntPtr sound,int paused,out IntPtr channel);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] private static extern int FMOD_System_Update(IntPtr system);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] private static extern int FMOD_System_Release(IntPtr system);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] private static extern int FMOD_Sound_Release(IntPtr sound);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] private static extern int FMOD_Sound_GetLength(IntPtr sound,out uint length,uint unit);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] private static extern int FMOD_Channel_SetPosition(IntPtr channel,uint position,uint unit);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] private static extern int FMOD_Channel_GetPosition(IntPtr channel,out uint position,uint unit);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] private static extern int FMOD_Channel_SetPaused(IntPtr channel,int paused);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] private static extern int FMOD_Channel_SetVolume(IntPtr channel,float volume);
    [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] private static extern int FMOD_Channel_Stop(IntPtr channel);
    private IntPtr system,sound,channel;
    public uint Version {get;private set;}
    public double Duration {get;private set;}
    public bool Playing {get;private set;}
    public float Volume=.65f;
    private static void Check(int error){if(error!=0)throw new InvalidOperationException("音乐播放失败（FMOD "+error+"）");}
    public void Load(string file)
    {
        Dispose();if(!File.Exists(file))throw new FileNotFoundException("找不到工程音乐",file);
        Check(FMOD_System_Create(out system));uint version;Check(FMOD_System_GetVersion(system,out version));Version=version;
        Check(FMOD_System_Init(system,16,0,IntPtr.Zero));
        Check(FMOD_System_CreateSound(system,System.Text.Encoding.Unicode.GetBytes(Path.GetFullPath(file)+"\0"),0x01000000|0x00000001|0x00000008|0x00000080,IntPtr.Zero,out sound));
        uint length;Check(FMOD_Sound_GetLength(sound,out length,1));Duration=length/1000.0;
    }
    public void Play(double seconds)
    {
        Pause();if(sound==IntPtr.Zero)throw new InvalidOperationException("音乐尚未载入");if(seconds>=Duration)return;
        if(channel!=IntPtr.Zero)FMOD_Channel_Stop(channel);
        Check(FMOD_System_PlaySound(system,-1,sound,1,out channel));
        Check(FMOD_Channel_SetPosition(channel,(uint)Math.Max(0,seconds*1000),1));
        Check(FMOD_Channel_SetVolume(channel,Volume));Check(FMOD_Channel_SetPaused(channel,0));Playing=true;Update();
    }
    public void SetVolume(float volume){Volume=volume;if(channel!=IntPtr.Zero)FMOD_Channel_SetVolume(channel,volume);}
    public void Pause(){if(channel!=IntPtr.Zero)FMOD_Channel_SetPaused(channel,1);Playing=false;}
    public double Position {get{uint value;return channel!=IntPtr.Zero&&FMOD_Channel_GetPosition(channel,out value,1)==0?value/1000.0:-1;}}
    public void Update(){if(system!=IntPtr.Zero)FMOD_System_Update(system);}
    public void Dispose(){Playing=false;if(channel!=IntPtr.Zero){FMOD_Channel_Stop(channel);channel=IntPtr.Zero;}if(sound!=IntPtr.Zero){FMOD_Sound_Release(sound);sound=IntPtr.Zero;}if(system!=IntPtr.Zero){FMOD_System_Release(system);system=IntPtr.Zero;}}
}
