using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class VariableAndKeyframeTests
{
    static readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object o, string name) { return o.GetType().GetField(name, flags).GetValue(o); }
    static object Field(object o, string name) { return o.GetType().GetField(name).GetValue(o); }
    static void Set(object o, string name, object value) { o.GetType().GetField(name, flags).SetValue(o, value); }
    static object Call(object o, string name, params object[] args) { return o.GetType().GetMethod(name, flags).Invoke(o, args); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    static EditorState State(params Track[] tracks)
    {
        var s = new EditorState { duration = 600, fps = 50, laneCount = 4, tracks = tracks.ToList() };
        Motion.Normalize(s);
        return s;
    }

    static MainForm Editor(string root, EditorState state)
    {
        var form = new MainForm(root);
        Set(form, "state", state);
        Set(form, "document", new LoadData
        {
            state = state,
            project = root,
            projectName = "variable-and-keyframe-fixture",
            room = new RoomData { width = 800, height = 608 },
            inventory = new Inventory { sprites = new[] { "sprCherry" }, objects = new string[0], scripts = new string[0] },
            waveform = new WaveData { duration = 12, peaks = new double[100] }
        });
        ((TimelineCanvas)Get(form, "timeline")).State = state;
        return form;
    }

    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        var root = args[0];
        try
        {
            // 1. 验证添加全局变量绝不报 NullReferenceException
            TestAddGlobalVariable(root);

            // 2. 验证时间轴右键单选关键帧与删除
            TestRightClickSingleKeyframe(root);

            // 3. 验证时间轴右键多选关键帧与删除
            TestRightClickMultiKeyframes(root);

            // 4. 验证无关键帧时的菜单
            TestRightClickNoKeyframe(root);

            Console.WriteLine("ALL PASS: Variable addition and keyframe right-click delete tests succeeded.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("TEST FAILED: " + ex);
            return 1;
        }
    }

    static void TestAddGlobalVariable(string root)
    {
        var emitter = new Track { id = "emit1", name = "emitter", kind = "emitter", lane = 0, start = 0, end = 300, sprite = "sprCherry" };
        var state = State(emitter);
        Check(state.variableTimeline == null, "Initial state should have no variableTimeline");

        using (var form = Editor(root, state))
        {
            var timeline = (TimelineCanvas)Get(form, "timeline");
            var properties = (ParameterInspector)Get(form, "properties");

            // 调用 AddGlobalVariable
            Call(form, "AddGlobalVariable");

            var added = state.tracks.FirstOrDefault(t => t.kind == "variable");
            Check(added != null, "Global variable track should be added");
            Check(added.variableName == "phase", "Default variable name should be phase");
            Check(state.variableTimeline != null, "variableTimeline should be attached after adding variable");

            // 验证属性面板读取
            var bag = (NativeProperties)properties.SelectedObject;
            Check(bag != null, "Inspector properties must be populated");

            var currentValProp = bag.GetProperties().Find("当前游标值", false);
            Check(currentValProp != null, "Inspector must have '当前游标值' property");

            // 读取当前游标值，此时帧为 0，不能抛异常
            double val = (double)currentValProp.GetValue(bag);
            Check(!Double.IsNaN(val), "Variable value must be a valid number");
            Console.WriteLine("PASS: AddGlobalVariable succeeded without NullReferenceException; initial value: " + val);

            // 再次添加第二个变量，验证重命名递增
            Call(form, "AddGlobalVariable");
            var second = state.tracks.FirstOrDefault(t => t.kind == "variable" && t.variableName == "phase2");
            Check(second != null, "Second variable should be phase2");

            bag = (NativeProperties)properties.SelectedObject;
            currentValProp = bag.GetProperties().Find("当前游标值", false);
            double val2 = (double)currentValProp.GetValue(bag);
            Check(!Double.IsNaN(val2), "Second variable value must be a valid number");
            Console.WriteLine("PASS: Second variable phase2 added cleanly.");
        }
    }

    static Point KeyPoint(TimelineCanvas t, Track track, Keyframe key)
    {
        var lanes = ((IEnumerable)Get(t, "lanes")).Cast<object>().ToArray();
        var lane = lanes.FirstOrDefault(l => (Track)Field(l, "track") == track && (string)Field(l, "property") == key.property)
            ?? lanes.First(l => (Track)Field(l, "track") == track || (Field(l, "clips") as List<Track>) != null && ((List<Track>)Field(l, "clips")).Contains(track));
        int top = (int)Field(lane, "top");
        int height = (int)Field(lane, "height");
        string prop = (string)Field(lane, "property");
        int header = (int)t.GetType().GetProperty("Header", flags).GetValue(t, null);
        int y = header + top + (prop == null ? height - 6 : height / 2);
        int x = (int)(float)Call(t, "X", key.frame);
        return new Point(x, y);
    }

    static void TestRightClickSingleKeyframe(string root)
    {
        var obj = new Track { id = "obj1", name = "obj1", kind = "object", lane = 0, start = 0, end = 300, sprite = "sprCherry" };
        Motion.SetKey(obj, "x", 50, 200);
        Motion.SetKey(obj, "x", 150, 400);
        var state = State(obj);

        using (var form = Editor(root, state))
        {
            var timeline = (TimelineCanvas)Get(form, "timeline");
            timeline.Size = new Size(1000, 400);
            timeline.PixelsPerSecond = 100;
            timeline.RefreshLayout();

            Call(form, "SelectTrack", obj, null);
            var key1 = obj.keyframes.First(k => k.frame == 50);

            // 模拟右键点击 key1 菱形位置
            var p = KeyPoint(timeline, obj, key1);

            // 直接调用 OnMouseDown with Right button
            Call(timeline, "OnMouseDown", new MouseEventArgs(MouseButtons.Right, 1, p.X, p.Y, 0));

            // 检查 key1 被选中
            Check(timeline.SelectedKeys.Contains(key1), "Right-clicked keyframe must be in SelectedKeys");
            Check(Get(form, "selectedKey") == key1, "form.selectedKey must match right-clicked key");

            // 构建右键菜单
            using (var menu = new ContextMenuStrip())
            {
                Call(form, "CompactTrackContext", menu);
                var items = menu.Items.Cast<ToolStripItem>().ToList();

                var delItem = items.FirstOrDefault(i => i.Text != null && i.Text.Contains("删除关键帧"));
                Check(delItem != null, "Menu must contain '删除关键帧    Delete'");

                var copyItem = items.FirstOrDefault(i => i.Text != null && i.Text.Contains("复制关键帧"));
                Check(copyItem != null, "Menu must contain '复制关键帧    Ctrl+C'");

                var objDelItem = items.FirstOrDefault(i => i.Text != null && i.Text.Contains("删除所选物件"));
                Check(objDelItem != null, "Menu should retain object delete option");
                Check(objDelItem.Text == "删除所选物件", "Object delete must not have 'Delete' shortcut suffix when key is selected");

                // 执行删除关键帧
                delItem.PerformClick();

                // 验证 key1 被删除，而 key2 和物件依然存在
                Check(!obj.keyframes.Contains(key1), "key1 must be removed from track.keyframes");
                Check(obj.keyframes.Count == 1 && obj.keyframes[0].frame == 150, "Other keyframes must remain intact");
                Check(state.tracks.Contains(obj), "Track obj must NOT be deleted");
                Check(timeline.SelectedKeys.Count == 0, "SelectedKeys must be cleared after delete");
                Console.WriteLine("PASS: Single keyframe right-click delete works and preserves track.");

                // 验证撤销恢复
                Call(form, "Undo", false);
                var restoredState = (EditorState)Get(form, "state");
                var restoredObj = restoredState.tracks.Single(t => t.id == "obj1");
                Check(restoredObj.keyframes.Any(k => k.frame == 50), "Undo must restore deleted keyframe");
                Console.WriteLine("PASS: Undo successfully restored deleted keyframe.");
            }
        }
    }

    static void TestRightClickMultiKeyframes(string root)
    {
        var obj = new Track { id = "obj2", name = "obj2", kind = "object", lane = 0, start = 0, end = 300, sprite = "sprCherry" };
        Motion.SetKey(obj, "x", 40, 100);
        Motion.SetKey(obj, "x", 80, 200);
        Motion.SetKey(obj, "x", 120, 300);
        var state = State(obj);

        using (var form = Editor(root, state))
        {
            var timeline = (TimelineCanvas)Get(form, "timeline");
            Call(form, "SelectTrack", obj, null);

            // 多选两个关键帧
            timeline.SelectedKeys.Clear();
            timeline.SelectedKeys.Add(obj.keyframes[0]);
            timeline.SelectedKeys.Add(obj.keyframes[1]);

            using (var menu = new ContextMenuStrip())
            {
                Call(form, "CompactTrackContext", menu);
                var items = menu.Items.Cast<ToolStripItem>().ToList();

                var delItem = items.FirstOrDefault(i => i.Text != null && i.Text.Contains("删除所选关键帧"));
                Check(delItem != null, "Menu must contain '删除所选关键帧    Delete' for multi-selection");

                var copyItem = items.FirstOrDefault(i => i.Text != null && i.Text.Contains("复制所选 2 个关键帧"));
                Check(copyItem != null, "Menu must contain '复制所选 2 个关键帧    Ctrl+C'");

                // 执行删除
                delItem.PerformClick();

                Check(obj.keyframes.Count == 1 && obj.keyframes[0].frame == 120, "Both selected keyframes must be removed");
                Console.WriteLine("PASS: Multi-keyframe right-click delete succeeded.");
            }
        }
    }

    static void TestRightClickNoKeyframe(string root)
    {
        var obj = new Track { id = "obj3", name = "obj3", kind = "object", lane = 0, start = 0, end = 300, sprite = "sprCherry" };
        var state = State(obj);

        using (var form = Editor(root, state))
        {
            var timeline = (TimelineCanvas)Get(form, "timeline");
            Call(form, "SelectTrack", obj, null);
            timeline.SelectedKeys.Clear();

            using (var menu = new ContextMenuStrip())
            {
                Call(form, "CompactTrackContext", menu);
                var items = menu.Items.Cast<ToolStripItem>().ToList();

                var delKey = items.FirstOrDefault(i => i.Text != null && i.Text.Contains("关键帧") && i.Text.Contains("删除"));
                Check(delKey == null, "Menu must NOT contain delete keyframe option when no key is selected");

                var delObj = items.FirstOrDefault(i => i.Text != null && i.Text.Contains("删除所选物件"));
                Check(delObj != null && delObj.Text.Contains("Delete"), "Object delete must have 'Delete' shortcut suffix when no key is selected");
                Console.WriteLine("PASS: Normal track right-click menu unchanged when no keyframes selected.");
            }
        }
    }
}
