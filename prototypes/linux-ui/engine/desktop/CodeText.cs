internal static class CodeText
{
    private static string Lines(string text){return (text??"").Replace("\r\n","\n").Replace("\r","\n");}
    public static string NewlineOf(string text){return text!=null&&text.Contains("\r\n")?"\r\n":"\n";}
    // The native Windows multiline EDIT control requires CRLF to show line breaks.
    public static string ForDisplay(string text){return Lines(text).Replace("\n","\r\n");}
    public static string ForSource(string text,string newline){return Lines(text).Replace("\n",newline);}
    public static int LineOffset(string text,int line)
    {
        int at=0;for(int current=1;current<line;current++){int next=text.IndexOf('\n',at);if(next<0)return text.Length;at=next+1;}return at;
    }
}
