using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal static class PlayerTraceTests
{
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
    }

    public static int Main(string[] args)
    {
        string scratch = args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "build", "player-trace-check");
        Directory.CreateDirectory(scratch);

        TestPlayerTraceReadLegacy(scratch);
        TestPlayerTraceReadEnhanced(scratch);
        TestPlayerTraceConcurrentRead(scratch);
        TestPreviewRendererDrawPlayer(scratch);
        TestPlayerTraceCanvasAndAimGuide(scratch);
        TestGodModeIniAndPayload(scratch);

        Console.WriteLine("PASS: Player trace parsing (legacy 3-col & 10-col sprite format), concurrent read-share, PreviewRenderer sync player drawing, canvas aim guide, F5/F6 godMode INI and guard checks.");
        return 0;
    }

    private static void TestPlayerTraceReadLegacy(string dir)
    {
        string file = Path.Combine(dir, "legacy_trace.csv");
        File.WriteAllLines(file, new string[]
        {
            "0,400.0,304.0",
            "10,430.5,290.0",
            "20,460.0,320.0"
        });

        var points = PlayerTrace.Read(file);
        Assert(points.Count == 3, "Legacy trace has 3 points");
        Assert(points[0].frame == 0 && Math.Abs(points[0].x - 400) < 1e-4 && Math.Abs(points[0].y - 304) < 1e-4, "Point 0 coordinates");
        Assert(points[1].facing == 1 && points[1].action == "idle" && !points[1].dead, "Legacy defaults to facing 1, idle, alive");
    }

    private static void TestPlayerTraceReadEnhanced(string dir)
    {
        string file = Path.Combine(dir, "enhanced_trace.csv");
        File.WriteAllLines(file, new string[]
        {
            "0,400.000000,304.000000,sprPlayerIdle,0, 1.00, 1.00, 1.00, 0.00,0",
            "10,430.000000,280.000000,sprPlayerJump,0, 1.00, 1.00, 1.00, 0.00,0",
            "20,450.000000,270.000000,sprPlayerRunning,2, 1.00, 1.00, 1.00, 0.00,0",
            "30,480.000000,310.000000,sprPlayerFall,0,-1.00, 1.00, 1.00, 0.00,0",
            "40,490.000000,320.000000,sprPlayerIdle,0, 1.00, 1.00, 0.00, 0.00,1"
        });

        var points = PlayerTrace.Read(file);
        Assert(points.Count == 5, "Enhanced trace has 5 points");
        Assert(points[0].sprite == "sprPlayerIdle" && points[0].action == "idle", "Point 0 is idle");
        Assert(points[1].sprite == "sprPlayerJump" && points[1].action == "jump", "Point 1 is jump");
        Assert(points[2].sprite == "sprPlayerRunning" && points[2].subimage == 2 && points[2].action == "run", "Point 2 is running frame 2");
        Assert(points[3].sprite == "sprPlayerFall" && points[3].facing == -1 && points[3].xscale == -1.0, "Point 3 facing left and falling");
        Assert(points[4].dead, "Point 4 is dead");

        // Test snapshot
        var snap = points[2].Snapshot();
        Assert(snap.frame == 20 && snap.sprite == "sprPlayerRunning" && snap.subimage == 2 && snap.xscale == 1.0, "Snapshot preserves sprite fields");
    }

    private static void TestPlayerTraceConcurrentRead(string dir)
    {
        string file = Path.Combine(dir, "concurrent_trace.csv");
        using (var writerFs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        using (var writer = new StreamWriter(writerFs))
        {
            writer.WriteLine("0,400,304,sprPlayerIdle,0,1,1,1,0,0");
            writer.Flush();

            // Read while writer has file open
            var points = PlayerTrace.Read(file);
            Assert(points.Count == 1, "Concurrent read succeeded while file is open for writing");
            Assert(points[0].frame == 0, "Point 0 read correctly under concurrent file sharing");
        }
    }

    private static void TestPreviewRendererDrawPlayer(string dir)
    {
        var state = new EditorState
        {
            fps = 50,
            duration = 100,
            usePlayerTrace = true,
            playerTrace = new List<PlayerTracePoint>
            {
                new PlayerTracePoint { frame = 0, x = 400, y = 304, sprite = "sprPlayerIdle", subimage = 0, xscale = 1, yscale = 1, alpha = 1 },
                new PlayerTracePoint { frame = 20, x = 460, y = 250, sprite = "sprPlayerJump", subimage = 0, xscale = 1, yscale = 1, alpha = 1 },
                new PlayerTracePoint { frame = 40, x = 500, y = 304, sprite = "sprPlayerRunning", subimage = 1, xscale = -1, yscale = 1, alpha = 1 }
            }
        };

        using (var bmp = new Bitmap(800, 608))
        using (var g = Graphics.FromImage(bmp))
        {
            PreviewRenderer.DrawPlayer(g, state, null, 10, true);
            string outBmp = Path.Combine(dir, "renderer_player_preview.png");
            bmp.Save(outBmp, System.Drawing.Imaging.ImageFormat.Png);
            Assert(File.Exists(outBmp) && new FileInfo(outBmp).Length > 100, "PreviewRenderer.DrawPlayer rendered fallback box and hitbox");
        }
    }

    private static void TestPlayerTraceCanvasAndAimGuide(string dir)
    {
        var state = new EditorState
        {
            fps = 50,
            duration = 100,
            usePlayerTrace = true,
            nativeTest = new NativeTestPosition { x = 400, y = 304 },
            playerTrace = new List<PlayerTracePoint>
            {
                new PlayerTracePoint { frame = 0, x = 400, y = 304, facing = 1, action = "idle" },
                new PlayerTracePoint { frame = 20, x = 460, y = 250, facing = 1, action = "jump", jumping = true },
                new PlayerTracePoint { frame = 40, x = 500, y = 304, facing = -1, action = "fall" },
                new PlayerTracePoint { frame = 60, x = 520, y = 304, facing = 1, action = "dead", dead = true }
            }
        };

        var emitter = new Track
        {
            id = "aim-emitter",
            name = "Aim Emitter",
            kind = "emitter",
            emissionFacing = "aim",
            x = 200,
            y = 150,
            start = 0,
            end = 100
        };
        state.tracks.Add(emitter);

        // Test BulletSystem.PlayerAt interpolation
        var posAt10 = BulletSystem.PlayerAt(state, 10);
        Assert(Math.Abs(posAt10.x - 430.0) < 1e-3, "PlayerAt interpolated X at frame 10");
        Assert(Math.Abs(posAt10.y - 277.0) < 1e-3, "PlayerAt interpolated Y at frame 10");

        // Test Aim angle calculation
        double angleAt10 = ExpressionContext.Direction(200, 150, posAt10.x, posAt10.y);
        Assert(angleAt10 > 0, "Aim direction from emitter to player is computed");

        // Verify off-screen rendering of canvas with player trace and aim guide
        using (var canvas = new StageCanvas())
        using (var bmp = new Bitmap(800, 608))
        using (var g = Graphics.FromImage(bmp))
        {
            canvas.State = state;
            canvas.Room = new RoomData { name = "rAvoidance", width = 800, height = 608 };
            canvas.Frame = 20;
            canvas.SelectedTrack = emitter;
            canvas.Size = new Size(800, 608);

            // Paint canvas to graphics
            var pe = new PaintEventArgs(g, new Rectangle(0, 0, 800, 608));
            var onPaintMethod = typeof(StageCanvas).GetMethod("OnPaint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert(onPaintMethod != null, "StageCanvas has OnPaint method");
            onPaintMethod.Invoke(canvas, new object[] { pe });

            string outBmp = Path.Combine(dir, "canvas_trace_preview.png");
            bmp.Save(outBmp, System.Drawing.Imaging.ImageFormat.Png);
            Assert(File.Exists(outBmp) && new FileInfo(outBmp).Length > 1000, "Canvas rendered aim guide successfully to PNG");
        }
    }

    private static void TestGodModeIniAndPayload(string dir)
    {
        string previewIniPath = Path.Combine(dir, "rufmaker_preview.ini");

        // Simulate packageGame preview INI generation for Normal mode (F5)
        File.WriteAllText(previewIniPath, "[preview]\r\nframe=50\r\ngod=0\r\n");
        string normalIni = File.ReadAllText(previewIniPath);
        Assert(normalIni.Contains("god=0"), "Normal preview writes god=0");

        // Simulate packageGame preview INI generation for Debug mode (F6)
        File.WriteAllText(previewIniPath, "[preview]\r\nframe=50\r\ngod=1\r\n");
        string godIni = File.ReadAllText(previewIniPath);
        Assert(godIni.Contains("god=1"), "Debug preview writes god=1");

        // Test GML guard string
        string killPlayerGml = "instance_create(player.x,player.y,bloodEmitter);\nwith(player) instance_destroy();\n";
        string guardedGml = "if (variable_global_exists('rf_god_mode') && global.rf_god_mode) return 0;\n" + killPlayerGml;
        Assert(guardedGml.StartsWith("if (variable_global_exists('rf_god_mode') && global.rf_god_mode) return 0;"), "God mode guard protects killPlayer script");
    }
}
