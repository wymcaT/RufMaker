using System;
using System.IO;

internal static class CodeTextTests
{
    private static void Check(string source)
    {
        string display=CodeText.ForDisplay(source),bare=display.Replace("\r\n","");
        if(bare.Contains("\r")||bare.Contains("\n"))throw new Exception("Windows display text contains a bare line break.");
        if(CodeText.ForSource(display,CodeText.NewlineOf(source))!=source)throw new Exception("Opening and saving changed the source text.");
        if(CodeText.ForDisplay(display)!=display)throw new Exception("Display conversion doubled carriage returns.");
    }
    private static int Main(string[] args)
    {
        try{
            const string block="    with(obj_wymdan){\n    if(kind=4){\n    alarm[0]=(y/30)+1\n    }\n    }\n    break\n";
            Check(block);Check(block.Replace("\n","\r\n"));Check("");Check("x=1;");
            string display=CodeText.ForDisplay(block);int third=CodeText.LineOffset(display,3);
            if(!display.Substring(third).StartsWith("    alarm[0]"))throw new Exception("Source line navigation changed with display wrapping.");
            if(CodeText.ForSource(CodeText.ForDisplay("a\r\nb\nc\rd"),"\n")!="a\nb\nc\nd")throw new Exception("Mixed pasted line breaks were not normalized.");
            if(args.Length>0&&File.Exists(args[0]))Check(File.ReadAllText(args[0]));
            Console.WriteLine("Code newline display and source round-trip checks passed.");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
