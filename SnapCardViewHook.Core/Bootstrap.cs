﻿using SnapCardViewHook.Core.Forms;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SnapCardViewHook.Core
{
    public class Bootstrap
    {
        static Bootstrap()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

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

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name).Name;

            try
            {
                var directory = Path.GetDirectoryName(args.RequestingAssembly.Location);

                Debug.WriteLine($"resolving \"{assemblyName}\"");

                if (directory == null)
                    return null;

                foreach (var file in Directory.GetFiles(directory, "*.dll"))
                {
                    if (string.Equals(assemblyName, Path.GetFileNameWithoutExtension(file),
                            StringComparison.InvariantCultureIgnoreCase))
                        return Assembly.LoadFile(file);
                }

                Debug.WriteLine($"resolve of \"{assemblyName}\" failed!");

                return null;
            }
            catch (Exception e)
            {
                MessageBox.Show($"Exception while trying to resolve assembly \"{assemblyName}\"." +
                                $"\nException details:\n\n\n{e}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }
    }
}
