#pragma once
#include "IL2cppNativeApi.h"
#include "il2cpp.h"
#include "IL2CppWrapper.h"
#include "IL2CppMethodInfoWrapper.h"
#include "IL2CppFieldInfoWrapper.h"
#include "IL2CppPropertyInfoWrapper.h"
#include "IL2CppTypeWrapper.h"
#include "IL2CppNativeHelper.h"

using namespace IL2CppApi;
using namespace Native;

namespace IL2CppApi
{
	namespace Wrappers
	{
		public ref class IL2CppClassWrapper : IL2CppWrapper<Il2CppClass>
		{
		public:
			property String^ Name
			{
				String^ get()
				{
					return gcnew String(IL2cppNativeApi::il2cpp_class_get_name(_native_pointer));
				}
			}

			property String^ Namespace
			{
				String^ get()
				{
					return gcnew String(IL2cppNativeApi::il2cpp_class_get_namespace(_native_pointer));
				}
			}

			property Boolean IsValueType
			{
				Boolean get()
				{

					return IL2cppNativeApi::il2cpp_class_is_valuetype(_native_pointer);
				}
			}

			property Boolean IsGeneric
			{
				Boolean get()
				{

					return IL2cppNativeApi::il2cpp_class_is_generic(_native_pointer);
				}
			}

			property Boolean IsInflated
			{
				Boolean get()
				{

					return IL2cppNativeApi::il2cpp_class_is_inflated(_native_pointer);
				}
			}

			property Int32 ValueSize
			{
				Int32 get()
				{

					uint32_t align;
					return Int32(IL2cppNativeApi::il2cpp_class_value_size(_native_pointer, &align));
				}
			}

			property Int32 ValueAlign
			{
				Int32 get()
				{

					uint32_t align;
					IL2cppNativeApi::il2cpp_class_value_size(_native_pointer, &align);
					return Int32(align);
				}
			}

			property IL2CppTypeWrapper^ Type
			{
				IL2CppTypeWrapper^ get()
				{

					return gcnew IL2CppTypeWrapper(IL2cppNativeApi::il2cpp_class_get_type(_native_pointer));
				}
			}

			property IL2CppTypeWrapper^ BaseType
			{
				IL2CppTypeWrapper^ get()
				{

					return gcnew IL2CppTypeWrapper(IL2cppNativeApi::il2cpp_class_enum_basetype(_native_pointer));
				}
			}

			property IL2CppClassWrapper^ DeclaringType
			{
				IL2CppClassWrapper^ get()
				{

					return gcnew IL2CppClassWrapper(IL2cppNativeApi::il2cpp_class_get_declaring_type(_native_pointer));
				}
			}

			property IL2CppClassWrapper^ Parent
			{
				IL2CppClassWrapper^ get()
				{

					return gcnew IL2CppClassWrapper(IL2cppNativeApi::il2cpp_class_get_parent(_native_pointer));
				}
			}

			property Reflection::TypeAttributes Attributes
			{
				Reflection::TypeAttributes get()
				{
					return static_cast<Reflection::TypeAttributes>(IL2cppNativeApi::il2cpp_class_get_flags(_native_pointer));
				}
			}

			property UInt32 Token
			{
				UInt32 get()
				{
					return UInt32(IL2cppNativeApi::il2cpp_class_get_type_token(_native_pointer));
				}
			}

			property Int32 Rank
			{
				Int32 get()
				{
					return Int32(IL2cppNativeApi::il2cpp_class_get_rank(_native_pointer));
				}
			}

			array<IL2CppClassWrapper^>^ GetNestedTypes()
			{
				const auto nested_types = gcnew Collections::Generic::List<IL2CppClassWrapper^>();

				void* iter = nullptr;
				Il2CppClass* klass;

				while ((klass = IL2cppNativeApi::il2cpp_class_get_nested_types(_native_pointer, &iter)))
					nested_types->Add(gcnew IL2CppClassWrapper(klass));

				return nested_types->ToArray();
			}

			array<IL2CppClassWrapper^>^ GetInterfaces()
			{
				const auto interfaces = gcnew Collections::Generic::List<IL2CppClassWrapper^>();

				void* iter = nullptr;
				Il2CppClass* klass;

				while ((klass = IL2cppNativeApi::il2cpp_class_get_interfaces(_native_pointer, &iter)))
					interfaces->Add(gcnew IL2CppClassWrapper(klass));

				return interfaces->ToArray();
			}

			array<IL2CppMethodInfoWrapper^>^ GetMethods()
			{
				const auto method_list = gcnew Collections::Generic::List<IL2CppMethodInfoWrapper^>();

				void* iter = nullptr;
				MethodInfo* method;

				while ((method = IL2cppNativeApi::il2cpp_class_get_methods(_native_pointer, &iter)))
					method_list->Add(gcnew IL2CppMethodInfoWrapper(method));

				return method_list->ToArray();
			}

			array<IL2CppFieldInfoWrapper^>^ GetFields()
			{
				const auto field_list = gcnew Collections::Generic::List<IL2CppFieldInfoWrapper^>();

				void* iter = nullptr;
				FieldInfo* field;

				while ((field = IL2cppNativeApi::il2cpp_class_get_fields(_native_pointer, &iter)))
					field_list->Add(gcnew IL2CppFieldInfoWrapper(field));

				return field_list->ToArray();
			}

			array<IL2CppPropertyInfoWrapper^>^ GetProperties()
			{
				const auto property_list = gcnew Collections::Generic::List<IL2CppPropertyInfoWrapper^>();

				void* iter = nullptr;
				PropertyInfo* property;

				while ((property = IL2cppNativeApi::il2cpp_class_get_properties(_native_pointer, &iter)))
					property_list->Add(gcnew IL2CppPropertyInfoWrapper(property));

				return property_list->ToArray();
			}

			array<IL2CppTypeWrapper^>^ GetGenericParameters()
			{
				if (!IsInflated)
					return nullptr;

				const auto generic_class = Il2CppClassGetGenericClass(_native_pointer);

				if (!generic_class)
					return nullptr;
					//throw gcnew Exception("Il2CppClass->generic_class is null, please make sure your struct definitions are valid");

				const auto generic_inst = generic_class->context.class_inst;
				const auto parameters = gcnew array<IL2CppTypeWrapper^>(generic_inst->type_argc);

				for(auto i = 0u; i < generic_inst->type_argc; i++)
				{
					parameters[i] = gcnew IL2CppTypeWrapper(generic_inst->type_argv[i]);
				}

				return parameters;
			}

			Boolean HasAttribute(IL2CppClassWrapper^ attr_class)
			{
				return IL2cppNativeApi::il2cpp_class_has_attribute(_native_pointer, attr_class->_native_pointer);
			}

			IL2CppClassWrapper^ GetElementClass()
			{
				return gcnew IL2CppClassWrapper(IL2cppNativeApi::il2cpp_class_get_element_class(_native_pointer));
			}

			Int32 GetArrayElementSize()
			{
				return Int32(IL2cppNativeApi::il2cpp_array_element_size(_native_pointer));
			}

			IL2CppClassWrapper^ GetGenericTypeDefinition()
			{
				if (!IsInflated)
					return nullptr;

				const auto generic_class = Il2CppClassGetGenericClass(_native_pointer);

				if (!generic_class)
					return nullptr;

				return gcnew IL2CppClassWrapper(IL2cppNativeApi::GenericClass_GetTypeDefinition(generic_class));
			}

			static IL2CppClassWrapper^ GetClassFromType(IL2CppTypeWrapper^ type)
			{
				//return gcnew IL2CppClassWrapper(IL2cppNativeApi::il2cpp_type_get_class_or_element_class(type->_native_pointer));
				//data.type

				return gcnew IL2CppClassWrapper(IL2cppNativeApi::il2cpp_class_from_il2cpp_type(type->_native_pointer));
			}

			static IL2CppClassWrapper^ FromPointer(IntPtr pointer)
			{
				if (pointer == IntPtr::Zero) return nullptr;
				return gcnew IL2CppClassWrapper(static_cast<Il2CppClass*>(pointer.ToPointer()));
			}

			static bool MethodHasAttribute(IL2CppMethodInfoWrapper^ method, IL2CppClassWrapper^ attr_class)
			{
				return IL2cppNativeApi::il2cpp_method_has_attribute(method->_native_pointer, attr_class->_native_pointer);
			}


			static bool FieldHasAttribute(IL2CppFieldInfoWrapper^ field, IL2CppClassWrapper^ attr_class)
			{
				return IL2cppNativeApi::il2cpp_field_has_attribute(field->_native_pointer, attr_class->_native_pointer);
			}

			static IL2CppClassWrapper^ GetMethodDeclaringType(IL2CppMethodInfoWrapper^ method)
			{
				return gcnew IL2CppClassWrapper(IL2cppNativeApi::il2cpp_method_get_declaring_type(method->_native_pointer));
			}

			static IL2CppClassWrapper^ GetFieldParent(IL2CppFieldInfoWrapper^ field)
			{
				return gcnew IL2CppClassWrapper(IL2cppNativeApi::il2cpp_field_get_parent(field->_native_pointer));
			}

		internal:
			IL2CppClassWrapper(Il2CppClass* native_ptr) : IL2CppWrapper(native_ptr) {}
		};
	}
}
