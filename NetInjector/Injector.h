#pragma once
#include "pch.h"

__declspec(dllexport)
LRESULT MessageHookProc(int nCode, WPARAM wparam, LPARAM lparam);

namespace ManagedInjector
{
	ref class InjectorData;

	public ref class Injector : System::Object
	{
		literal System::String^ RequestDataKey = "NetInjector.Request";
		literal System::String^ ErrorDataKey = "NetInjector.Error";
		literal System::String^ EntryAssemblyDataKey = "NetInjector.EntryAssembly";
		literal System::String^ DomainRegistryKeyPrefix = "NetInjector.Domain:";
		literal System::String^ DomainMutexNamePrefix = "Local\\NetInjector.";

		static System::AppDomain^ GetDefaultAppDomain();
		static void InvokeEntryPoint();

	public:

		static void Launch(System::IntPtr windowHandle, InjectorData^ transportData);

		static void LoadIntoAppDomain(InjectorData^ injectorData);

		static void LogMessage(System::String^ message, bool append);
	};
}
