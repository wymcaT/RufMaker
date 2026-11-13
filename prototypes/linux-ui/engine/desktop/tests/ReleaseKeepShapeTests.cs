using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class ReleaseKeepShapeTests
{
    static readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object o, string name) { return o.GetType().GetField(name, flags).GetValue(o); }
    static void Set(object o, string name, object value) { o.GetType().GetField(name, flags).SetValue(o, value); }
    static object Call(object o, string name, params object[] args) { return o.GetType().GetMethod(name, flags).Invoke(o, args); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Near(double a, double b, double eps = 0.001, string message = "")
    {
        if (Math.Abs(a - b) > eps) throw new Exception(message + string.Format(" (expected {0}, actual {1})", b, a));
    }

    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        try
        {
            TestUniformSpeedVsKeepShape();
            TestRodLineKeepShape();
            TestInspectorPropertyField(args.Length > 0 ? args[0] : ".");

            Console.WriteLine("ALL PASS: Release Keep Shape tests succeeded!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("TEST FAILED: " + ex);
            return 1;
        }
    }

    static void TestUniformSpeedVsKeepShape()
    {
        // 1. 五角星队形，尖角距离100，内角距离40
        var starUniform = new Track
        {
            id = "star-u",
            kind = "rod",
            shape = "star",
            count = 10,
            x = 400,
            y = 304,
            radius = 100,
            shapeInner = 0.4,
            speed = 5,
            start = 0,
            end = 100,
            releaseFrame = 10,
            releaseDirection = "radial",
            releaseKeepShape = false
        };

        var starKeep = new Track
        {
            id = "star-k",
            kind = "rod",
            shape = "star",
            count = 10,
            x = 400,
            y = 304,
            radius = 100,
            shapeInner = 0.4,
            speed = 5,
            start = 0,
            end = 100,
            releaseFrame = 10,
            releaseDirection = "radial",
            releaseKeepShape = true
        };

        // 验证统一速度模式
        {
            var s = new EditorState { duration = 100, fps = 50, tracks = new List<Track> { starUniform } };
            Motion.Normalize(s);
            var world = new BulletWorld(s);
            world.Seek(10); // 释放帧
            Check(world.bullets.Count == 10, "Should spawn 10 released bullets");
            foreach (var b in world.bullets)
            {
                Near(b.speed, 5.0, 0.001, "Uniform mode: all bullets must have speed 5.0");
            }
        }

        // 验证保持原形状模式
        {
            var s = new EditorState { duration = 100, fps = 50, tracks = new List<Track> { starKeep } };
            Motion.Normalize(s);
            var world = new BulletWorld(s);
            world.Seek(10); // 释放帧
            Check(world.bullets.Count == 10, "Should spawn 10 released bullets");

            var bullets = world.bullets.OrderBy(b => b.index).ToArray();
            var maxSpeed = bullets.Max(b => b.speed);
            var minSpeed = bullets.Min(b => b.speed);

            Near(maxSpeed, 5.0, 0.001, "Outer tip bullet must have speed 5.0");
            Near(minSpeed, 2.0, 0.001, "Inner notch bullet (0.4 ratio) must have speed 5.0 * 0.4 = 2.0");

            // 推进到第 20 帧（飞行 10 帧），验证所有点相对中心等比缩放
            world.Seek(20);
            double cx = 400, cy = 304;
            double expectedRatio = 1.0 + (5.0 * 10) / 100.0; // 1 + 50/100 = 1.5

            var initialPoints = Motion.RodPoints(starKeep, 10);
            for (int i = 0; i < bullets.Length; i++)
            {
                var b = bullets[i];
                double r0 = Math.Sqrt((initialPoints[i].X - cx) * (initialPoints[i].X - cx) + (initialPoints[i].Y - cy) * (initialPoints[i].Y - cy));
                double rCurrent = Math.Sqrt((b.x - cx) * (b.x - cx) + (b.y - cy) * (b.y - cy));
                double ratio = rCurrent / r0;
                Near(ratio, expectedRatio, 0.002, string.Format("Bullet {0} scale ratio must equal 1.5", i));
            }
            Console.WriteLine("PASS: Star formation expands keeping exact geometric shape!");
        }
    }

    static void TestRodLineKeepShape()
    {
        // 直线转转棒模式：长度 200，5 个点（-100, -50, 0, 50, 100）
        var rodKeep = new Track
        {
            id = "rod-k",
            kind = "rod",
            shape = "rod",
            rodMode = "center",
            count = 5,
            x = 400,
            y = 304,
            length = 200,
            speed = 4,
            start = 0,
            end = 100,
            releaseFrame = 10,
            releaseDirection = "radial",
            releaseKeepShape = true
        };

        var s = new EditorState { duration = 100, fps = 50, tracks = new List<Track> { rodKeep } };
        Motion.Normalize(s);
        var world = new BulletWorld(s);
        world.Seek(10);

        var bullets = world.bullets.OrderBy(b => b.index).ToArray();
        Check(bullets.Length == 5, "5 bullets on rod");

        // 中心点（index 2，r = 0）速度应为 0
        Near(bullets[2].speed, 0.0, 0.001, "Center bullet of rod must have speed 0");
        // 端点（index 0 和 4）速度为 4.0
        Near(bullets[0].speed, 4.0, 0.001, "End bullet 0 must have speed 4.0");
        Near(bullets[4].speed, 4.0, 0.001, "End bullet 4 must have speed 4.0");
        // 中间点（index 1 和 3，r = 50）速度为 2.0
        Near(bullets[1].speed, 2.0, 0.001, "Mid bullet 1 must have speed 2.0");
        Near(bullets[3].speed, 2.0, 0.001, "Mid bullet 3 must have speed 2.0");

        world.Seek(25); // 飞行 15 帧
        // 验证 5 颗子弹依然在一条直线上（斜率和 y 坐标完全一致）
        for (int i = 0; i < 5; i++)
        {
            Near(bullets[i].y, 304.0, 0.001, "All bullets stay on horizontal line");
        }
        Console.WriteLine("PASS: Straight line rod expands symmetrically on same line!");
    }

    static void TestInspectorPropertyField(string root)
    {
        var rod = new Track
        {
            id = "rod-ui",
            kind = "rod",
            shape = "star",
            count = 5,
            x = 400,
            y = 304,
            radius = 100,
            start = 0,
            end = 100,
            releaseFrame = 20,
            releaseDirection = "radial",
            releaseKeepShape = true
        };
        var s = new EditorState { duration = 100, fps = 50, tracks = new List<Track> { rod } };
        Motion.Normalize(s);

        using (var form = new MainForm(root))
        {
            Set(form, "state", s);
            Set(form, "document", new LoadData
            {
                state = s,
                project = root,
                projectName = "keep-shape-fixture",
                room = new RoomData { width = 800, height = 608 },
                inventory = new Inventory { sprites = new[] { "sprCherry" }, objects = new string[0], scripts = new string[0] },
                waveform = new WaveData { duration = 12, peaks = new double[100] }
            });

            Call(form, "SelectTrack", rod, null);
            var properties = (ParameterInspector)Get(form, "properties");
            var bag = (NativeProperties)properties.SelectedObject;
            Check(bag != null, "Inspector bag populated");

            var prop = bag.GetProperties().Find("保持原形状", false);
            Check(prop != null, "Inspector must expose '保持原形状' property when radial release");
            Check((bool)prop.GetValue(bag) == true, "'保持原形状' initial value must be true");

            // 测试修改为 false
            prop.SetValue(bag, false);
            Check(rod.releaseKeepShape == false, "Modifying property must update track.releaseKeepShape");
            Console.WriteLine("PASS: Inspector correctly binds '保持原形状' checkbox.");
        }
    }
}
