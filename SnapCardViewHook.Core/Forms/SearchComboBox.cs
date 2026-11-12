using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SnapCardViewHook.Core.Forms
{
    internal sealed class SearchComboBox : ComboBox
    {
        private object[] _source = Array.Empty<object>();
        private string[] _searchKeys = Array.Empty<string>();
        private bool _updating;
        private bool _filtered;
        private DropDownWindow _dropDownWindow;

        public SearchComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDown;
            AutoCompleteMode = AutoCompleteMode.None;
            AutoCompleteSource = AutoCompleteSource.None;
            IntegralHeight = false;
            DropDownHeight = 300;
        }

        public bool IsUpdating => _updating;

        public void SetItems(object[] items)
        {
            var previous = SelectedItem?.ToString();
            _source = items.ToArray();
            _searchKeys = _source.Select(item => SearchKey(item.ToString())).ToArray();
            SetSelection(previous);
        }

        public object FindItem(string text)
        {
            return _source.FirstOrDefault(item =>
                string.Equals(item.ToString(), text, StringComparison.OrdinalIgnoreCase));
        }

        public void SetSelection(string text)
        {
            var selected = FindItem(text);
            var value = selected?.ToString() ?? text ?? "";
            _filtered = false;
            ReplaceItems(_source, value, value.Length, 0, false, selected);
            base.OnSelectedIndexChanged(EventArgs.Empty);
            base.OnTextChanged(EventArgs.Empty);
        }

        protected override void OnTextUpdate(EventArgs e)
        {
            if (!_updating)
            {
                var text = Text;
                var query = SearchKey(text);
                var matches = query.Length == 0 ? _source : _source.Where((item, index) =>
                    _searchKeys[index].IndexOf(query, StringComparison.Ordinal) >= 0).ToArray();
                _filtered = query.Length != 0;
                ReplaceItems(matches, text, SelectionStart, SelectionLength, Focused && matches.Length != 0);
                if (DroppedDown) Cursor.Current = Cursors.Default;
                base.OnTextChanged(EventArgs.Empty);
            }
            base.OnTextUpdate(e);
        }

        protected override void OnDropDown(EventArgs e)
        {
            if (_dropDownWindow == null)
            {
                var info = new ComboBoxInfo { Size = Marshal.SizeOf<ComboBoxInfo>() };
                if (GetComboBoxInfo(Handle, ref info) && info.List != IntPtr.Zero)
                    _dropDownWindow = new DropDownWindow(this, info.List);
            }

            if (!_updating && _filtered && SelectedItem != null)
            {
                var selected = SelectedItem;
                _filtered = false;
                ReplaceItems(_source, Text, SelectionStart, SelectionLength, null, selected);
            }
            Cursor.Current = Cursors.Default;
            base.OnDropDown(e);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _dropDownWindow?.ReleaseHandle();
            _dropDownWindow = null;
            base.OnHandleDestroyed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && DroppedDown && Items.Count != 0)
            {
                if (SelectedIndex < 0) SelectedIndex = 0;
                DroppedDown = false;
                SelectAll();
                e.Handled = true;
                e.SuppressKeyPress = true;
                OnSelectionChangeCommitted(EventArgs.Empty);
            }
            base.OnKeyDown(e);
        }

        private void ReplaceItems(object[] items, string text, int selectionStart, int selectionLength, bool? showDropDown, object selectedItem = null)
        {
            _updating = true;
            BeginUpdate();
            try
            {
                if (showDropDown == false) DroppedDown = false;
                Items.Clear();
                Items.AddRange(items);
                if (showDropDown == true && !DroppedDown) DroppedDown = true;
                SelectedIndex = -1;
                Text = text;
                if (selectedItem != null) SelectedItem = selectedItem;
                SelectionStart = Math.Min(selectionStart, text.Length);
                SelectionLength = Math.Min(selectionLength, text.Length - SelectionStart);
            }
            finally
            {
                EndUpdate();
                _updating = false;
            }
        }

        private static string SearchKey(string text)
        {
            var result = new StringBuilder(text.Length);
            foreach (var character in text)
                if (char.IsLetterOrDigit(character)) result.Append(char.ToUpperInvariant(character));
            return result.ToString();
        }

        [DllImport("user32.dll")]
        private static extern bool GetComboBoxInfo(IntPtr window, ref ComboBoxInfo info);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ComboBoxInfo
        {
            public int Size;
            public NativeRect Item, Button;
            public int ButtonState;
            public IntPtr Combo, Edit, List;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowPosition
        {
            public IntPtr Window, InsertAfter;
            public int X, Y, Width, Height;
            public uint Flags;
        }

        private sealed class DropDownWindow : NativeWindow
        {
            private const int WmWindowPosChanging = 0x0046;
            private const uint SwpNoSize = 0x0001;
            private const uint SwpNoMove = 0x0002;
            private readonly SearchComboBox _owner;

            public DropDownWindow(SearchComboBox owner, IntPtr handle)
            {
                _owner = owner;
                AssignHandle(handle);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WmWindowPosChanging)
                {
                    var position = Marshal.PtrToStructure<WindowPosition>(m.LParam);
                    if ((position.Flags & SwpNoSize) == 0)
                    {
                        var bounds = _owner.RectangleToScreen(_owner.ClientRectangle);
                        var area = Screen.FromControl(_owner).WorkingArea;
                        var below = Math.Max(0, area.Bottom - bounds.Bottom);
                        var above = Math.Max(0, bounds.Top - area.Top);
                        var openBelow = below >= _owner.DropDownHeight || below >= above;
                        position.Height = Math.Max(1, Math.Min(_owner.DropDownHeight, openBelow ? below : above));
                        if ((position.Flags & SwpNoMove) == 0)
                            position.Y = openBelow ? bounds.Bottom : bounds.Top - position.Height;
                        Marshal.StructureToPtr(position, m.LParam, false);
                    }
                }
                base.WndProc(ref m);
            }
        }
    }
}
