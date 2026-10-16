using SnapCardViewHook.Core.Forms;
using System;
using System.Threading;
using System.Windows.Forms;

namespace SnapCardViewHook.Core
{
    public class Bootstrap
    {
        public static void Init(string settings)
        {
            try
            {
                SnapTypeDataCollector.EnsureLoaded();
                CreateForm();
            }
            catch (Exception e) 
            {
                MessageBox.Show(
                    $"An error occured, SnapCardViewer has not loaded properly and will not function, an update might be required." +
                    $"\nError details:\n\n\n{e}",
                    "Error", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Exclamation);
            }
        }

        private static Thread _formThread;

        private static void CreateForm()
        {
            if (_formThread != null && _formThread.IsAlive)
                return;

            var t = _formThread = new Thread(() =>
            {
                var f = new CardViewSelectorForm();
                Application.Run(f);
            });

            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }
    }
}
