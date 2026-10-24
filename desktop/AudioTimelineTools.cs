using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

internal sealed partial class MainForm
{
    private CancellationTokenSource spectrumCancel;private string spectrumSource="";private SpectrogramData loadedSpectrum;private bool spectrumLoading;
    private async void EnsureSpectrum()
    {
        if(state==null||document==null||timeline.AudioDisplay!="spectrogram")return;
        try
        {
            string file=String.IsNullOrEmpty(state.music)?"":Path.Combine(document.project,state.music.Replace('/',Path.DirectorySeparatorChar));
            if(!File.Exists(file)){DisposeAudioAnalysis();loadedSpectrum=null;spectrumSource="";timeline.SpectrumStatus="当前工程没有可分析的歌曲";timeline.Invalidate();return;}
            string key=AudioSpectrogram.SourceKey(file);if(spectrumSource==key){if(loadedSpectrum!=null)timeline.Spectrum=loadedSpectrum;if(spectrumLoading||loadedSpectrum!=null)return;}
            DisposeAudioAnalysis();spectrumSource=key;loadedSpectrum=null;spectrumLoading=true;timeline.Spectrum=null;timeline.SpectrumStatus="正在生成频谱…";timeline.Invalidate();
            var cancel=spectrumCancel=new CancellationTokenSource();var token=cancel.Token;string directory=Path.Combine(document.project,"cache","audio-spectrum");
            var progress=new Progress<int>(value=>{if(!IsDisposed&&spectrumSource==key){timeline.SpectrumStatus=Language.Text("正在生成频谱…")+" "+value+"%";timeline.Invalidate();}});
            try{var data=await Task.Run(()=>AudioSpectrogram.Load(file,directory,token,progress));if(!IsDisposed&&spectrumSource==key&&!token.IsCancellationRequested){loadedSpectrum=data;timeline.SpectrumStatus="";timeline.Spectrum=data;}}
            catch(OperationCanceledException){}
            finally{if(spectrumSource==key)spectrumLoading=false;}
        }
        catch(Exception error){if(!IsDisposed){timeline.SpectrumStatus=Language.Text("频谱分析失败：")+error.Message;timeline.Invalidate();}}
    }
    private void DisposeAudioAnalysis(){if(spectrumCancel!=null){spectrumCancel.Cancel();spectrumCancel.Dispose();spectrumCancel=null;}}
}
