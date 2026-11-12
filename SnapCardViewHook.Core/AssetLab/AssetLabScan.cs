using IL2CppApi.Runtime;
using SnapCardViewHook.Core.IL2Cpp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SnapCardViewHook.Core.AssetLab
{
    internal sealed class AssetLabScan : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool LocateDelegate(IntPtr instance, IntPtr key, IntPtr type, out IntPtr locations, IntPtr methodInfo);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PullDelegate(IntPtr instance, IntPtr type, out IntPtr result,
            [MarshalAs(UnmanagedType.U1)] bool throwExceptions, IntPtr methodInfo);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool FindRenderDelegate(IntPtr instance, IntPtr address, out IntPtr render, IntPtr methodInfo);

        private static readonly string[] Roles = { "Foreground", "Background", "Logo" };
        private static readonly string[] TextureRoles = { "Foreground", "Background", "Logo", "ForegroundScreen" };
        private static readonly string[] BakedFolders = { "Assets/Baked/Cards/", "Assets/Baked/SmallCards/", "Assets/Baked/Logos/" };
        private static readonly Regex PlaceholderPath = new Regex(
            @"(?:^|[/\\_.-])(?:wip[a-z0-9]*|placeholder[a-z0-9]*|work[_ -]?in[_ -]?progress)(?=$|[/\\_.-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private readonly Il2CppRuntime _roots = new Il2CppRuntime();
        private readonly Dictionary<string, AssetLabEntry> _entries = new Dictionary<string, AssetLabEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AssetLabCardData> _cards = new Dictionary<string, AssetLabCardData>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _variants = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _variantCards = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, string>> _renderMaterials = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, string>> _renderTextures = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AssetLabResource> _resources = new Dictionary<string, AssetLabResource>(StringComparer.Ordinal);
        private readonly HashSet<string> _listedCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _listedVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _bundleFiles = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly List<(IntPtr Instance, LocateDelegate Locate, IntPtr Method)> _locators =
            new List<(IntPtr, LocateDelegate, IntPtr)>();
        private readonly TaskCompletionSource<AssetLabEntry[]> _completion = new TaskCompletionSource<AssetLabEntry[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationToken _cancellation;
        private readonly IProgress<string> _progress;
        private IEnumerator<bool> _work;
        private Il2CppRuntime _frame;
        private string _runtimePath;
        private string _phase = "Reading client definitions";
        private int _locations;
        private int _keys;
        private long _nextReport;
        private IntPtr _templateRender;
        private bool _templatesWereScanned;
        private bool _finished, _disposed;

        public AssetLabScan(IProgress<string> progress, CancellationToken cancellation)
        {
            _progress = progress;
            _cancellation = cancellation;
            _work = Read().GetEnumerator();
            SnapTypeDataCollector.RegisterGameUiCallback(Tick);
        }

        public Task<AssetLabEntry[]> Completion => _completion.Task;
        public IntPtr FindRenderTemplate(AssetLabEntry entry)
        {
            if (entry.RenderDefinition != IntPtr.Zero) return entry.RenderDefinition;
            _entries.TryGetValue(entry.CardId, out var baseEntry);
            if (baseEntry != null && baseEntry.RenderDefinition != IntPtr.Zero) return baseEntry.RenderDefinition;
            if (_templatesWereScanned) return _templateRender;
            using (var runtime = new Il2CppRuntime())
            {
                var containerType = runtime.Class("Infra.DependencyInjection.dll",
                    "CubeUnity.Infra.DependencyInjection", "DependencyContainer");
                var container = runtime.Invoke(runtime.Method(containerType, "get_Instance", true), IntPtr.Zero);
                if (container == IntPtr.Zero) return _templateRender;
                var managerType = runtime.Class("AssetCacheManager.dll", "CubeUnity.App.Assets", "CardRenderDefManager");
                var pull = runtime.Method(containerType, "Pull", false, "System.Type", "System.Object&", "System.Boolean");
                var resolve = Marshal.GetDelegateForFunctionPointer<PullDelegate>(pull.MethodPointer);
                resolve(container, runtime.TypeObject(managerType), out var manager, false, pull.Ptr);
                if (manager == IntPtr.Zero) return _templateRender;
                runtime.Retain(manager);
                if (!runtime.InvokeValue<bool>(runtime.Method(managerType, "get_IsLoaded", false), manager))
                    return _templateRender;
                var find = AssetLabNative.Method(managerType, "TryGetLoadedRenderDef", 2);
                var lookup = Marshal.GetDelegateForFunctionPointer<FindRenderDelegate>(find.MethodPointer);
                foreach (var id in new[] { entry.AssetId, entry.CardId }.Distinct(StringComparer.OrdinalIgnoreCase))
                    if (lookup(manager, runtime.String("CardRenderDefs/" + id + ".asset"), out var render, find.Ptr) && render != IntPtr.Zero)
                    {
                        _roots.Retain(render);
                        if (id == entry.AssetId) entry.RenderDefinition = render;
                        else if (baseEntry != null) baseEntry.RenderDefinition = render;
                        return render;
                    }
                if (_templateRender != IntPtr.Zero) return _templateRender;
                var renderContainer = runtime.ReadReference(manager, runtime.Field(managerType, "_cardRenderDefContainer"));
                if (renderContainer == IntPtr.Zero) return IntPtr.Zero;
                var list = runtime.ReadReference(renderContainer, runtime.Field(runtime.ObjectClass(renderContainer), "RenderDefs"));
                if (list == IntPtr.Zero) return IntPtr.Zero;
                using (var sequence = new AssetLabNative.Sequence(list))
                    if (sequence.Next(runtime, out var fallbackRender)) _templateRender = _roots.Retain(fallbackRender);
                return _templateRender;
            }
        }

        private void Tick()
        {
            if (_finished) return;
            try
            {
                _cancellation.ThrowIfCancellationRequested();
                var clock = Stopwatch.StartNew();
                using (_frame = new Il2CppRuntime())
                {
                    var steps = 0;
                    while (steps++ < 100 && clock.ElapsedMilliseconds < 4)
                    {
                        _cancellation.ThrowIfCancellationRequested();
                        if (_work.MoveNext()) continue;
                        Finish();
                        _completion.TrySetResult(_entries.Values.OrderBy(e => e.AssetId, StringComparer.OrdinalIgnoreCase).ToArray());
                        return;
                    }
                }
                if (Stopwatch.GetTimestamp() >= _nextReport)
                {
                    _nextReport = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 3;
                    _progress?.Report(_phase + ": " + (_keys == 0 ? _locations.ToString("N0") + " locations" : _keys.ToString("N0") + " keys") + ", " +
                        _entries.Count.ToString("N0") + " card/variant groups...");
                }
            }
            catch (OperationCanceledException) { Finish(); _completion.TrySetCanceled(); }
            catch (Exception error) { Finish(); _completion.TrySetException(error); }
            finally { _frame = null; }
        }

        private IEnumerable<bool> Read()
        {
            foreach (var field in SnapTypeDataCollector.CardDef_Id_Fields)
            {
                _listedCards.Add(_roots.StringValue(IL2CppHelper.GetStaticFieldValue(field)));
                yield return true;
            }
            foreach (var field in SnapTypeDataCollector.ArtVariantDef_Id_Fields)
            {
                _listedVariants.Add(_roots.StringValue(IL2CppHelper.GetStaticFieldValue(field)));
                yield return true;
            }

            using (var sequence = Definitions("CardDefList"))
                while (sequence.Next(_frame, out var definition))
                {
                    var type = _frame.ObjectClass(definition);
                    var id = AssetLabNative.Id(_frame, definition, "<CardDefId>k__BackingField");
                    if (!string.IsNullOrEmpty(id))
                        _cards[id] = new AssetLabCardData
                        {
                            Definition = _roots.Retain(definition), Id = id,
                            Name = AssetLabNative.FieldText(_frame, definition, "<Name>k__BackingField"),
                            Description = AssetLabNative.FieldText(_frame, definition, "<Description>k__BackingField"),
                            Cost = _frame.ReadField<int>(definition, _frame.Field(type, "<Cost>k__BackingField")),
                            Power = _frame.ReadField<int>(definition, _frame.Field(type, "<Power>k__BackingField"))
                        };
                    yield return true;
                }

            using (var sequence = Definitions("ArtVariantDefList"))
                while (sequence.Next(_frame, out var definition))
                {
                    var id = AssetLabNative.Id(_frame, definition, "<ArtVariantDefId>k__BackingField");
                    if (!string.IsNullOrEmpty(id))
                    {
                        var field = _frame.Field(_frame.ObjectClass(definition), "<IsPlaceholder>k__BackingField", true);
                        _variants[id] = field != null && _frame.ReadField<bool>(definition, field);
                    }
                    yield return true;
                }

            using (var sequence = Definitions("CardToArtVariantDefList"))
                while (sequence.Next(_frame, out var definition))
                {
                    ReadVariantMapping(definition);
                    yield return true;
                }

            var containerType = AssetLabNative.RenderType(_roots, "CardRenderDefContainer");
            foreach (var container in AssetLabNative.LoadedObjects(_roots, containerType))
            {
                var list = _roots.ReadReference(container, _roots.Field(containerType, "RenderDefs"));
                if (list == IntPtr.Zero) continue;
                using (var sequence = new AssetLabNative.Sequence(list))
                    while (sequence.Next(_frame, out var render))
                    {
                        var id = AssetLabNative.FieldText(_frame, render, "name");
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            id = Path.GetFileNameWithoutExtension(id);
                            var entry = Entry(id);
                            entry.RenderDefinition = _roots.Retain(render);
                            var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var role in Roles)
                            {
                                var reference = _frame.ReadReference(render, _frame.Field(_frame.ObjectClass(render), "_" + role + "MaterialReference"));
                                if (reference != IntPtr.Zero) keys[role] = AssetLabNative.Text(_frame, reference, "AssetGUID");
                            }
                            _renderMaterials[id] = keys;
                            var textures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var role in TextureRoles)
                            {
                                var reference = _frame.ReadReference(render, _frame.Field(_frame.ObjectClass(render), "_Baked" + role + "TextureReference"));
                                if (reference != IntPtr.Zero) textures[role] = AssetLabNative.Text(_frame, reference, "AssetGUID");
                            }
                            _renderTextures[id] = textures;
                        }
                        yield return true;
                    }
            }

            var addressables = _roots.Class("Unity.Addressables.dll", "UnityEngine.AddressableAssets", "Addressables");
            _phase = "Scanning local asset index";
            _runtimePath = _roots.StringValue(_roots.Invoke(_roots.Method(addressables, "get_RuntimePath", true), IntPtr.Zero));
            var locators = _roots.Invoke(_roots.Method(addressables, "get_ResourceLocators", true), IntPtr.Zero);
            using (var sequence = new AssetLabNative.Sequence(locators))
                while (sequence.Next(_frame, out var locator))
                {
                    IntPtr locations;
                    try { locations = AssetLabNative.Get(_frame, locator, "AllLocations"); }
                    catch (NotSupportedException) { continue; }
                    if (locations == IntPtr.Zero) continue;
                    var locate = AssetLabNative.Method(_frame.ObjectClass(locator), "Locate", 3);
                    var parameters = locate.GetParameters();
                    if (parameters[0].Name != "System.Object" || parameters[1].Name != "System.Type" ||
                        !parameters[2].Name.EndsWith("&", StringComparison.Ordinal) || locate.MethodPointer == IntPtr.Zero)
                        throw new NotSupportedException("Unexpected client asset-locator signature.");
                    _locators.Add((_roots.Retain(locator), Marshal.GetDelegateForFunctionPointer<LocateDelegate>(locate.MethodPointer), locate.Ptr));
                    using (var assets = new AssetLabNative.Sequence(locations))
                        while (assets.Next(_frame, out var location))
                        {
                            if (++_locations > 1000000) throw new InvalidOperationException("The asset catalog exceeded the scan limit.");
                            ReadLocation(location);
                            yield return true;
                        }
                }

            _phase = "Resolving render references";
            foreach (var pair in _renderMaterials)
                foreach (var role in pair.Value)
                {
                    var resource = ResolveResource(role.Value, "Material");
                    if (resource != null) Entry(pair.Key).Materials[role.Key] = resource;
                    yield return true;
                }

            foreach (var pair in _renderTextures)
                foreach (var role in pair.Value)
                {
                    var resource = ResolveResource(role.Value, "Texture2D");
                    if (resource != null)
                    {
                        var textures = Entry(pair.Key).Textures;
                        if (!textures.TryGetValue(role.Key, out var existing) || !resource.Placeholder || existing.Placeholder)
                            textures[role.Key] = resource;
                    }
                    yield return true;
                }

            _phase = "Matching asset GUIDs";
            var stringType = _roots.Class("mscorlib.dll", "System", "String");
            var assetType = _roots.TypeObject(AssetLabNative.EngineType(_roots, "Object"));
            foreach (var locator in _locators)
                using (var keys = new AssetLabNative.Sequence(AssetLabNative.Get(_frame, locator.Instance, "Keys")))
                    while (keys.Next(_frame, out var nativeKey))
                    {
                        if (++_keys > 1000000) throw new InvalidOperationException("The asset catalog exceeded the key scan limit.");
                        if (_frame.IsInstance(nativeKey, stringType))
                        {
                            var key = _frame.StringValue(nativeKey);
                            if (Guid.TryParse(key, out _) && !_resources.ContainsKey(key) &&
                                locator.Locate(locator.Instance, nativeKey, assetType, out var locations, locator.Method) && locations != IntPtr.Zero)
                            {
                                _frame.Retain(locations);
                                using (var matches = new AssetLabNative.Sequence(locations))
                                    while (matches.Next(_frame, out var location))
                                    {
                                        var path = AssetLabNative.Text(_frame, location, "InternalId")?.Replace('\\', '/');
                                        if (path != null && _resources.TryGetValue(path, out var resource) && resource != null)
                                        {
                                            resource.Key = key;
                                            _resources[key] = resource;
                                        }
                                    }
                            }
                        }
                        yield return true;
                    }

            _phase = "Checking preview availability";
            _templatesWereScanned = _entries.Values.Any(entry => entry.RenderDefinition != IntPtr.Zero);
            foreach (var entry in _entries.Values)
            {
                CompleteEntry(entry);
                yield return true;
            }
            _templateRender = _entries.Values.OrderBy(entry => entry.Variant || !entry.HasCardData ? 1 : 0)
                .Select(entry => entry.RenderDefinition).FirstOrDefault(render => render != IntPtr.Zero);
        }

        private AssetLabNative.Sequence Definitions(string name)
        {
            var type = AssetLabNative.DefType(_roots, name);
            return new AssetLabNative.Sequence(_roots.Invoke(_roots.Method(type, "get_Defs", true), IntPtr.Zero));
        }

        private unsafe void ReadVariantMapping(IntPtr definition)
        {
            var id = AssetLabNative.Id(_frame, definition, "<CardDefId>k__BackingField");
            var field = _frame.Field(_frame.ObjectClass(definition), "<ArtVariantDefIds>k__BackingField");
            var array = _frame.ReadReference(definition, field);
            if (array == IntPtr.Zero) return;
            if (_frame.ObjectClass(array).GetArrayElementSize() != IntPtr.Size ||
                ((IL2CppArray*)array)->Count < 0 || ((IL2CppArray*)array)->Count > 200000)
                throw new NotSupportedException("Unexpected variant mapping array layout.");
            foreach (var value in IL2CppHelper.ArrayToArray<IntPtr>((IL2CppArray*)array))
            {
                var variant = _frame.StringValue(value);
                if (!string.IsNullOrEmpty(variant)) _variantCards[variant] = id;
            }
        }

        private AssetLabEntry Entry(string id)
        {
            if (!_entries.TryGetValue(id, out var entry))
                _entries.Add(id, entry = new AssetLabEntry { AssetId = id });
            return entry;
        }

        private AssetLabResource ResolveResource(string key, string typeName)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_resources.TryGetValue(key, out var resource)) return resource;
            var nativeKey = _frame.String(key);
            var assetType = _frame.TypeObject(AssetLabNative.EngineType(_frame, typeName));
            foreach (var locator in _locators)
            {
                if (!locator.Locate(locator.Instance, nativeKey, assetType, out var locations, locator.Method) || locations == IntPtr.Zero)
                    continue;
                _frame.Retain(locations);
                using (var sequence = new AssetLabNative.Sequence(locations))
                    if (sequence.Next(_frame, out var location))
                    {
                        var path = AssetLabNative.Text(_frame, location, "InternalId")?.Replace('\\', '/');
                        if (path == null) continue;
                        if (!_resources.TryGetValue(path, out resource))
                        {
                            ReadLocation(location);
                            _resources.TryGetValue(path, out resource);
                        }
                        if (resource != null)
                        {
                            if (Guid.TryParse(key, out _)) resource.Key = key;
                            return _resources[key] = resource;
                        }
                    }
            }
            _resources[key] = null;
            return null;
        }

        private void ReadLocation(IntPtr location)
        {
            var path = AssetLabNative.Text(_frame, location, "InternalId")?.Replace('\\', '/');
            if (string.IsNullOrEmpty(path)) return;
            var cardFolder = path.IndexOf("Assets/Cards/", StringComparison.OrdinalIgnoreCase);
            var folderLength = "Assets/Cards/".Length;
            var baked = false;
            if (cardFolder < 0)
                foreach (var folder in BakedFolders)
                {
                    var index = path.IndexOf(folder, StringComparison.OrdinalIgnoreCase);
                    if (index < 0) continue;
                    cardFolder = index;
                    folderLength = folder.Length;
                    baked = true;
                    break;
                }
            var isMaterial = path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase);
            var placeholderTexture = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && PlaceholderPath.IsMatch(path);
            if (cardFolder < 0 && !isMaterial && !placeholderTexture) return;
            string id = null;
            if (cardFolder >= 0)
            {
                var relative = path.Substring(cardFolder + folderLength);
                var separator = relative.IndexOf('/');
                if (separator <= 0) return;
                id = relative.Substring(0, separator);
                Entry(id);
            }
            if (!isMaterial && !placeholderTexture && (!baked || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))) return;
            var key = AssetLabNative.Text(_frame, location, "PrimaryKey");
            if (string.IsNullOrEmpty(key)) return;
            if (_resources.TryGetValue(path, out var known) && known != null)
            {
                _resources[key] = known;
                if (Guid.TryParse(key, out _)) known.Key = key;
                return;
            }
            var resource = new AssetLabResource { Key = Guid.TryParse(key, out _) ? key : null, Path = path };
            resource.Local = LocalDependencies(location, out resource.Bundles, out resource.Placeholder);
            _resources[key] = resource;
            _resources[path] = resource;
            if (id == null) return;
            var file = Path.GetFileNameWithoutExtension(path);
            if (baked)
            {
                if (file.EndsWith("_Small", StringComparison.OrdinalIgnoreCase))
                {
                    resource.Priority += 2;
                    file = file.Substring(0, file.Length - "_Small".Length);
                }
                if (file.EndsWith("_Common", StringComparison.OrdinalIgnoreCase))
                {
                    resource.Priority++;
                    file = file.Substring(0, file.Length - "_Common".Length);
                }
            }
            var target = baked ? Entry(id).Textures : Entry(id).Materials;
            foreach (var role in baked ? TextureRoles : Roles)
                if (file.Equals(id + "_" + role, StringComparison.OrdinalIgnoreCase))
                {
                    if (!target.TryGetValue(role, out var existing) || resource.Priority < existing.Priority)
                        target[role] = resource;
                    if (baked)
                    {
                        var sets = Entry(id).BakeSets;
                        if (!sets.TryGetValue(resource.Priority, out var set))
                            sets[resource.Priority] = set = new Dictionary<string, AssetLabResource>(StringComparer.OrdinalIgnoreCase);
                        set[role] = resource;
                    }
                }
        }

        private bool LocalDependencies(IntPtr location, out string[] bundles, out bool placeholder)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<(IntPtr Location, int Depth)>();
            pending.Push((location, 0));
            var local = true;
            placeholder = false;
            while (pending.Count != 0)
            {
                var dependency = pending.Pop();
                var current = dependency.Location;
                var id = AssetLabNative.Text(_frame, current, "InternalId");
                if (id != null && dependency.Depth <= 2 && PlaceholderPath.IsMatch(id)) placeholder = true;
                if (id == null || !visited.Add(id)) continue;
                if (visited.Count > 512) throw new InvalidOperationException("An asset has too many bundle dependencies.");
                if (id.IndexOf(".bundle", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var path = id.Replace("{UnityEngine.AddressableAssets.Addressables.RuntimePath}", _runtimePath);
                    if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
                    {
                        if (!uri.IsFile) { local = false; paths.Add(path); continue; }
                        path = uri.LocalPath;
                    }
                    paths.Add(path);
                    if (!_bundleFiles.TryGetValue(path, out var exists)) _bundleFiles[path] = exists = File.Exists(path);
                    if (!exists) local = false;
                }
                var dependencies = AssetLabNative.Get(_frame, current, "Dependencies");
                if (dependencies == IntPtr.Zero) continue;
                var type = _frame.ObjectClass(dependencies);
                if (type.Rank > 0)
                {
                    var values = _frame.ReferenceArray(dependencies);
                    if (values.Length > 512) throw new InvalidOperationException("Invalid asset dependency array.");
                    foreach (var value in values) pending.Push((value, dependency.Depth + 1));
                    continue;
                }
                var count = _frame.InvokeValue<int>(AssetLabNative.Method(type, "get_Count"), dependencies);
                if (count < 0 || count > 512) throw new InvalidOperationException("Invalid asset dependency list.");
                var item = AssetLabNative.Method(type, "get_Item", 1);
                for (var i = 0; i < count; i++)
                    pending.Push((_frame.Invoke(item, dependencies, Il2CppArgument.Value(i)), dependency.Depth + 1));
            }
            bundles = paths.OrderBy(p => p).ToArray();
            return local && paths.Count != 0;
        }

        private void CompleteEntry(AssetLabEntry entry)
        {
            var variantName = Regex.Match(entry.AssetId, @"^(.+)_([0-9]{2,})$");
            entry.Variant = _variantCards.ContainsKey(entry.AssetId) || _variants.ContainsKey(entry.AssetId) ||
                (!_cards.ContainsKey(entry.AssetId) && variantName.Success);
            entry.CardId = _variantCards.TryGetValue(entry.AssetId, out var mapped) ? mapped :
                entry.Variant && variantName.Success ? variantName.Groups[1].Value : entry.AssetId;
            var reasons = new List<string>();
            if (_cards.TryGetValue(entry.CardId, out var card))
            {
                entry.CardId = card.Id;
                entry.CardDefinition = card.Definition;
                entry.HasCardData = true;
                entry.Name = string.IsNullOrWhiteSpace(card.Name) ? AssetLabEntry.FriendlyName(card.Id) : card.Name;
                entry.Description = card.Description ?? string.Empty;
                entry.Cost = card.Cost;
                entry.Power = card.Power;
                if (string.IsNullOrWhiteSpace(card.Name)) reasons.Add("Missing card name");
                if (!_listedCards.Contains(card.Id)) reasons.Add("Not in the card selector");
            }
            else
            {
                entry.Name = AssetLabEntry.FriendlyName(entry.CardId);
                entry.Description = string.Empty;
                reasons.Add("Missing card definition");
            }
            if (entry.Variant)
            {
                if (!_variants.TryGetValue(entry.AssetId, out var placeholder)) reasons.Add("Missing variant definition");
                else if (placeholder) reasons.Add("Placeholder variant");
                if (!_variantCards.ContainsKey(entry.AssetId)) reasons.Add("Missing variant mapping");
                if (!_listedVariants.Contains(entry.AssetId)) reasons.Add("Not in the variant selector");
                if (_entries.TryGetValue(entry.CardId, out var baseEntry))
                {
                    foreach (var role in Roles)
                        if (!entry.Materials.ContainsKey(role) && baseEntry.Materials.TryGetValue(role, out var inherited))
                            entry.Materials[role] = inherited;
                    foreach (var role in TextureRoles)
                        if (!entry.Textures.ContainsKey(role) && baseEntry.Textures.TryGetValue(role, out var inheritedTexture))
                            entry.Textures[role] = inheritedTexture;
                }
            }
            if (entry.RenderDefinition == IntPtr.Zero)
                reasons.Add(_templatesWereScanned ? "Missing render definition" : "Render definition not loaded yet");
            BuildBakes(entry);
            entry.HasWipMaterials = entry.Materials.Values.Any(resource => resource.Placeholder);
            if (entry.HasWipMaterials) reasons.Add("WIP/placeholder texture reference in 3D materials");
            entry.CanPreviewMaterials = Ready(entry.Materials);
            entry.CanPreviewBaked = entry.Bakes.Count != 0;
            if (entry.CanPreviewBaked && !entry.CanPreviewMaterials) reasons.Add("Baked artwork without a complete 3D set");
            entry.Stray = reasons.Count != 0;
            entry.Status = entry.Stray ? string.Join("; ", reasons) : "Defined in client";
            entry.CanPreview = entry.CanPreviewMaterials || entry.CanPreviewBaked;
            entry.Availability = entry.HasWipMaterials && entry.CanPreviewBaked ? "WIP reference in 3D; " + entry.Bakes.Count + " bake choices available" :
                entry.CanPreviewMaterials && entry.CanPreviewBaked ? "Ready: 3D and " + entry.Bakes.Count + " bake choices" :
                entry.CanPreviewMaterials ? "Ready: 3D materials" : entry.CanPreviewBaked ? "Ready: baked artwork (2D)" :
                "3D: " + Missing(entry.Materials) + "; baked: " + Missing(entry.Textures);
        }

        private void BuildBakes(AssetLabEntry entry)
        {
            entry.Bakes.Clear();
            _entries.TryGetValue(entry.CardId, out var baseEntry);
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in entry.BakeSets.OrderBy(pair => pair.Key))
            {
                if (!pair.Value.ContainsKey("Foreground") && !pair.Value.ContainsKey("Background")) continue;
                var textures = new Dictionary<string, AssetLabResource>(pair.Value, StringComparer.OrdinalIgnoreCase);
                if (entry.Variant && baseEntry != null && baseEntry.BakeSets.TryGetValue(pair.Key, out var inherited))
                    foreach (var role in inherited)
                        if (!textures.ContainsKey(role.Key)) textures[role.Key] = role.Value;
                if (!textures.ContainsKey("Logo") && entry.Textures.TryGetValue("Logo", out var logo)) textures["Logo"] = logo;
                var name = pair.Key == 0 ? "Full size" : pair.Key == 1 ? "Common" : pair.Key == 2 ? "Small" : "Small common";
                AddBake(name, textures);
            }
            AddBake("Available textures", new Dictionary<string, AssetLabResource>(entry.Textures, StringComparer.OrdinalIgnoreCase));
            var preferred = entry.Bakes.OrderBy(bake => bake.Placeholder).ToArray();
            entry.Bakes.Clear();
            entry.Bakes.AddRange(preferred);

            void AddBake(string name, Dictionary<string, AssetLabResource> textures)
            {
                if (!Ready(textures)) return;
                var signature = string.Join("|", textures.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value.Key));
                if (signatures.Add(signature)) entry.Bakes.Add(new AssetLabBake { Name = name, Textures = textures });
            }
        }

        private static bool Ready(Dictionary<string, AssetLabResource> resources) =>
            Roles.All(resources.ContainsKey) && resources.Values.All(r => r.Key != null && r.Local);

        private static string Missing(Dictionary<string, AssetLabResource> resources)
        {
            var missing = Roles.Where(role => !resources.ContainsKey(role)).ToArray();
            if (missing.Length != 0) return "no " + string.Join(" / ", missing).ToLowerInvariant();
            return resources.Values.Any(resource => resource.Key == null) ? "missing asset GUID" : "bundle not installed";
        }

        private void Finish()
        {
            _finished = true;
            SnapTypeDataCollector.RemoveGameUiCallback(Tick);
            var work = _work;
            _work = null;
            work?.Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { Finish(); _completion.TrySetCanceled(); }
            finally { _roots.Dispose(); }
        }
    }
}
