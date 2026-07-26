using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SnapCardViewHook.Core.Helpers
{
    internal static unsafe class HookManager
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<IntPtr, HookRegistration> Hooks =
            new Dictionary<IntPtr, HookRegistration>();

        public static TDelegate CreateHook<TDelegate>(
            IntPtr target,
            TDelegate detour,
            string hookName = null)
            where TDelegate : class
        {
            if (target == IntPtr.Zero)
                throw new ArgumentException("The hook target cannot be null.", nameof(target));

            if (detour == null)
                throw new ArgumentNullException(nameof(detour));

            var detourDelegate = detour as Delegate;
            if (detourDelegate == null)
                throw new ArgumentException("The detour must be a delegate.", nameof(detour));

            var name = string.IsNullOrWhiteSpace(hookName)
                ? detourDelegate.Method.Name
                : hookName;

            lock (SyncRoot)
            {
                if (Hooks.TryGetValue(target, out var existing))
                {
                    throw new InvalidOperationException(
                        $"Target 0x{target.ToInt64():X} already has hook '{existing.Name}' " +
                        $"in the {existing.State} state.");
                }

                IntPtr detourPtr;

                try
                {
                    detourPtr = Marshal.GetFunctionPointerForDelegate(detourDelegate);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"The detour for hook '{name}' could not be marshalled.",
                        exception);
                }

                var registration = new HookRegistration(name, detourDelegate);
                Hooks.Add(target, registration);

                void* originalPtr = null;
                bool created;

                try
                {
                    created = HookHelper.CreateHook(target.ToPointer(), detourPtr.ToPointer(), &originalPtr);
                }
                catch (Exception exception)
                {
                    var failure = new InvalidOperationException(
                        $"Hook '{name}' at 0x{target.ToInt64():X} could not be created.",
                        exception);
                    registration.MarkFailed(failure);
                    throw failure;
                }

                if (!created || originalPtr == null)
                {
                    var failure = new InvalidOperationException(
                        $"Hook '{name}' at 0x{target.ToInt64():X} could not be created and enabled.");
                    registration.MarkFailed(failure);
                    throw failure;
                }

                Delegate originalDelegate;

                try
                {
                    originalDelegate = Marshal.GetDelegateForFunctionPointer(
                        new IntPtr(originalPtr),
                        detourDelegate.GetType());
                }
                catch (Exception exception)
                {
                    var failure = new InvalidOperationException(
                        $"The original trampoline for hook '{name}' could not be marshalled.",
                        exception);
                    registration.MarkFailed(failure);
                    throw failure;
                }

                var typedOriginal = originalDelegate as TDelegate;
                if (typedOriginal == null)
                {
                    var failure = new InvalidOperationException(
                        $"The original trampoline for hook '{name}' has an unexpected delegate type.");
                    registration.MarkFailed(failure);
                    throw failure;
                }

                registration.MarkInstalled(originalDelegate);
                return typedOriginal;
            }
        }

        public static bool DeleteHook(IntPtr target)
        {
            if (target == IntPtr.Zero)
                throw new ArgumentException("The hook target cannot be null.", nameof(target));

            lock (SyncRoot)
            {
                if (!Hooks.TryGetValue(target, out var registration))
                    return false;
                
                if (!HookHelper.DeleteHook(target.ToPointer()))
                    return false;

                registration.Release();
                Hooks.Remove(target);
                return true;
            }
        }

        private sealed class HookRegistration
        {
            private GCHandle _detourRoot;

            public HookRegistration(string name, Delegate detour)
            {
                Name = name;
                _detourRoot = GCHandle.Alloc(detour, GCHandleType.Normal);
            }

            public string Name { get; }

            public Delegate Original { get; private set; }

            public Exception Failure { get; private set; }

            public string State
            {
                get
                {
                    if (Failure != null)
                        return "failed";

                    return Original == null ? "installing" : "installed";
                }
            }

            public void MarkInstalled(Delegate original)
            {
                Original = original;
            }

            public void MarkFailed(Exception failure)
            {
                Failure = failure;
            }

            public void Release()
            {
                Original = null;
                Failure = null;

                if (_detourRoot.IsAllocated)
                    _detourRoot.Free();
            }
        }
    }
}
