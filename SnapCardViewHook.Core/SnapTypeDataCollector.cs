using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IL2CppApi.Wrappers;
using SnapCardViewHook.Core.Helpers;
using SnapCardViewHook.Core.IL2Cpp;
// ReSharper disable InconsistentNaming

namespace SnapCardViewHook.Core
{
    public static unsafe class SnapTypeDataCollector
    {
        // namespace and assembly constants
        private static class Constants
        {
            public const string Dll_SecondDinner_CubeDef = "SecondDinner.CubeDef.dll";
            public const string Namespace_CubeDef = "CubeDef";
            //
            public const string Dll_App_View = "App.View.dll";
            public const string Namespace_CubeUnity_App_View = "CubeUnity.App.View";
            //
            public const string Namespace_CubeDef_DefData = "CubeDef.DefData";
            //
            public const string Dll_App_Game = "App.Game.dll";
        }

        //
        // delegate type definitions
        public delegate IntPtr CardDefList_Find_delegate_(IntPtr cardDef);
        public delegate IntPtr CardToArtVariantDefList_Find_delegate_(IntPtr artVariantDefId);
        public delegate IntPtr ptr_void__delegate_();

        public delegate void CardDetailsCardView_FlipCard_delegate_(IntPtr thisPtr, bool flipped, float overrideDuration);

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        public delegate void void_this__delegate_(IntPtr thisPtr);

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        public delegate void CardView_Initialize_delegate_(
            IntPtr thisPtr, IntPtr cardDef, int cost, int power, int rarity,
            IntPtr borderDefId, IntPtr artVariantDefId, IntPtr surfaceEffectDefId,
            IntPtr cardRevealEffectDefId, int cardRevealEffectType, bool showRevealEffectOnStart,
            int logoEffectId, IntPtr cardBackDefId, bool isMorph, bool setTransparentQueue);

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        public delegate void BoardView_LoadBoard_delegate_(IntPtr thisPtr, IntPtr p1);

        //
        // collected type data fields
        public static IL2CppFieldInfoWrapper[] CardDef_Id_Fields { get; private set; }
        public static IntPtr CardDefList_Find_methodPtr { get; private set; }
        public static IL2CppFieldInfoWrapper[] ArtVariantDef_Id_Fields { get; private set; }
        public static IL2CppFieldInfoWrapper[] SurfaceEffectDef_Id_Fields { get; private set; }
        public static IL2CppFieldInfoWrapper[] CardRevealEffectDef_Id_Fields { get; private set; }
        public static CardDefList_Find_delegate_ CardDefList_Find { get; private set; }
        public static int CardDef_Name_Field_Offset { get; private set; }
        public static int CardDef_CardDefId_Field_Offset { get; private set; }
        public static int CardDef_Power_Field_Offset { get; private set; }
        public static int CardDef_Cost_Field_Offset { get; private set; }
        public static int CardDef_Description_Field_Offset { get; private set; }
        public static int CardDef_SeriesStartDates_Field_Offset { get; private set; }
        public static int CardDef_Attributes_Field_Offset { get; private set; }
        public static IL2CppFieldInfoWrapper[] DataAttributeType_Fields { get; private set; }
        public static CardView_Initialize_delegate_ CardViewInitializeOriginal { get; private set; }
        public static CardToArtVariantDefList_Find_delegate_ CardToArtVariantDefList_Find { get; private set; }
        public static int CardToArtVariantDef_CardDefId_Field_Offset { get; private set; }
        public static int BorderDef_BorderDefId_Field_Offset { get; private set; }
        public static int BorderDef_Name_Field_Offset { get; private set; }
        public static ptr_void__delegate_ BorderDefList_get_Defs { get; private set; }
        public static IntPtr BorderDefList_Defs_cached_value { get; private set; }
        public static void_this__delegate_ UiVfxManagerRuntimeUpdateOriginal { get; private set; }
        public static void_this__delegate_ CardDetailsCardViewInitializeOriginal { get; private set; }
        public static IntPtr CardDetailsCardView_InstancePtr { get; private set; }
        public static CardDetailsCardView_FlipCard_delegate_ CardDetailsCardView_FlipCard { get; private set; }
        public static IL2CppFieldInfoWrapper[] CardBackDefId_Fields { get; private set; }
        public static IL2CppFieldInfoWrapper[] GameBoardDef_Id_Fields { get; private set; }
        public static BoardView_LoadBoard_delegate_ BoardViewLoadBoardOriginal { get; private set; }

        //
        // hooks
        public static CardView_Initialize_delegate_ CardViewInitializeHookOverride { get; set; }
        public static BoardView_LoadBoard_delegate_ BoardViewLoadBoardHookOverride { get; set; }
        public static bool Loaded { get; private set; }

        // 
        //
        private static readonly ConcurrentStack<Action> _uiThreadActions = new ConcurrentStack<Action>();

        //
        // GC fields
        private static CardView_Initialize_delegate_ _cache_detour_CardView_Initialize;
        private static void_this__delegate_ _cache_detour_UiVfxManager_RuntimeUpdate;
        private static void_this__delegate_ _cache_detour_CardDetailsCardView_Initialize;
        private static BoardView_LoadBoard_delegate_ _cache_detour_BoardView_LoadBoard;

        private static object initLockObj = new object();

        static SnapTypeDataCollector()
        {
            // ensure all methods get compiled
            JitHelper.PrepareAllMethods(typeof(SnapTypeDataCollector));
        }

        public static void EnsureLoaded()
        {
            lock(initLockObj)
            {
                if (Loaded)
                    return;

                CollectAllRequiredTypeData();

                Loaded = true;
            }
        }

        private static void CollectAllRequiredTypeData()
        {
            var assemblies = IL2CppApi.IL2CppDumper.GetLoadedAssemblies();

            Collect_CardDefId(assemblies);
            Collect_CardDefList(assemblies);
            Collect_ArtVariantDef(assemblies);
            Collect_SurfaceEffectDef(assemblies);
            Collect_CardRevealEffectDef(assemblies);
            Collect_CardView(assemblies);
            Collect_CardDef(assemblies);
            Collect_DataAttributeType(assemblies);
            Collect_CardToArtVariantDefList(assemblies);
            Collect_CardToArtVariantDef(assemblies);
            Collect_BorderDefList(assemblies);
            Collect_BorderDef(assemblies);
            Collect_UiVfxManager(assemblies);
            Collect_CardDetailsCardView(assemblies);
            Collect_CardBackDef(assemblies);
            Collect_GameBoardDef(assemblies);
            Collect_BoardView(assemblies);
        }

        private static IL2CppClassWrapper GetIL2CppClass(IL2CppImageWrapper[] assemblies, string assemblyName, string typeNameSpace, string typeName)
        {
            var assembly = assemblies.FirstOrDefault(a => a.Name == assemblyName);

            var @class =
                assembly?
                    .GetClasses()
                    .FirstOrDefault(c => c.Namespace == typeNameSpace && c.Name == typeName);

            return @class;
        }

        private static void ThrowIL2CppTypeError(string name)
        {
            throw new Exception($"IL2CppApi type error, could not locate type '{name}'");
        }

        private static void ThrowIL2CppMethodError(string name)
        {
            throw new Exception($"IL2CppApi type error, could not locate method '{name}'");
        }

        private static IL2CppClassWrapper TryGetIL2CppClass(IL2CppImageWrapper[] assemblies, string assemblyName,
            string @namespace, string typeName)
        {
            var @class = GetIL2CppClass(assemblies, assemblyName, @namespace, typeName);

            if (@class == null)
            {
                ThrowIL2CppTypeError($"{@namespace}.{typeName}");
                return null;
            }

            return @class;
        }

        private static void Collect_CardDefId(IL2CppImageWrapper[] assemblies)
        {
            CardDef_Id_Fields =
                GetIdClassFields(assemblies, Constants.Dll_SecondDinner_CubeDef, Constants.Namespace_CubeDef,
                    "CardDef");
        }

        private static void Collect_CardDefList(IL2CppImageWrapper[] assemblies)
        {
            const string className = "CardDefList";
            const string methodName = "Find";

            var cardDefListClass = TryGetIL2CppClass(assemblies, Constants.Dll_SecondDinner_CubeDef, Constants.Namespace_CubeDef, className);
            var method = cardDefListClass.GetMethods().FirstOrDefault(m => m.Name == methodName);

            if (method == null)
            {
                ThrowIL2CppMethodError($"{className}::{methodName}");
                return;
            }

            CardDefList_Find_methodPtr = method.MethodPointer;
            CardDefList_Find = Marshal.GetDelegateForFunctionPointer<CardDefList_Find_delegate_>(CardDefList_Find_methodPtr);
        }

        private static IL2CppFieldInfoWrapper[] GetIdClassFields(IL2CppImageWrapper[] assemblies, string assemblyName,
            string @namespace, string typeName)
        {
            const string class_name_Def_Id = "Id";

            var defClass = TryGetIL2CppClass(assemblies, assemblyName, @namespace, typeName);
            var defClass_Id = defClass.GetNestedTypes().FirstOrDefault(c => c.Name == class_name_Def_Id);

            if (defClass_Id == null)
            {
                ThrowIL2CppTypeError($"{@namespace}.{typeName}.{class_name_Def_Id}");
                return null;
            }

            return defClass_Id.GetFields();
        }

        private static void Collect_ArtVariantDef(IL2CppImageWrapper[] assemblies)
        {
            ArtVariantDef_Id_Fields =
                GetIdClassFields(assemblies, Constants.Dll_SecondDinner_CubeDef, Constants.Namespace_CubeDef, "ArtVariantDef");
        }

        private static void Collect_SurfaceEffectDef(IL2CppImageWrapper[] assemblies)
        {
            SurfaceEffectDef_Id_Fields =
                GetIdClassFields(assemblies, Constants.Dll_SecondDinner_CubeDef, Constants.Namespace_CubeDef, "SurfaceEffectDef");
        }
        private static void Collect_CardRevealEffectDef(IL2CppImageWrapper[] assemblies)
        {
            CardRevealEffectDef_Id_Fields =
                GetIdClassFields(assemblies, Constants.Dll_SecondDinner_CubeDef, Constants.Namespace_CubeDef, "CardRevealEffectDef");
        }

        private static unsafe void Collect_CardView(IL2CppImageWrapper[] assemblies)
        {
            const string className = "CardView";
            const string methodName = "Initialize";

            var cardViewClass = TryGetIL2CppClass(assemblies, Constants.Dll_App_View, Constants.Namespace_CubeUnity_App_View, className);
            var method = cardViewClass
                .GetMethods()
                .Where(m => m.Name == methodName)
                .OrderByDescending(m => m.ParamCount)
                .FirstOrDefault();

            if (method == null)
            {
                ThrowIL2CppMethodError($"{className}::{methodName}");
                return;
            }

            void* originalPtr;
            // store delegate into class to avoid garbage collection
            // alternatively use GCHandle.Alloc
            var detourDelegate = _cache_detour_CardView_Initialize = new CardView_Initialize_delegate_(CardView_Initialize_Detour);

            if (!HookHelper.CreateHook(
                    (void*)method.MethodPointer,
                    (void*)Marshal.GetFunctionPointerForDelegate(detourDelegate),
                    &originalPtr))
                throw new Exception("CreateHook failed");

            CardViewInitializeOriginal = (CardView_Initialize_delegate_)
                Marshal.GetDelegateForFunctionPointer(new IntPtr(originalPtr), typeof(CardView_Initialize_delegate_));
        }


        private static IL2CppFieldInfoWrapper TryGetField(IL2CppClassWrapper @class, string fieldName)
        {
            var field = @class.GetFields().FirstOrDefault(f => f.Name == fieldName);

            if (field == null)
                throw new Exception($"Field {@class.Name}.{fieldName} could not be found");

            return field;
        }

        private static void Collect_CardDef(IL2CppImageWrapper[] assemblies)
        {
            var cardDefIdClass = TryGetIL2CppClass(assemblies, Constants.Dll_SecondDinner_CubeDef, Constants.Namespace_CubeDef, "CardDef");

            CardDef_Name_Field_Offset = TryGetField(cardDefIdClass, "<Name>k__BackingField").Offset.ToInt32();
            CardDef_CardDefId_Field_Offset = TryGetField(cardDefIdClass, "<CardDefId>k__BackingField").Offset.ToInt32();
            CardDef_Power_Field_Offset = TryGetField(cardDefIdClass, "<Power>k__BackingField").Offset.ToInt32();
            CardDef_Cost_Field_Offset = TryGetField(cardDefIdClass, "<Cost>k__BackingField").Offset.ToInt32();
            CardDef_Description_Field_Offset = TryGetField(cardDefIdClass, "<Description>k__BackingField").Offset.ToInt32();
            CardDef_SeriesStartDates_Field_Offset = TryGetField(cardDefIdClass, "<SeriesStartDates>k__BackingField").Offset.ToInt32();
            // !! hardcoded value
            CardDef_Attributes_Field_Offset = 0x10; //TryGetField(cardDefIdClass, "<Attributes>k__BackingField").Offset.ToInt32();
        }

        private static void Collect_DataAttributeType(IL2CppImageWrapper[] assemblies)
        {
            var cardDefIdClass = TryGetIL2CppClass(assemblies, Constants.Dll_SecondDinner_CubeDef, Constants.Namespace_CubeDef, "DataAttributeType");
            DataAttributeType_Fields = cardDefIdClass.GetFields().Skip(1).ToArray();
        }

        private static void Collect_CardToArtVariantDefList(IL2CppImageWrapper[] assemblies)
        {
            const string className = "CardToArtVariantDefList";
            const string methodName = "Find";

            var cardToArtVariantDefListClass = TryGetIL2CppClass(assemblies, Constants.Dll_SecondDinner_CubeDef, Constants.Namespace_CubeDef, className);
            var method = cardToArtVariantDefListClass
                .GetMethods()
                .FirstOrDefault(f => f.Name == methodName && f.GetParamName(0) == "artVariantDefId");

            if (method == null)
            {
                ThrowIL2CppMethodError($"{className}.{methodName}");
                return;
            }

            CardToArtVariantDefList_Find =
                Marshal.GetDelegateForFunctionPointer<CardToArtVariantDefList_Find_delegate_>(method.MethodPointer);
        }

        private static void Collect_CardToArtVariantDef(IL2CppImageWrapper[] assemblies)
        {
            var cardToArtVariantDefClass = TryGetIL2CppClass(assemblies, Constants.Dll_SecondDinner_CubeDef, Constants.Namespace_CubeDef, "CardToArtVariantDef");

            var fieldCardDefId = TryGetField(cardToArtVariantDefClass, "<CardDefId>k__BackingField");
            CardToArtVariantDef_CardDefId_Field_Offset = fieldCardDefId.Offset.ToInt32();
        }

        private static void Collect_BorderDefList(IL2CppImageWrapper[] assemblies)
        {
            const string className = "BorderDefList";
            var borderDefListClass = TryGetIL2CppClass(assemblies, Constants.Dll_SecondDinner_CubeDef, Constants.Namespace_CubeDef_DefData, className);

            var method = borderDefListClass.GetMethods().FirstOrDefault(m => m.Name == "get_DefIds");

            BorderDefList_get_Defs = Marshal.GetDelegateForFunctionPointer<ptr_void__delegate_>(method.MethodPointer);
            BorderDefList_Defs_cached_value = BorderDefList_get_Defs();
        }

        private static void Collect_BorderDef(IL2CppImageWrapper[] assemblies)
        {
            var borderDefClass = TryGetIL2CppClass(assemblies, Constants.Dll_SecondDinner_CubeDef, Constants.Namespace_CubeDef, "BorderDef");

            var fieldBorderDefId = TryGetField(borderDefClass, "<BorderDefId>k__BackingField");
            BorderDef_BorderDefId_Field_Offset = fieldBorderDefId.Offset.ToInt32();

            var fieldName = TryGetField(borderDefClass, "<Name>k__BackingField");
            BorderDef_Name_Field_Offset = fieldName.Offset.ToInt32();
        }


        private static void Collect_UiVfxManager(IL2CppImageWrapper[] assemblies)
        {
            var uiVfxManagerClass = TryGetIL2CppClass(assemblies, Constants.Dll_App_View, Constants.Namespace_CubeUnity_App_View, "UiVfxManager");
            var method = uiVfxManagerClass
                .GetMethods()
                .FirstOrDefault(f => f.Name == "RuntimeUpdate");

            if (method == null)
            {
                ThrowIL2CppMethodError("UiVfxManager.RuntimeUpdate");
                return;
            }

            void* originalPtr;
            var detourDelegate = _cache_detour_UiVfxManager_RuntimeUpdate = new void_this__delegate_(UiVfxManager_RuntimeUpdate_Detour);

            if (!HookHelper.CreateHook(
                    (void*)method.MethodPointer,
                    (void*)Marshal.GetFunctionPointerForDelegate(detourDelegate),
                    &originalPtr))
                throw new Exception("CreateHook for UiVfxManager failed");

            UiVfxManagerRuntimeUpdateOriginal = (void_this__delegate_)
                Marshal.GetDelegateForFunctionPointer(new IntPtr(originalPtr), typeof(void_this__delegate_));
        }

        private static void Collect_CardDetailsCardView(IL2CppImageWrapper[] assemblies)
        {
            var uiVfxManagerClass = TryGetIL2CppClass(assemblies, Constants.Dll_App_View, Constants.Namespace_CubeUnity_App_View, "CardDetailsCardView");
            var method = uiVfxManagerClass
                .GetMethods()
                .FirstOrDefault(f => f.Name == "Initialize");

            if (method == null)
            {
                ThrowIL2CppMethodError("CardDetailsCardView.Initialize");
                return;
            }

            void* originalPtr;
            var detourDelegate = _cache_detour_CardDetailsCardView_Initialize = new void_this__delegate_(CardDetailsCardView_Initialize_Detour);

            if (!HookHelper.CreateHook(
                    (void*)method.MethodPointer,
                    (void*)Marshal.GetFunctionPointerForDelegate(detourDelegate),
                    &originalPtr))
                throw new Exception("CreateHook for CardDetailsCardView failed");

            CardDetailsCardViewInitializeOriginal = (void_this__delegate_)
                Marshal.GetDelegateForFunctionPointer(new IntPtr(originalPtr), typeof(void_this__delegate_));


            var flipCardMethod = uiVfxManagerClass
                .GetMethods()
                .FirstOrDefault(f => f.Name == "FlipCard");

            if (flipCardMethod == null)
            {
                ThrowIL2CppMethodError("CardDetailsCardView.FlipCard");
                return;
            }

            CardDetailsCardView_FlipCard = (CardDetailsCardView_FlipCard_delegate_)
                Marshal.GetDelegateForFunctionPointer(flipCardMethod.MethodPointer, typeof(CardDetailsCardView_FlipCard_delegate_));
        }

        private static void Collect_CardBackDef(IL2CppImageWrapper[] assemblies)
        {
            CardBackDefId_Fields =
              GetIdClassFields(assemblies, Constants.Dll_SecondDinner_CubeDef, Constants.Namespace_CubeDef, "CardBackDef");
        }

        private static void Collect_GameBoardDef(IL2CppImageWrapper[] assemblies)
        {
            GameBoardDef_Id_Fields =
               GetIdClassFields(assemblies, Constants.Dll_SecondDinner_CubeDef, Constants.Namespace_CubeDef, "GameBoardDef");
        }

        private static void Collect_BoardView(IL2CppImageWrapper[] assemblies)
        {
            var boardViewClass = TryGetIL2CppClass(assemblies, Constants.Dll_App_Game, string.Empty, "BoardViewLoader");
            var method = boardViewClass
               .GetMethods()
               .FirstOrDefault(f => f.Name == "LoadBoard");

            if (method == null)
            {
                ThrowIL2CppMethodError("BoardView.LoadBoard");
                return;
            }

            void* originalPtr;
            var detourDelegate = _cache_detour_BoardView_LoadBoard = new BoardView_LoadBoard_delegate_(BoardView_LoadBoard_Detour);

            if (!HookHelper.CreateHook(
                    (void*)method.MethodPointer,
                    (void*)Marshal.GetFunctionPointerForDelegate(detourDelegate),
                    &originalPtr))
                throw new Exception("CreateHook for LoadBoard failed");

            BoardViewLoadBoardOriginal = (BoardView_LoadBoard_delegate_)
                Marshal.GetDelegateForFunctionPointer(new IntPtr(originalPtr), typeof(BoardView_LoadBoard_delegate_));
        }

        //
        // detours
        //
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CardDetailsCardView_Initialize_Detour(IntPtr thisPtr)
        {
            CardDetailsCardView_InstancePtr = thisPtr;
            CardDetailsCardViewInitializeOriginal(thisPtr);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UiVfxManager_RuntimeUpdate_Detour(IntPtr thisPtr)
        {
            if (_uiThreadActions.TryPop(out var callback))
                callback();

            UiVfxManagerRuntimeUpdateOriginal(thisPtr);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CardView_Initialize_Detour(
            IntPtr thisPtr, IntPtr cardDef, int cost, int power, int rarity,
            IntPtr borderDefId, IntPtr artVariantDefId, IntPtr surfaceEffectDefId,
            IntPtr cardRevealEffectDefId, int cardRevealEffectType, bool showRevealEffectOnStart,
            int logoEffectId, IntPtr cardBackDefId, bool isMorph, bool setTransparentQueue)
        {
            var @delegate = CardViewInitializeHookOverride ?? CardViewInitializeOriginal;

            @delegate(thisPtr, cardDef, cost, power, rarity, borderDefId, artVariantDefId,
                surfaceEffectDefId, cardRevealEffectDefId, cardRevealEffectType, showRevealEffectOnStart,
                logoEffectId,
                cardBackDefId, isMorph, setTransparentQueue);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BoardView_LoadBoard_Detour(IntPtr thisPtr, IntPtr p1)
        {
            var @delegate = BoardViewLoadBoardHookOverride ?? BoardViewLoadBoardOriginal;

            @delegate(thisPtr, p1);
        }

        //
        // API
        //
        public static void ExecuteActionInGameUiThread(Action callback)
        {
            _uiThreadActions.Push(callback);
        }
    }
}
