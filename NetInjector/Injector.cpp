// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#include "pch.h"
#include "Injector.h"
#include "InjectorData.h"
#include <vcclr.h>
#include <mscoree.h>

//#using <System.Dll>
//#using <System.XML.Dll>

#pragma comment(lib, "User32.lib")

using namespace ManagedInjector;
using namespace System;
using namespace System::IO;
using namespace System::Reflection;
using namespace System::Runtime::InteropServices;
using namespace System::Threading;

static unsigned int WM_GOBABYGO = ::RegisterWindowMessage(L"Net_Injector_wmsg");
static HHOOK _messageHookHandle;

AppDomain^ Injector::GetDefaultAppDomain()
{
	// An unmanaged callback may enter through a payload domain. Anchor the registry
	// in the current CLR's default domain, independently of the calling DLL/domain.
	auto runtimePointer = RuntimeEnvironment::GetRuntimeInterfaceAsIntPtr(
		Guid("CB2F6723-AB3A-11D2-9C40-00C04FA30A3E"),
		Guid("CB2F6722-AB3A-11D2-9C40-00C04FA30A3E"));
	auto runtimeHost = static_cast<ICorRuntimeHost*>(runtimePointer.ToPointer());
	IUnknown* domainUnknown = nullptr;
	try
	{
		Marshal::ThrowExceptionForHR(runtimeHost->GetDefaultDomain(&domainUnknown));
		return safe_cast<AppDomain^>(Marshal::GetObjectForIUnknown(IntPtr(domainUnknown)));
	}
	finally
	{
		if (domainUnknown != nullptr) domainUnknown->Release();
		runtimeHost->Release();
	}
}

void Injector::InvokeEntryPoint()
{
	auto domain = AppDomain::CurrentDomain;
	auto request = safe_cast<array<String^>^>(domain->GetData(RequestDataKey));
	try
	{
		auto assembly = safe_cast<Assembly^>(domain->GetData(EntryAssemblyDataKey));
		if (assembly == nullptr)
		{
			assembly = Assembly::LoadFrom(request[0]);
			domain->SetData(EntryAssemblyDataKey, assembly);
		}
		auto type = assembly->GetType(request[1], true);
		auto method = type->GetMethod(request[2], BindingFlags::Public | BindingFlags::Static,
			nullptr, gcnew array<Type^> { String::typeid }, nullptr);
		if (method == nullptr)
			throw gcnew MissingMethodException(request[1], request[2] + "(String)");
		method->Invoke(nullptr, gcnew array<Object^> { request[3] });
	}
	catch (Exception^ error)
	{
		// Pass error text back without loading payload exception types in the host.
		domain->SetData(ErrorDataKey, error->ToString());
	}
}

void Injector::LoadIntoAppDomain(InjectorData^ injectorData)
{
	if (injectorData == nullptr)
		throw gcnew ArgumentNullException("injectorData");
	auto assemblyPath = Path::GetFullPath(injectorData->AssemblyName);
	auto assemblyName = AssemblyName::GetAssemblyName(assemblyPath)->Name;
	auto host = GetDefaultAppDomain();
	auto domainKey = DomainRegistryKeyPrefix + assemblyName->ToUpperInvariant();

	// A named mutex also coordinates different NetInjector copies and AppDomains.
	auto gate = gcnew Mutex(false, DomainMutexNamePrefix +
		System::Diagnostics::Process::GetCurrentProcess()->Id.ToString() + "." + assemblyName->ToUpperInvariant());
	bool ownsGate = false;
	try
	{
		try { ownsGate = gate->WaitOne(); }
		catch (AbandonedMutexException^) { ownsGate = true; }
		auto domain = safe_cast<AppDomain^>(host->GetData(domainKey));
		bool created = domain == nullptr;
		if (domain == nullptr)
		{
			auto setup = gcnew AppDomainSetup();
			setup->ApplicationBase = Path::GetDirectoryName(assemblyPath);
			setup->ConfigurationFile = assemblyPath + ".config";
			domain = AppDomain::CreateDomain(assemblyName, nullptr, setup);
			host->SetData(domainKey, domain);
		}
		try
		{
			String^ action = "Reusing";
			if (created) action = "Created";
			LogMessage(String::Format("{0} AppDomain '{1}' (id {2}).",
				action, assemblyName, domain->Id), true);
		}
		catch (Exception^ error) { System::Diagnostics::Debug::WriteLine(error); }

		domain->SetData(RequestDataKey, gcnew array<String^> {
			assemblyPath, injectorData->ClassName, injectorData->MethodName, injectorData->SettingsFile });
		domain->SetData(ErrorDataKey, nullptr);
		try
		{
			domain->DoCallBack(gcnew CrossAppDomainDelegate(&Injector::InvokeEntryPoint));
			auto error = safe_cast<String^>(domain->GetData(ErrorDataKey));
			if (error != nullptr)
				throw gcnew InvalidOperationException(error);
		}
		finally
		{
			domain->SetData(RequestDataKey, nullptr);
			domain->SetData(ErrorDataKey, nullptr);
		}
	}
	finally
	{
		if (ownsGate) gate->ReleaseMutex();
		delete gate;
	}
}
//-----------------------------------------------------------------------------
//Spying Process functions follow
//-----------------------------------------------------------------------------
void Injector::Launch(System::IntPtr windowHandle, InjectorData^ injectorData)
{
	auto transportDataString = System::String::Empty;
	{
		auto serializer = gcnew System::Xml::Serialization::XmlSerializer(InjectorData::typeid);

		System::IO::StringWriter^ stream = nullptr;
		try
		{
			stream = gcnew System::IO::StringWriter();
			{
				serializer->Serialize(stream, injectorData);
				transportDataString = stream->ToString();
			}
		}
		finally
		{
			if (stream != nullptr)
			{
				delete stream;
			}
		}
	}

	const pin_ptr<const wchar_t> acmLocal = PtrToStringChars(transportDataString);

	HINSTANCE hinstDLL;

	if (::GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, (LPCTSTR)&MessageHookProc, &hinstDLL))
	{
		LogMessage("GetModuleHandleEx successful", true);
		DWORD processID = 0;
		DWORD threadID = GetWindowThreadProcessId((HWND)windowHandle.ToPointer(), &processID);

		if (processID)
		{
			LogMessage("Got process id", true);
			HANDLE hProcess = OpenProcess(PROCESS_ALL_ACCESS, FALSE, processID);
			if (hProcess)
			{
				LogMessage("Got process handle", true);
				int buffLen = (transportDataString->Length + 1) * sizeof(wchar_t);
				void* acmRemote = VirtualAllocEx(hProcess, nullptr, buffLen, MEM_COMMIT, PAGE_READWRITE);

				if (acmRemote)
				{
					LogMessage("VirtualAllocEx successful", true);
					WriteProcessMemory(hProcess, acmRemote, acmLocal, buffLen, nullptr);

					_messageHookHandle = ::SetWindowsHookEx(WH_CALLWNDPROC, &MessageHookProc, hinstDLL, threadID);

					if (_messageHookHandle)
					{
						LogMessage("SetWindowsHookEx successful", true);
						SendMessage((HWND)windowHandle.ToPointer(), WM_GOBABYGO, (WPARAM)acmRemote, 0);
						UnhookWindowsHookEx(_messageHookHandle);
					}

					VirtualFreeEx(hProcess, acmRemote, 0, MEM_RELEASE);
				}

				CloseHandle(hProcess);
			}
		}
		FreeLibrary(hinstDLL);
	}
}

void Injector::LogMessage(System::String^ message, bool append)
{
	System::String^ applicationDataPath = System::Environment::GetFolderPath(System::Environment::SpecialFolder::ApplicationData);
	applicationDataPath += "\\NetInjector";

	if (!System::IO::Directory::Exists(applicationDataPath))
	{
		System::IO::Directory::CreateDirectory(applicationDataPath);
	}

	System::String^ pathname = applicationDataPath + "\\Log.txt";

	if (!append)
	{
		System::IO::File::Delete(pathname);
	}

	System::IO::FileInfo^ fi = gcnew System::IO::FileInfo(pathname);

	System::IO::StreamWriter^ sw = fi->AppendText();
	sw->WriteLine(System::DateTime::Now.ToString("MM/dd/yyyy HH:mm:ss") + " : " + message);
	sw->Close();
}


__declspec(dllexport)
LRESULT MessageHookProc(int nCode, WPARAM wparam, LPARAM lparam)
{
	if (nCode == HC_ACTION)
	{
		const auto msg = (CWPSTRUCT*)lparam;
		if (msg != nullptr && msg->message == WM_GOBABYGO)
		{
			try
			{
				if (msg->wParam == 0)
					throw gcnew ArgumentException("The injector request is empty.");
				const auto acmLocal = gcnew String((wchar_t*)msg->wParam);
				auto serializer = gcnew System::Xml::Serialization::XmlSerializer(InjectorData::typeid);
				auto reader = gcnew StringReader(acmLocal);
				InjectorData^ injectorData;
				try
				{
					injectorData = safe_cast<InjectorData^>(serializer->Deserialize(reader));
				}
				finally
				{
					delete reader;
				}
				Injector::LoadIntoAppDomain(injectorData);
			}
			catch (Exception^ exception)
			{
				const auto errorMessage = String::Format(
					"NetInjector could not invoke the requested entry point.\n\n{0}", exception);
				try
				{
					Injector::LogMessage(errorMessage, true);
				}
				catch (Exception^ logException)
				{
					System::Diagnostics::Debug::WriteLine(logException);
				}

				const pin_ptr<const wchar_t> pinnedErrorMessage = PtrToStringChars(errorMessage);
				::MessageBoxW(msg->hwnd, pinnedErrorMessage,
					L"NetInjector - Injection failed", MB_OK | MB_ICONERROR | MB_TOPMOST);
			}
		}
	}

	return CallNextHookEx(_messageHookHandle, nCode, wparam, lparam);
}
