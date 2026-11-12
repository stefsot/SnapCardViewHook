using IL2CppApi.Runtime;
using IL2CppApi.Wrappers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace SnapCardViewHook.Core.AssetLab
{
    internal sealed class AssetLabPreview : IDisposable
    {
        private const int BakedLoadOptions = 1 | 2 | 8;
        private const int ForceBakedLoadOption = 2;
        private const int IgnoreQualityLoadOption = 16;
        private const int PreviewRarity = 7;
        private const int BakedPreviewRarity = 1;
        private readonly Il2CppRuntime _roots = new Il2CppRuntime();
        private readonly Dictionary<IntPtr, ViewState> _views = new Dictionary<IntPtr, ViewState>();
        private readonly Dictionary<IntPtr, string> _descriptions = new Dictionary<IntPtr, string>();
        private readonly IntPtr _card, _render;
        private readonly IL2CppClassWrapper _viewType;
        private readonly IL2CppFieldInfoWrapper _renderOverride, _renderAddress, _loadOptions;
        private readonly string _description;
        private readonly string _missingCardId;
        private readonly int _cost, _power;
        private readonly bool _baked;
        private bool _disposed;

        public AssetLabPreview(AssetLabScan scan, AssetLabEntry entry, string name, string description, int cost, int power, AssetLabBake bake)
        {
            try
            {
                var baked = bake != null;
                _baked = baked;
                if (!(baked ? entry.CanPreviewBaked && entry.Bakes.Contains(bake) : entry.CanPreviewMaterials))
                    throw new InvalidOperationException("The selected preview mode is unavailable. " + entry.Availability);
                var template = scan.FindRenderTemplate(entry);
                if (template == IntPtr.Zero)
                    throw new InvalidOperationException("Open any card in the game, wait for it to appear, then click Preview selected again. Your scan results are kept; no rescan is needed.");
                var resources = baked ? bake.Textures : entry.Materials;
                foreach (var resource in resources.Values)
                {
                    if (!resource.Local || resource.Bundles.Any(path => !System.IO.File.Exists(path)))
                        throw new InvalidOperationException("A required local bundle is missing. Scan again before previewing this asset.");
                    if (!Guid.TryParse(resource.Key, out _))
                        throw new InvalidOperationException("A required asset has no usable GUID. Scan again before previewing this asset.");
                }
                _viewType = AssetLabNative.ViewType(_roots, "CardView");
                _renderOverride = _roots.Field(_viewType, "_overrideCardRenderDef");
                _renderAddress = _roots.Field(_viewType, "_renderDefAddress");
                _loadOptions = _roots.Field(_viewType, "_loadOptions");
                _description = description ?? string.Empty;
                _cost = cost;
                _power = power;
                var cardType = AssetLabNative.DefType(_roots, "CardDef");
                if (entry.CardDefinition != IntPtr.Zero) _card = AssetLabNative.Clone(_roots, entry.CardDefinition);
                else
                {
                    _card = _roots.NewObject(cardType);
                    _roots.Invoke(_roots.Method(cardType, ".ctor", false), _card);
                    _missingCardId = entry.CardId;
                    AssetLabDefinitions.EnsureInstalled();
                }
                _roots.WriteReference(_card, _roots.Field(cardType, "<Name>k__BackingField"), _roots.String(name));
                _roots.WriteReference(_card, _roots.Field(cardType, "<Description>k__BackingField"), _roots.String(_description));
                AssetLabNative.SetId(_roots, _card, "CardDefId", entry.CardId);
                _roots.Invoke(_roots.Method(cardType, "set_Cost", false, "System.Int32"), _card, Il2CppArgument.Value(cost));
                _roots.Invoke(_roots.Method(cardType, "set_Power", false, "System.Int32"), _card, Il2CppArgument.Value(power));

                _render = AssetLabNative.Clone(_roots, template);
                var renderType = AssetLabNative.RenderType(_roots, "CardRenderDefSlim");
                _roots.WriteReference(_render, _roots.Field(renderType, "name"), _roots.String(entry.AssetId));
                var referenceType = _roots.Class("Unity.Addressables.dll", "UnityEngine.AddressableAssets", "AssetReference");
                var emptyReference = CreateReference(referenceType, string.Empty);
                foreach (var field in renderType.GetFields())
                    if (field.Name.EndsWith("Reference", StringComparison.Ordinal) && field.Type.Name.Contains("AssetReference"))
                        _roots.WriteReference(_render, field, emptyReference);
                foreach (var resource in resources)
                {
                    var reference = CreateReference(referenceType, resource.Value.Key);
                    _roots.WriteReference(_render, _roots.Field(renderType, baked
                        ? "_Baked" + resource.Key + "TextureReference" : "_" + resource.Key + "MaterialReference"),
                        reference);
                    if (baked && resource.Key != "Logo" && (resource.Value.Priority & 1) != 0)
                        _roots.WriteReference(_render, _roots.Field(renderType, "_BakedCommon" + resource.Key + "TextureReference"), reference);
                }
                foreach (var role in new[] { "Foreground", "ForegroundScreen", "Background" })
                {
                    var field = _roots.Field(renderType, "_HasBaked" + role + "CommonTextures", true);
                    if (field != null)
                    {
                        _roots.ReadField<bool>(_render, field);
                        var common = baked && resources.TryGetValue(role, out var resource) && (resource.Priority & 1) != 0;
                        Marshal.WriteByte(_render, field.Offset.ToInt32(), common ? (byte)1 : (byte)0);
                    }
                }
            }
            catch { _roots.Dispose(); throw; }
        }

        private IntPtr CreateReference(IL2CppClassWrapper type, string key)
        {
            var reference = _roots.NewObject(type);
            _roots.Invoke(_roots.Method(type, ".ctor", false, "System.String"), reference, Il2CppArgument.Ref(_roots.String(key)));
            return reference;
        }

        public void Activate()
        {
            if (_missingCardId != null) AssetLabDefinitions.Set(this, _missingCardId, _card);
        }

        public void Initialize(ViewState incoming)
        {
            using (var runtime = new Il2CppRuntime())
            {
                incoming.Root(runtime, _renderOverride, _loadOptions);
                if (_views.TryGetValue(incoming.View, out var previous))
                {
                    if (previous.InstanceId == incoming.InstanceId)
                    {
                        incoming.InheritOriginal(previous);
                    }
                    previous.Dispose();
                }
                _views[incoming.View] = incoming;
                runtime.WriteReference(incoming.View, _renderOverride, _render);
                runtime.WriteReference(incoming.View, _renderAddress, IntPtr.Zero);
                Marshal.WriteInt32(incoming.View, _loadOptions.Offset.ToInt32(),
                    (incoming.OriginalLoadOptions & ~BakedLoadOptions) | IgnoreQualityLoadOption | (_baked ? ForceBakedLoadOption : 0));
                TrackDescription(incoming.View);
                incoming.Invoke(_card, _cost, _power, _baked ? BakedPreviewRarity : PreviewRarity, IntPtr.Zero);
                TrackDescription(incoming.View);
            }
        }

        private void TrackDescription(IntPtr view)
        {
            var instance = Marshal.ReadIntPtr(view, SnapTypeDataCollector.CardView_LocalizeDescriptionEvent_Field_Offset);
            if (instance != IntPtr.Zero) _descriptions[instance] = _description;
        }

        public string Description(IntPtr descriptionEvent) =>
            _descriptions.TryGetValue(descriptionEvent, out var description) ? description : null;

        public static bool RefreshOpenCard()
        {
            using (var runtime = new Il2CppRuntime())
            {
                var detailsType = AssetLabNative.ViewType(runtime, "CardDetailsCardView");
                var details = SnapTypeDataCollector.CardDetailsCardView_InstancePtr;
                if (!AssetLabNative.Alive(runtime, details)) return false;
                if (!runtime.InvokeValue<bool>(runtime.Method(detailsType, "get_IsVisible", false), details)) return false;
                var entity = runtime.Value<DetailsEntity>(runtime.Invoke(runtime.Method(detailsType, "get_CurrentEntity", false), details));
                if (!AssetLabNative.Alive(runtime, entity.View) || entity.Card == IntPtr.Zero) return false;
                var component = AssetLabNative.EngineType(runtime, "Component");
                var viewType = AssetLabNative.ViewType(runtime, "CardView");
                var card = runtime.Invoke(runtime.Method(component, "GetComponent", false, "System.Type"), entity.View,
                    Il2CppArgument.Ref(runtime.TypeObject(viewType)));
                if (!AssetLabNative.Alive(runtime, card)) return false;
                var initialize = viewType.GetMethods().Single(m => m.Name == "Initialize" && m.ParamCount == 12 &&
                    m.GetParameters()[0].Name.EndsWith(".ICard", StringComparison.Ordinal));
                var options = runtime.ReadField<int>(card, runtime.Field(viewType, "_loadOptions"));
                runtime.Invoke(initialize, card, Il2CppArgument.Ref(entity.Card), Il2CppArgument.Value(2),
                    Il2CppArgument.Value(false), Il2CppArgument.Value(default(OptionalRarity)),
                    Il2CppArgument.Value(IntPtr.Zero), Il2CppArgument.Value(options), Il2CppArgument.Value(false),
                    Il2CppArgument.Value(false), Il2CppArgument.Value(IntPtr.Zero), Il2CppArgument.Value(IntPtr.Zero),
                    Il2CppArgument.Value(false), Il2CppArgument.Value(false));
                return true;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DetailsEntity
        {
            public IntPtr View, Card;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OptionalRarity
        {
            public byte HasValue;
            public int Value;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _descriptions.Clear();
            try
            {
                using (var runtime = new Il2CppRuntime())
                    foreach (var view in _views.Values)
                    {
                        try
                        {
                            if (!view.IsAlive(runtime)) continue;
                            runtime.WriteReference(view.View, _renderOverride, view.OriginalRender);
                            runtime.WriteReference(view.View, _renderAddress, IntPtr.Zero);
                            Marshal.WriteInt32(view.View, _loadOptions.Offset.ToInt32(), view.OriginalLoadOptions);
                            view.Restore();
                        }
                        catch (Exception error) { Debug.WriteLine("Asset Lab restore: " + error); }
                        finally { view.Dispose(); }
                    }
            }
            finally
            {
                AssetLabDefinitions.Clear(this);
                _views.Clear();
                _roots.Dispose();
            }
        }

        internal sealed class ViewState : IDisposable
        {
            public IntPtr View, Card, Border, Variant, Surface, Reveal, Back, Faction, MethodInfo;
            public int Cost, Power, Rarity, RevealType, Logo;
            public bool ShowReveal, Morph, Transparent;
            public int InstanceId, OriginalLoadOptions;
            public IntPtr OriginalRender;
            private Il2CppRuntime _roots;

            public void Root(Il2CppRuntime runtime, IL2CppFieldInfoWrapper renderOverride, IL2CppFieldInfoWrapper loadOptions)
            {
                _roots = new Il2CppRuntime();
                try
                {
                    foreach (var value in new[] { View, Card, Border, Variant, Surface, Reveal, Back, Faction }) _roots.Retain(value);
                    InstanceId = runtime.InvokeValue<int>(runtime.Method(AssetLabNative.EngineType(runtime, "Object"), "GetInstanceID", false), View);
                    OriginalRender = _roots.ReadReference(View, renderOverride);
                    OriginalLoadOptions = runtime.ReadField<int>(View, loadOptions);
                }
                catch { Dispose(); throw; }
            }

            public void InheritOriginal(ViewState previous)
            {
                OriginalRender = _roots.Retain(previous.OriginalRender);
                OriginalLoadOptions = previous.OriginalLoadOptions;
            }

            public bool IsAlive(Il2CppRuntime runtime) => AssetLabNative.Alive(runtime, View) &&
                runtime.InvokeValue<int>(runtime.Method(AssetLabNative.EngineType(runtime, "Object"), "GetInstanceID", false), View) == InstanceId;

            public void Restore()
            {
                var initialize = SnapTypeDataCollector.CardViewInitializeHookOverride ?? SnapTypeDataCollector.CardViewInitializeOriginal;
                initialize(View, Card, Cost, Power, Rarity, Border, Variant, Surface, Reveal, RevealType,
                    ShowReveal, Logo, Back, Morph, Transparent, Faction, MethodInfo);
            }

            public void Invoke(IntPtr card, int cost, int power, int rarity, IntPtr variant)
            {
                SnapTypeDataCollector.CardViewInitializeOriginal(View, card, cost, power, rarity, Border, variant,
                    Surface, Reveal, RevealType, ShowReveal, Logo, Back, Morph, Transparent, Faction, MethodInfo);
            }

            public void Dispose() { _roots?.Dispose(); _roots = null; }
        }
    }
}
