using System;
using System.Diagnostics;
using System.Globalization;

internal sealed class PreviewTiming
{
    public double Scene,Film,Simulation,Trails,Bodies,Effects,Statistics,Total;
    public double Gather,Submit,Backdrop,Crop,Fused;
    public int Readbacks;
    public int Sprites,GpuSprites,GpuEffects;
    public string Adapter;
    internal static long Now(){return Stopwatch.GetTimestamp();}
    internal static double Since(long start){return (Now()-start)*1000.0/Stopwatch.Frequency;}
    public string Caption()
    {
        return String.Format(CultureInfo.CurrentCulture,Language.Text("预览 {0:0.0} ms · 演算 {1:0.0} · 拖影 {2:0.0} · 本体 {3:0.0} · 独立特效 {4:0.0}\n底片 {5:0.0} · 场景 {6:0.0} · 统计 {7:0.0} · {8}\nGPU 合成（含同批特效）{9:0.0} ms · 回读 {10} 次"),
            Total,Simulation,Trails,Bodies,Effects,Film,Scene,Statistics,GpuSprites+GpuEffects>0?"GPU · "+Adapter:"CPU",Fused,Readbacks);
    }
}
