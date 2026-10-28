using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed class ObjectStatisticsSnapshot
{
    public int frame,total,drawOnly;public bool nativeMissing,roomEstimate;public Dictionary<string,int> types=new Dictionary<string,int>(),sources=new Dictionary<string,int>();
    public void Type(string name,int count){if(count==0)return;int n;types.TryGetValue(name,out n);types[name]=n+count;total+=count;}
    public void Source(Track track,int count){if(count==0)return;string name=String.IsNullOrEmpty(track.name)?track.id:track.name;int n;sources.TryGetValue(name,out n);sources[name]=n+count;}
}
internal static class ObjectStatistics
{
    public static ObjectStatisticsSnapshot Capture(EditorState original,RoomData room,int frame,PreviewCache cache)
    {
        var state=cache.expandedState??original;var result=new ObjectStatisticsSnapshot{frame=frame};bool imported=state.nativeProject!=null;
        var native=imported&&cache.FilmInfo!=null?cache.FilmInfo.Counts((int)Math.Round(frame*cache.FilmInfo.fps/(double)state.fps)):null;
        if(imported){result.nativeMissing=native==null;if(native!=null)foreach(var row in native)result.Type(row.Key,row.Value);}
        else {result.roomEstimate=true;foreach(var instance in room.instances){if(instance.name.StartsWith("objRuf",StringComparison.Ordinal))continue;result.Type(instance.name=="playerStart"?"player":instance.name,1);}}
        bool layers=!imported||state.tracks.Any(t=>t.kind!="native");if(layers){result.Type("objRufController",1);result.Type("objRufTrails",1);if(!imported&&state.tracks.Any(t=>t.enabled&&(t.kind=="effect"||t.kind=="camera"))){result.Type("objRufFxBegin",1);result.Type("objRufFxEnd",1);}}
        var world=cache.bulletWorld;
        if(world!=null&&world.Frame==frame){var names=new Dictionary<string,Track>();foreach(var item in state.tracks)names[item.id]=item;foreach(var b in world.bullets.Where(b=>!b.dead)){Track current;result.Type("objRufBullet",1);result.Source(names.TryGetValue(b.source.id,out current)?current:b.source,1);}result.Type("objRufTemplate",world.Templates.InstanceCount);}
        foreach(var t in state.tracks)
        {
            bool visible=BulletSystem.Active(t,frame)&&(world==null||world.Templates.Visible(t));
            if(t.kind=="object"&&visible){result.drawOnly++;result.Source(t,1);}
            if(t.kind=="rod"&&visible&&(t.releaseFrame<0||frame<t.releaseFrame)){
                int count=Math.Max(1,Math.Min(300,BulletSystem.Round(Motion.Value(t,"count",frame))));if(world!=null)count=Enumerable.Range(0,count).Count(i=>world.Templates.Visible(t,i));result.Type("objRufBullet",count);result.Source(t,count);
            }
            if(t.kind!="emitter"||world!=null||!t.enabled||t.templateOnly)continue;
            int interval=Math.Max(1,t.interval);double life=Math.Max(1,t.life);foreach(var key in t.keyframes.Where(k=>k.property=="life"))life=Math.Max(life,key.value);
            if(t.expressions.ContainsKey("life")&&RandomExpression.IsDynamic(t.expressions["life"])||t.keyframes.Any(k=>k.property=="life"&&RandomExpression.IsDynamic(k.expression??"")))life=state.duration+1;
            int first=t.start+Math.Max(0,(int)Math.Ceiling((frame-life+1-t.start)/interval))*interval,countAll=0;
            for(int birth=first;birth<=Math.Min(frame,t.end);birth+=interval)if(frame-birth<Motion.Value(t,"life",birth))countAll+=Math.Max(1,Math.Min(300,BulletSystem.Round(Motion.Value(t,"count",birth))));
            result.Type("objRufBullet",countAll);result.Source(t,countAll);
        }
        return result;
    }
}
internal sealed class ObjectStatisticsControl : UserControl
{
    private readonly Label total=new Label{Dock=DockStyle.Top,Height=38,Padding=new Padding(8,0,0,0),TextAlign=ContentAlignment.MiddleLeft,Font=Theme.Bold,ForeColor=Theme.Accent};
    private readonly Label context=new Label{Dock=DockStyle.Top,Height=38,AutoEllipsis=true,Padding=new Padding(8,0,0,0),ForeColor=Theme.Muted};
    private readonly Label note=new Label{Dock=DockStyle.Bottom,Height=78,Padding=new Padding(8),ForeColor=Theme.Muted};
    private readonly TreeView rows=new TreeView{Dock=DockStyle.Fill,FullRowSelect=true,HideSelection=false,BackColor=Theme.Panel,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,ShowLines=false,ShowRootLines=false,Indent=14,ItemHeight=26};
    private readonly Dictionary<string,TreeNode> items=new Dictionary<string,TreeNode>();private readonly TreeNode types,sources;private int target;private bool populated;
    public ObjectStatisticsControl()
    {
        BackColor=Theme.Panel;Font=Theme.Font;types=new TreeNode{ForeColor=Theme.Accent,NodeFont=Theme.Bold};sources=new TreeNode{ForeColor=Theme.Accent,NodeFont=Theme.Bold};rows.Nodes.Add(types);rows.Nodes.Add(sources);Controls.Add(rows);Controls.Add(context);Controls.Add(total);Controls.Add(note);
        Resize+=(s,e)=>FitNote();Language.Attach(this);RefreshLanguage();SetPending(0);
    }
    public void SetPending(int frame){target=frame;if(!populated)context.Text=String.Format(Language.Text("第 {0} 帧 · 等待预览更新"),frame);}
    private void FitNote(){note.Height=Math.Max(62,note.GetPreferredSize(new Size(Math.Max(1,Width),0)).Height);}
    public void RefreshLanguage(){types.Text=Language.Text("GameMaker 对象类型");sources.Text=Language.Text("RufMaker 来源物件");}
    public void ShowStatistics(ObjectStatisticsSnapshot data)
    {
        if(data==null||data.frame!=target)return;populated=true;total.Text=String.Format(Language.Text("全局 obj 总数：{0:N0} 个"),data.total);
        context.Text=String.Format(Language.Text("第 {0} 帧 · {1}"),data.frame,Language.Text(data.nativeMissing?"仅 RufMaker 可演算部分":data.roomEstimate?"预览估算":"原生实录 + RufMaker 演算"));
        note.Text=Language.Text(data.nativeMissing?"原生底片未记录对象数量。重新生成底片后可补全原生统计。":"房间实例按编辑数据估算；原生代码数量来自底片记录。")+"\n"+String.Format(Language.Text("纯绘制物件：{0} 项；拖影不额外产生 obj。"),data.drawOnly);
        FitNote();
        rows.BeginUpdate();var present=new HashSet<string>();
        Action<Dictionary<string,int>,TreeNode,string> update=(counts,group,prefix)=>{foreach(var row in counts.OrderByDescending(r=>r.Value).ThenBy(r=>r.Key)){string key=prefix+row.Key;present.Add(key);TreeNode item;if(!items.TryGetValue(key,out item)){item=new TreeNode();items[key]=item;group.Nodes.Add(item);}string value=row.Key+"  "+String.Format(Language.Text("{0:N0} 个"),row.Value);if(item.Text!=value)item.Text=value;}group.Expand();};
        update(data.types,types,"t:");update(data.sources,sources,"s:");foreach(string key in items.Keys.Where(k=>!present.Contains(k)).ToArray()){items[key].Remove();items.Remove(key);}rows.EndUpdate();
    }
}
