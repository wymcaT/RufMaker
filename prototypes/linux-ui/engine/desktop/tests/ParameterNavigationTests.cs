using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class ParameterNavigationTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(Control nested in All(c))yield return nested;}}
    private static void Key(Control control,Keys key){typeof(Control).GetMethod("OnKeyDown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(control,new object[]{new KeyEventArgs(key)});}
    private static void MainWindowRouting(string root)
    {
        using(var main=new MainForm(root))
        {
            // Suppress only startup I/O. Exercise the real MainForm focus tree
            // and command route, without loading a bridge or a user project.
            var events=(EventHandlerList)typeof(Component).GetProperty("Events",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(main,null);
            var shown=typeof(Form).GetField("EVENT_SHOWN",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
            events.RemoveHandler(shown,events[shown]);
            var inspector=(ParameterInspector)typeof(MainForm).GetField("properties",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(main);
            bool keyed=false,random=false;int expressions=0,toggles=0;
            var p=new NativeProperties();
            p.Add("Coordinate","General",typeof(double),()=>4.0,v=>{},actions:new ParameterActions{Property="x",Random=()=>random,Expression=()=>"",SetExpression=v=>{},ToggleRandom=()=>{random=!random;},EditExpression=()=>{expressions++;},Keyed=()=>keyed,ToggleKey=()=>{keyed=!keyed;toggles++;}});
            inspector.SelectedObject=p;
            main.Show();Application.DoEvents();
            foreach(string name in new[]{"key:Coordinate","random:Coordinate","expression:Coordinate"})
            {
                var button=All(inspector).OfType<ButtonBase>().Single(c=>c.Name==name);
                Check(button.Focus()&&button.ContainsFocus,"Main window action must receive focus: "+name);
                var msg=Message.Create(button.Handle,0x100,(IntPtr)(int)Keys.Space,IntPtr.Zero);
                var route=typeof(MainForm).GetMethod("ProcessCmdKey",BindingFlags.NonPublic|BindingFlags.Instance);
                Check(!(bool)route.Invoke(main,new object[]{msg,Keys.Space}),"Main window must leave Space to the focused action: "+name);
                Key(button,Keys.Space);
                typeof(Control).GetMethod("OnKeyUp",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(button,new object[]{new KeyEventArgs(Keys.Space)});
            }
            Check(keyed&&toggles==1&&random&&expressions==1,"Space must activate each native property action exactly once");
            var stage=(Control)typeof(MainForm).GetField("stage",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(main);
            Check(stage.Focus(),"Canvas must receive focus");
            var canvasMessage=Message.Create(stage.Handle,0x100,(IntPtr)(int)Keys.Space,IntPtr.Zero);
            Check((bool)typeof(MainForm).GetMethod("ProcessCmdKey",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,new object[]{canvasMessage,Keys.Space}),"Canvas Space must retain preview command routing");
            main.Close();
        }
    }
    [STAThread] private static void Main(string[] args)
    {
        double value=4;string title="original",choice="one";bool keyed=false;int toggles=0;
        var actions=new ParameterActions{Property="x",Random=()=>false,Expression=()=>"",SetExpression=v=>{},ToggleRandom=()=>{},EditExpression=()=>{},Keyed=()=>keyed,ToggleKey=()=>{keyed=!keyed;toggles++;}};
        var properties=new NativeProperties();
        properties.Add("Coordinate","General",typeof(double),()=>value,v=>value=Convert.ToDouble(v),actions:actions);
        properties.Add("Title","General",typeof(string),()=>title,v=>title=(string)v);
        properties.Add("Choice","General",typeof(string),()=>choice,v=>choice=(string)v,new[]{"one","two"});
        properties.Add("Read only","General",typeof(double),()=>9.0,null);
        using(var form=new Form())using(var inspector=new ParameterInspector{Dock=DockStyle.Fill})
        {
            form.Controls.Add(inspector);inspector.SelectedObject=properties;form.Show();Application.DoEvents();
            var controls=All(inspector).ToArray();var number=controls.OfType<ScrubNumber>().Single(n=>n.AccessibleName=="Coordinate");
            Check(number.TabStop,"Editable number must be keyboard reachable");
            Check(!controls.OfType<ScrubNumber>().Single(n=>n.AccessibleName=="Read only").TabStop,"Read-only number must not trap Tab");
            var combo=controls.OfType<ComboBox>().Single();Check(combo.TabStop,"Choice must be keyboard reachable");
            combo.Focus();Application.DoEvents();Check(!combo.DroppedDown&&inspector.EditingText,"Closed focused choice must retain typing and edit shortcuts");
            Check(controls.OfType<TextBox>().Single(t=>t.AccessibleName=="Title").TabStop,"Text must be keyboard reachable");
            var key=controls.OfType<CheckBox>().Single(c=>c.Name=="key:Coordinate");
            var expression=controls.OfType<Button>().Single(c=>c.Name=="expression:Coordinate");
            Check(key.AccessibleName.Contains("Coordinate")&&!String.IsNullOrEmpty(key.AccessibleDescription),"Keyframe state must have a descriptive accessible label");
            Check(expression.AccessibleName.Contains("Coordinate"),"Expression action must identify its property");
            Check(number.TabIndex==1&&key.TabIndex==2&&expression.TabIndex==3,"Numeric row follows visual tab order");
            number.Focus();Check(form.SelectNextControl(number,true,true,true,true)&&key.Focused,"Tab reaches keyframe toggle from numeric editor");
            Check(form.SelectNextControl(key,true,true,true,true)&&expression.Focused,"Tab reaches expression action next");
            Check(form.SelectNextControl(expression,false,true,true,true)&&key.Focused,"Shift-Tab reverses row order");
            Check(!key.Checked&&key.Text=="◇","Unkeyed state");
            keyed=true;inspector.Refresh();Check(key.Checked&&key.Text=="◆","Keyed state refreshes without mutation");Check(toggles==0,"Refresh must never toggle animation");
            number.Focus();Key(number,Keys.F2);Application.DoEvents();var input=number.Controls.OfType<TextBox>().Single();
            Check(input.Visible,"F2 opens numeric text entry");input.Text="12";input.Modified=true;Key(input,Keys.Escape);Check(value==4,"Escape must not commit");
            Key(number,Keys.Enter);input.Text="13";input.Modified=true;Key(input,Keys.Enter);Check(value==13&&!input.Visible,"Enter commits numeric text entry");
            Check(toggles==0,"Numeric entry must preserve keyframe-toggle semantics");
            inspector.Width=220;Application.DoEvents();Check(All(inspector).Contains(number),"Relayout must preserve editor identity");
            form.Close();
        }
        MainWindowRouting(args[0]);
        Console.WriteLine("Parameter navigation checks passed");
    }
}
