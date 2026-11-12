using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SnapCardViewHook.Core.Forms
{
    internal sealed class DropDownButton : Button
    {
        private const int ButtonTypeMask = 0x000F;
        private const int SplitButtonStyle = 0x000C;
        private const int DefaultSplitButtonStyle = 0x000D;
        private const int ReflectedNotifyMessage = 0x204E;
        private const int DropDownNotification = -1248;

        public DropDownButton()
        {
            FlatStyle = FlatStyle.System;
            AccessibleRole = AccessibleRole.ButtonDropDown;
        }

        [DefaultValue(null)]
        public ContextMenuStrip DropDownMenu { get; set; }

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.Style = (parameters.Style & ~ButtonTypeMask) |
                    (IsDefault ? DefaultSplitButtonStyle : SplitButtonStyle);
                return parameters;
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            ShowMenu();
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Down || keyData == (Keys.Alt | Keys.Down))
            {
                ShowMenu();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == ReflectedNotifyMessage && message.LParam != IntPtr.Zero &&
                Marshal.PtrToStructure<NotificationHeader>(message.LParam).Code == DropDownNotification)
            {
                ShowMenu();
                message.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref message);
        }

        private void ShowMenu()
        {
            if (DropDownMenu == null || DropDownMenu.IsDisposed || DropDownMenu.Visible) return;
            DropDownMenu.Show(this, Point.Empty, ToolStripDropDownDirection.AboveRight);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NotificationHeader
        {
            public IntPtr Window;
            public UIntPtr Id;
            public int Code;
        }
    }
}
