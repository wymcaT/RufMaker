using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;

internal static class PreviewHiddenAndOrbitTests
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static void TestPreviewHidden(string root)
    {
        var s = new EditorState { fps = 50, duration = 200 };
        var track = new Track { id = "fx1", name = "Shockwave", kind = "effect", enabled = true, previewHidden = false, start = 0, end = 100 };
        var normal = new Track { id = "bullet1", name = "Emitter", kind = "emitter", enabled = true, previewHidden = false, start = 0, end = 100 };
        s.tracks.Add(track);
        s.tracks.Add(normal);

        using (var sprites = new SpriteStore { Project = root })
        using (var stage = new StageCanvas { State = s, Room = new RoomData { width = 800, height = 608 }, Sprites = sprites })
        {
            stage.SceneChanged();
            var render = (EditorState)typeof(StageCanvas).GetField("renderState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stage);
            Check(render.tracks.Single(t => t.id == "fx1").enabled == true, "Enabled fx must initially be enabled in renderState");

            // Mark previewHidden
            track.previewHidden = true;
            stage.SceneChanged();
            render = (EditorState)typeof(StageCanvas).GetField("renderState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stage);
            Check(render.tracks.Single(t => t.id == "fx1").enabled == false, "previewHidden track must be disabled in preview renderState");
            Check(s.tracks.Single(t => t.id == "fx1").enabled == true, "Project track enabled must stay true when previewHidden");
            Check(render.tracks.Single(t => t.id == "bullet1").enabled == true, "Normal track must stay enabled in preview renderState");

            // Check hit testing / pick active skips previewHidden
            var pickActiveMethod = typeof(StageCanvas).GetMethod("PickActive", BindingFlags.Instance | BindingFlags.NonPublic);
            stage.ShowFrame(10);
            bool fxPick = (bool)pickActiveMethod.Invoke(stage, new object[] { track });
            Check(!fxPick, "PickActive must return false for previewHidden track");
            bool bulletPick = (bool)pickActiveMethod.Invoke(stage, new object[] { normal });
            Check(bulletPick, "PickActive must return true for normal track");
        }
        Console.WriteLine("PASS: previewHidden disables tracks in preview renderState and skips hit testing while preserving project enabled state.");
    }

    public static void TestOrbitMotionParameters()
    {
        var track = new Track
        {
            id = "orb1",
            kind = "emitter",
            motion = "orbit",
            x = 400,
            y = 304,
            orbitRadius = 100,
            orbitPhase = 0,
            orbitSpeed = 2
        };

        // Name checks
        Check(Motion.Name("x", track) == "公转圆心 X", "Motion.Name('x', orbitTrack) must be '公转圆心 X'");
        Check(Motion.Name("y", track) == "公转圆心 Y", "Motion.Name('y', orbitTrack) must be '公转圆心 Y'");
        Check(Motion.Name("orbitRadius", track) == "公转半径", "Motion.Name('orbitRadius') must be '公转半径'");
        Check(Motion.Name("x") == "X", "Default Motion.Name('x') must stay 'X'");

        // Category checks via reflection
        var catMethod = typeof(MainForm).GetMethod("ParameterCategory", BindingFlags.Static | BindingFlags.NonPublic);
        string catX = (string)catMethod.Invoke(null, new object[] { track, "x" });
        string catY = (string)catMethod.Invoke(null, new object[] { track, "y" });
        string catR = (string)catMethod.Invoke(null, new object[] { track, "orbitRadius" });
        string catP = (string)catMethod.Invoke(null, new object[] { track, "orbitPhase" });
        Check(catX == "圆周运动" && catY == "圆周运动" && catR == "圆周运动" && catP == "圆周运动",
            "Orbit parameters x, y, orbitRadius, orbitPhase must be in '圆周运动' category");

        // Motion calculation
        var p0 = Motion.At(track, 0);
        // at frame 0, a = 0, x = 400 + 100 = 500, y = 304
        Check(Math.Abs(p0.x - 500) < 0.001, "At frame 0, x should be centerX + radius = 500");
        Check(Math.Abs(p0.y - 304) < 0.001, "At frame 0, y should be centerY = 304");

        // at frame 45, angle = 45 * 2 = 90 deg, x = 400 + 0 = 400, y = 304 - 100 = 204
        var p45 = Motion.At(track, 45);
        Check(Math.Abs(p45.x - 400) < 0.001, "At 90 deg, x should be centerX = 400");
        Check(Math.Abs(p45.y - 204) < 0.001, "At 90 deg, y should be centerY - radius = 204");

        Console.WriteLine("PASS: circular motion orbit center and parameters cleanly grouped and calculated.");
    }

    public static void TestOrbitBulletEvents()
    {
        var emitter = new Track
        {
            id = "em",
            kind = "emitter",
            enabled = true,
            start = 0,
            end = 100,
            interval = 1,
            count = 1,
            speed = 0,
            life = 120,
            sprite = "sprCherry",
            x = 400,
            y = 204 // 100px above (400, 304)
        };
        var orbitEvent = new Track
        {
            id = "ev_orbit",
            kind = "control",
            enabled = true,
            start = 10,
            end = 10,
            eventCenterX = 400,
            eventCenterY = 304,
            eventOrbitSpeed = 3,
            eventRadiusMode = "keep"
        };
        EventPresets.ApplyPreset(orbitEvent, "orbit-bullets");
        orbitEvent.eventCenterX = 400;
        orbitEvent.eventCenterY = 304;
        orbitEvent.eventOrbitSpeed = 3;
        orbitEvent.eventRadiusMode = "keep";

        var s = new EditorState { fps = 50, duration = 200, tracks = new System.Collections.Generic.List<Track> { emitter, orbitEvent } };
        Motion.Normalize(s);

        var world = new BulletWorld(s);
        world.Seek(5);
        var b = world.bullets.FirstOrDefault(bullet => bullet.birth == 0);
        Check(b != null, "Bullet born at frame 0 must exist");
        Check(!b.orbiting, "Bullet before event must not be orbiting");

        world.Seek(15);
        b = world.bullets.FirstOrDefault(bullet => bullet.orbiting);
        Check(b != null, "Bullet orbiting at frame 15 must exist");
        Check(b.orbiting, "Bullet after event must be orbiting");
        Check(Math.Abs(b.orbitCenterX - 400) < 0.001, "Orbit center X must be 400");
        Check(Math.Abs(b.orbitCenterY - 304) < 0.001, "Orbit center Y must be 304");
        Check(Math.Abs(b.orbitRadius - 100) < 0.5, "Orbit radius should be initial distance ~100px");

        Console.WriteLine("PASS: orbit-bullets event triggers and maintains orbit kinematics in simulation.");
    }

    public static void TestPolygonRigidRotation()
    {
        var rod = new Track
        {
            id = "poly",
            kind = "rod",
            shape = "polygon",
            sides = 4,
            count = 4,
            radius = 100,
            x = 400,
            y = 304,
            angularSpeed = 2,
            start = 0,
            end = 100
        };

        var pts0 = Motion.RodPoints(rod, 0);
        var pts45 = Motion.RodPoints(rod, 45); // 45 * 2 = 90 deg rotation
        // The entire 4-gon should have rotated by 90 degrees as a rigid body
        double dx0 = pts0[0].X - 400, dy0 = pts0[0].Y - 304;
        double dx45 = pts45[0].X - 400, dy45 = pts45[0].Y - 304;
        double a0 = Math.Atan2(dy0, dx0) * 180 / Math.PI;
        double a45 = Math.Atan2(dy45, dx45) * 180 / Math.PI;
        double diff = ((a45 - a0) % 360 + 540) % 360 - 180;
        // In GDI screen coordinates, y increases down, angle increases clockwise: 90 deg rotation
        Check(Math.Abs(Math.Abs(diff) - 90) < 0.5, "Polygon must rotate as a rigid body by 90 degrees");

        Console.WriteLine("PASS: polygon formation rotates as rigid body decoupling from rolling.");
    }

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            string root = args.Length > 0 ? args[0] : Path.GetFullPath("projects/rufmaker-demo.gm82");
            TestPreviewHidden(root);
            TestOrbitMotionParameters();
            TestOrbitBulletEvents();
            TestPolygonRigidRotation();
            Console.WriteLine("ALL PREVIEW HIDDEN & ORBIT TESTS PASSED!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
