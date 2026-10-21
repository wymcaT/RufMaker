using System;
using System.IO;
internal static class UpdateRestartStub
{
    [STAThread] private static void Main(){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"restart-confirmed.txt"),"updated application started");}
}
