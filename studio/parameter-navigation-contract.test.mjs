// Source contracts supplement the Windows behavior test; they do not prove UI behavior.
import fs from 'node:fs';
import assert from 'node:assert/strict';
import test from 'node:test';
const inspector=fs.readFileSync(new URL('../desktop/ParameterInspector.cs',import.meta.url),'utf8');
test('editable text, choices and numbers participate in keyboard navigation',()=>{
  assert(inspector.includes('ReadOnly=p.IsReadOnly,TabStop=!p.IsReadOnly,BorderStyle='));
  assert(inspector.includes('TabStop=!p.IsReadOnly,LocalizeItems='));
  assert(inspector.includes('ReadOnly=p.IsReadOnly,TabStop=!p.IsReadOnly,Dock='));
  assert(inspector.includes('number.TabIndex=1'));
  assert(inspector.includes('ControlPaint.DrawFocusRectangle'));
  assert(inspector.includes('protected override bool IsInputKey(Keys keyData)'));
});
test('symbol actions expose descriptive labels and key state refresh is read-only',()=>{
  const state=inspector.slice(inspector.indexOf('private void UpdateKeyState'),inspector.indexOf('private void Place'));
  assert(state.includes('actions.Keyed()'));
  assert(state.includes('key.AccessibleName='));
  assert(state.includes('key.AccessibleDescription='));
  assert(state.includes('Language.ToolTip(tips,key,state)'));
  assert(!state.includes('actions.ToggleKey()'));
  assert(!state.includes('Motion.'));
  assert(inspector.includes('AccessibleName=Language.Text(p.Name)+"："+Language.Text("编辑表达式")'));
});

test('closed focused choices retain native keyboard routing',()=>{
  assert(inspector.includes('c is ComboBox&&c.Visible&&c.Enabled&&(c.ContainsFocus||((ComboBox)c).DroppedDown)'));
});

test('main window gives focused native buttons Space before preview shortcuts',()=>{
  const main=fs.readFileSync(new URL('../desktop/MainForm.cs',import.meta.url),'utf8');
  assert(main.includes('control is ButtonBase&&control.ContainsFocus'));
  assert(main.includes('child.ContainsFocus&&FocusedButtonOwnsSpace(child)'));
  const route=main.slice(main.indexOf('protected override bool ProcessCmdKey'));
  assert(route.indexOf('keyData==Keys.Space&&FocusedButtonOwnsSpace(this)')<route.indexOf('keyData==Keys.Space||keyData==Keys.P'));
});
