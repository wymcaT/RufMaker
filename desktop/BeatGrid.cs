using System;

internal static class BeatGrid
{
    // BPM describes quarter notes; an x-th note lasts four quarter notes / x.
    public static double StepSeconds(EditorState state){return 240.0/Math.Max(1,state.bpm)/Math.Max(1,state.beatDivision);}
    public static int Snap(EditorState state,int frame)
    {
        if(!state.beatSnap)return Math.Max(0,Math.Min(state.duration,frame));
        double offset=state.beatOffsetMs/1000,step=StepSeconds(state),time=frame/(double)state.fps;
        double tick=Math.Round((time-offset)/step,MidpointRounding.AwayFromZero);
        return Math.Max(0,Math.Min(state.duration,(int)Math.Round((offset+tick*step)*state.fps,MidpointRounding.AwayFromZero)));
    }
}
