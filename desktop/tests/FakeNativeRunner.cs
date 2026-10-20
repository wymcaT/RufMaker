using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

// Console-only protocol fixture. No GameMaker or Windows UI is started.
internal static class FakeNativeRunner
{
    private static int Value(Dictionary<string,int> values,string key,int fallback=0){int value;return values.TryGetValue(key,out value)?value:fallback;}
    public static void Main()
    {
        int sequence=-1,frame=0,target=0,phase=0,result=0;bool playing=false,rewind=false;var random=new Random(1234567);
        Action step=()=>{frame++;random.Next();if(frame==20)phase=random.Next(100000);if(frame==80)result=phase+random.Next(100000);};
        for(;;){
            try{
                var values=new Dictionary<string,int>();foreach(string line in File.ReadAllLines("command.ini")){int at=line.IndexOf('='),number;if(at>0&&Int32.TryParse(line.Substring(at+1),out number))values[line.Substring(0,at)]=number;}
                if(Value(values,"serial")!=sequence){
                    sequence=Value(values,"serial");bool seek=Value(values,"seek",1)!=0;
                    if(seek)target=Value(values,"frame");else if(frame>=target)target=frame;
                    playing=Value(values,"playing")!=0;rewind=seek&&target<frame;if(rewind)playing=false;
                }
                if(!rewind){while(frame<target)step();if(playing)step();}
                File.WriteAllText("snapshot.txt",frame+","+phase+","+result);
                File.WriteAllText("status.ini","[native]\nsequence="+sequence.ToString("F6",CultureInfo.InvariantCulture)+"\nframe="+frame+"\ntarget="+target+"\nplaying="+(playing?1:0)+"\nrewind="+(rewind?1:0)+"\ntest=0\nroom=fixture\n");
            }catch(IOException){}
            Thread.Sleep(10);
        }
    }
}
