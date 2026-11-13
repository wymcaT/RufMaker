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

    public static void TestNestedPreviewHidden(string root)
    {
        var hidden = new Track { id = "hidden", kind = "emitter", previewHidden = true, start = 0, end = 20 };
        var visible = new Track { id = "visible", kind = "emitter", start = 0, end = 20 };
        var disabled = new Track { id = "disabled", kind = "emitter", enabled = false, start = 0, end = 20 };
        var blocked = new Track { id = "blocked", kind = "emitter", start = 0, end = 20 };
        var hiddenPattern = new Track { id = "hidden-pattern", kind = "pattern", previewHidden = true, children = new System.Collections.Generic.List<Track> { blocked } };
        var nested = new Track { id = "nested", kind = "pattern", children = new System.Collections.Generic.List<Track> { hidden, visible, disabled, hiddenPattern } };
        var pattern = new Track { id = "outer", kind = "pattern", repeatCount = 2, children = new System.Collections.Generic.List<Track> { nested } };
        var state = new EditorState { duration = 200, tracks = new System.Collections.Generic.List<Track> { pattern } };
        using (var sprites = new SpriteStore { Project = root })
        using (var stage = new StageCanvas { State = state, Room = new RoomData(), Sprites = sprites })
        {
            stage.SceneChanged();
            var render = (EditorState)typeof(StageCanvas).GetField("renderState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stage);
            Check(render.tracks.Count(t => t.id.EndsWith("/hidden")) == 2, "Both outer repetitions must expand hidden leaf tracks");
            Check(render.tracks.Where(t => t.id.EndsWith("/hidden")).All(t => !t.enabled), "Hidden leaves must remain disabled through nested expansion");
            Check(!render.tracks.Any(t => t.id.EndsWith("/blocked")), "A hidden nested pattern must suppress its entire subtree");
            Check(render.tracks.Where(t => t.id.EndsWith("/visible")).All(t => t.enabled), "Visible nested siblings must remain enabled");
            Check(render.tracks.Where(t => t.id.EndsWith("/disabled")).All(t => !t.enabled), "Preview flags must not enable disabled tracks");
            Check(hidden.enabled && hiddenPattern.enabled && blocked.enabled, "Preview filtering must not change editable source enabled flags");
            var exported = BulletSystem.Expand(state.Snapshot());
            Check(exported.tracks.Where(t => t.id.EndsWith("/hidden")).All(t => t.enabled), "Export expansion must ignore preview-only hiding");
            Check(exported.tracks.Count(t => t.id.EndsWith("/blocked") && t.enabled) == 2, "Export must retain hidden pattern descendants");
            hidden.previewHidden = false; hiddenPattern.previewHidden = false;
            stage.SceneChanged();
            render = (EditorState)typeof(StageCanvas).GetField("renderState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stage);
            Check(render.tracks.Count(t => t.id.EndsWith("/blocked") && t.enabled) == 2, "Unhiding must restore nested descendants on the next snapshot");
            Check(render.tracks.Where(t => t.id.EndsWith("/hidden")).All(t => t.enabled), "Unhiding must restore nested leaf tracks");
        }
    }

    public static void TestOrbitEventAgeBoundary()
    {
        foreach (bool resetLife in new[] { true, false })
        foreach (int eventFrame in new[] { 0, 10 })
        {
            var emitter = new Track { id = "age-shot", kind = "emitter", start = 0, end = 0, count = 1, speed = 0, x = 500, y = 304, life = 200 };
            var orbit = new Track { id = "age-orbit", start = eventFrame };
            EventPresets.ApplyPreset(orbit, "orbit-bullets");
            orbit.eventCenterX = 400; orbit.eventCenterY = 304; orbit.eventOrbitSpeed = 90;
            orbit.eventResetLife = resetLife; orbit.eventRadiusMode = "fixed"; orbit.eventRadius = 50;
            var state = new EditorState { duration = 200, tracks = new System.Collections.Generic.List<Track> { emitter, orbit } };
            Motion.Normalize(state);
            var world = new BulletWorld(state);
            world.Seek(eventFrame);
            var bullet = world.bullets.Single();
            bool ageZero = resetLife || eventFrame == 0;
            Check(Math.Abs(bullet.x - (ageZero ? 500 : 400)) < .001, "Orbit must not move or snap radius at age zero");
            Check(Math.Abs(bullet.y - (ageZero ? 304 : 254)) < .001, "An older non-reset bullet must rotate immediately");
            Check(Math.Abs(bullet.direction - (ageZero ? 0 : 90)) < .001, "Direction must obey the same age boundary as position");
            world.Seek(eventFrame + 1);
            bullet = world.bullets.Single();
            Check(Math.Abs(bullet.x - (ageZero ? 400 : 350)) < .001, "Next frame must advance exactly one orbit step");
            Check(Math.Abs(bullet.y - (ageZero ? 254 : 304)) < .001, "Fixed radius applies when orbit movement first advances");
            string expected = Json.Write(new[] { bullet.x, bullet.y, bullet.direction, bullet.orbitAngle });
            world.Seek(0); world.Seek(eventFrame + 1);
            bullet = world.bullets.Single();
            Check(expected == Json.Write(new[] { bullet.x, bullet.y, bullet.direction, bullet.orbitAngle }), "Backward seek must reproduce orbit state");
        }
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
            TestNestedPreviewHidden(root);
            TestOrbitEventAgeBoundary();
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
