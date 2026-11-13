using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

internal sealed class UndoHistory
{
    public readonly UndoStack Undo,Redo;private long next,limit=128L*1024*1024;
    public UndoHistory(){Undo=new UndoStack(this);Redo=new UndoStack(this);}
    public long LimitBytes {get{return limit;}set{limit=Math.Max(1,value);Trim();}}
    public long RetainedBytes {get{return Undo.Bytes+Redo.Bytes;}}
    internal long Sequence(){return ++next;}
    internal void Trim()
    {
        // Keep the nearest single operation even if one unusually large scene
        // exceeds the estimate budget; dropping it would silently break Undo.
        while(RetainedBytes>limit&&Undo.Count+Redo.Count>1){var stack=Undo.Count==0?Redo:Redo.Count==0?Undo:Undo.Oldest<Redo.Oldest?Undo:Redo;stack.DropOldest();}
    }
}
internal sealed class UndoStack : ICollection
{
    private sealed class Item {public UndoEntry entry;public long sequence;}
    private readonly LinkedList<Item> entries=new LinkedList<Item>();private readonly UndoHistory owner;
    public UndoStack(UndoHistory owner){this.owner=owner;}
    public long Bytes {get;private set;}
    public long Oldest {get{return entries.First==null?Int64.MaxValue:entries.First.Value.sequence;}}
    public int Count {get{return entries.Count;}}
    public void Push(UndoEntry value){entries.AddLast(new Item{entry=value,sequence=owner.Sequence()});Bytes+=value.EstimatedBytes;owner.Trim();}
    public UndoEntry Peek(){if(entries.Last==null)throw new InvalidOperationException();return entries.Last.Value.entry;}
    public UndoEntry Pop(){var value=Peek();entries.RemoveLast();Bytes-=value.EstimatedBytes;return value;}
    internal void DropOldest(){Bytes-=entries.First.Value.entry.EstimatedBytes;entries.RemoveFirst();}
    public void Clear(){entries.Clear();Bytes=0;}
    public bool IsSynchronized {get{return false;}}public object SyncRoot {get{return this;}}
    public IEnumerator GetEnumerator(){return entries.Reverse().Select(item=>item.entry).GetEnumerator();}
    public void CopyTo(Array array,int index){foreach(UndoEntry entry in this)array.SetValue(entry,index++);}
}
internal static class SnapshotSize
{
    private static long Text(string value){return value==null?0:24L+value.Length*2L;}
    private static long Expressions(Dictionary<string,string> values){return values==null?0:64+values.Sum(row=>64+Text(row.Key)+Text(row.Value));}
    private static long TrackBytes(Track t)
    {
        long bytes=2048+Text(t.code)+Expressions(t.expressions)+t.keyframes.Count*160L;
        foreach(var key in t.keyframes)bytes+=Text(key.expression)+Text(key.randomId);
        foreach(var a in t.spawnActions.Concat(t.bulletActions))bytes+=1024+Text(a.expression)+Text(a.spawnColorExpression)+Expressions(a.expressions);
        bytes+=t.curvePoints.Count*40L;if(t.motionPreset!=null)bytes+=512;
        if(t.cameraPreset!=null)bytes+=2048;if(t.keyframeCycle!=null)bytes+=t.keyframeCycle.keys.Count*160L;
        return bytes+t.children.Sum(child=>TrackBytes(child));
    }
    public static long Estimate(UndoSnapshot value)
    {
        long bytes=512+(value.room==null?0:value.room.instances.Count*160L)+value.selectedKeys.Length*80L+value.selectedTracks.Length*80L;var state=value.state;if(state==null)return bytes;
        bytes+=1024+state.tracks.Sum(track=>TrackBytes(track))+state.playerTrace.Count*40L;
        if(state.nativeProject!=null)foreach(var item in state.nativeProject.events.Concat(state.nativeProject.unresolved))bytes+=256+Text(item.snippet)+Text(item.expression)+Text(item.reason);
        if(state.terrain!=null)bytes+=Text(Json.Write(state.terrain));return bytes;
    }
}
