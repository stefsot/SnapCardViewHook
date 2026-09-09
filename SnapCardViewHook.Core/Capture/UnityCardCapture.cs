using IL2CppApi.Runtime;
using IL2CppApi.Wrappers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace SnapCardViewHook.Core.Capture
{
    internal sealed class UnityCardCapture : IDisposable
    {
        private const string Core = "UnityEngine.CoreModule.dll";
        private const string Universal = "Unity.RenderPipelines.Universal.Runtime.dll";
        private const string UrpNamespace = "UnityEngine.Rendering.Universal";
        private static readonly string[] NoArgs = Array.Empty<string>();
        private readonly Il2CppRuntime _runtime;
        private readonly IL2CppClassWrapper _object, _component, _gameObject, _behaviour, _transform;
        private readonly IL2CppClassWrapper _renderer, _camera, _renderTexture, _texture2D, _graphics, _gl, _shader;
        private readonly IL2CppClassWrapper _canvas, _cardView, _cardRenderer, _details, _cameraData, _feature, _scriptableRenderer;
        private readonly IL2CppClassWrapper _pipeline, _pipelineManager, _requestType, _multiPass, _renderObjects;
        private readonly IL2CppClassWrapper[] _permittedFeatures, _coverageFeatures;
        private CaptureStateScope _resources;
        private CardCaptureOptions _options;
        private CardCaptureAnchor _anchor;
        private IntPtr _mainRenderer, _captureCamera, _target, _readTarget, _pixels, _request;
        private IntPtr[] _unrelatedRenderers, _unrelatedCanvases, _terrains, _suppressedFeatures, _matteFeatures;
        private (IntPtr Key, IntPtr Texture)[] _cameraInputs;
        private CaptureRect _readRect;
        private int _rendererCount, _pixelByteCount;
        private bool _linearColorSpace, _writeSrgb;
        private string _sourceCameraName;

        public UnityCardCapture(Il2CppRuntime runtime)
        {
            _runtime = runtime;
            _object = Engine("Object"); _component = Engine("Component"); _gameObject = Engine("GameObject");
            _behaviour = Engine("Behaviour"); _transform = Engine("Transform"); _renderer = Engine("Renderer");
            _camera = Engine("Camera"); _renderTexture = Engine("RenderTexture"); _texture2D = Engine("Texture2D");
            _graphics = Engine("Graphics"); _gl = Engine("GL"); _shader = Engine("Shader");
            _canvas = runtime.Class("UnityEngine.UIModule.dll", "UnityEngine", "Canvas");
            _cardView = runtime.Class("App.View.dll", "CubeUnity.App.View", "CardView");
            _details = runtime.Class("App.View.dll", "CubeUnity.App.View", "CardDetailsCardView");
            _cardRenderer = runtime.Class("SecondDinner.CubeRendering.Card.dll", "SecondDinner.CubeRendering.Card", "CardRenderer");
            _cameraData = runtime.Class(Universal, UrpNamespace, "UniversalAdditionalCameraData");
            _feature = runtime.Class(Universal, UrpNamespace, "ScriptableRendererFeature");
            _scriptableRenderer = runtime.Class(Universal, UrpNamespace, "ScriptableRenderer");
            _pipeline = runtime.Class(Universal, UrpNamespace, "UniversalRenderPipeline");
            _pipelineManager = runtime.Class(Core, "UnityEngine.Rendering", "RenderPipelineManager");
            _requestType = runtime.NestedClass(_pipeline, "SingleCameraRequest");
            _multiPass = runtime.Class("SecondDinner.CubeRendering.dll", "", "MultiPassCardRenderer", true);
            _renderObjects = runtime.Class(Universal, UrpNamespace, "RenderObjects");
            
            _coverageFeatures = new[] { _renderObjects, _multiPass }.Where(t => t != null).ToArray();
            _permittedFeatures = _coverageFeatures.Concat(new[] {
                runtime.Class("SecondDinner.CubeRendering.dll", "", "GlowRendererFeature", true),
                runtime.Class("SecondDinner.CubeRendering.dll", "", "BloomRendererFeature", true),
                runtime.Class("SecondDinner.CubeRendering.dll", "", "FXAARenderFeature", true)
            }).Where(t => t != null).ToArray();
            
            RequireSize(Engine("Vector3"), 12); RequireSize(Engine("Quaternion"), 16);
            RequireSize(Engine("Bounds"), 24); RequireSize(Engine("Rect"), 16); RequireSize(Engine("Color"), 16);
            runtime.Method(_camera, "SubmitRenderRequestsInternal", false, "System.Object");
            runtime.Method(_renderTexture, "set_active", true, "UnityEngine.RenderTexture");
            runtime.Method(_renderer, "set_forceRenderingOff", false, "System.Boolean");
            runtime.Method(Engine("MeshFilter"), "get_sharedMesh", false);
            runtime.Method(Engine("Mesh"), "get_bounds", false);
            runtime.Method(_transform, "TransformPoint", false, "UnityEngine.Vector3");
            runtime.Method(_texture2D, "ReadPixels", false, "UnityEngine.Rect", "System.Int32", "System.Int32", "System.Boolean");
            runtime.Method(_texture2D, "GetRawTextureData", false);
        }

        public void Prepare(CardCaptureOptions options, CancellationToken cancellation, CardCaptureAnchor anchor = null)
        {
            if (_resources != null) throw new InvalidOperationException("Card capture has already been prepared.");
            cancellation.ThrowIfCancellationRequested();
            if (GetReference(_camera, IntPtr.Zero, "current") != IntPtr.Zero)
                throw new InvalidOperationException("Card capture must be requested outside the camera rendering loop.");
            if (!_runtime.IsInstance(GetReference(_pipelineManager, IntPtr.Zero, "currentPipeline"), _pipeline))
                throw new NotSupportedException("The running game is not using the expected Universal Render Pipeline.");

            var card = FindSelectedCard();
            EnsureReady(card);
            var mainRenderer = FieldReference(card, _cardRenderer, "_CardRenderer");
            if (!Visible(mainRenderer)) throw new InvalidOperationException("The selected card is not currently visible. Open its details view first.");
            var roots = CardRoots(card);
            var exclusions = ExcludedRoots(card, options).ToArray();
            var allowed = new HashSet<IntPtr>(roots.SelectMany(r => Descendants(r, _renderer)));
            allowed.Add(mainRenderer);
            foreach (var excluded in exclusions)
                foreach (var renderer in Descendants(excluded, _renderer)) allowed.Remove(renderer);
            var visible = allowed.Where(Visible).ToArray();
            if (visible.Length == 0) throw new InvalidOperationException("The selected card has no visible renderers.");
            var mask = 0;
            foreach (var renderer in visible)
                mask |= 1 << Get<int>(_gameObject, GameOf(renderer), "layer");

            var selection = FindSourceCamera(mainRenderer);
            _options = options;
            _anchor = anchor ?? new CardCaptureAnchor();
            _mainRenderer = mainRenderer;
            _rendererCount = visible.Length;
            _sourceCameraName = ObjectName(selection.Camera);
            _readRect = new CaptureRect(options.Width, options.Height);
            _pixelByteCount = checked(options.Width * options.Height * 4);
            var framing = CalculateCaptureBounds(card, mainRenderer, selection.Camera, visible, options, _anchor);
            PrepareScene(roots, exclusions, allowed, selection.Features);
            cancellation.ThrowIfCancellationRequested();

            _resources = new CaptureStateScope();
            try { CreateRenderResources(selection, framing, mask); }
            catch
            {
                Dispose();
                throw;
            }
        }

        private CaptureFraming CalculateCaptureBounds(IntPtr card, IntPtr mainRenderer, IntPtr source,
            IntPtr[] visible, CardCaptureOptions options, CardCaptureAnchor anchor)
        {
            var sourceTransform = GetReference(_component, source, "transform");
            var orthographic = Get<bool>(_camera, source, "orthographic");
            var fieldOfView = Get<float>(_camera, source, "fieldOfView");
            var forward = Get<CaptureVector3>(_transform, sourceTransform, "forward");
            var rotation = Get<CaptureQuaternion>(_transform, sourceTransform, "rotation");
            
            CaptureFraming framing;
            if (anchor.Initialized)
            {
                if (card != anchor.Card || source != anchor.SourceCamera ||
                    CallValue<int>(_object, card, "GetInstanceID", NoArgs) != anchor.CardInstanceId ||
                    CallValue<int>(_object, source, "GetInstanceID", NoArgs) != anchor.SourceCameraInstanceId)
                    throw new InvalidOperationException("The selected card or source camera changed during recording.");
                framing = anchor.Framing.Copy();
                forward = anchor.Forward;
                rotation = anchor.Rotation;
                orthographic = anchor.Orthographic;
                fieldOfView = anchor.FieldOfView;
            }
            else
            {
                framing = CaptureFraming.FitCard(CardFramingCorners(card, mainRenderer),
                    Get<CaptureVector3>(_transform, sourceTransform, "right"), Get<CaptureVector3>(_transform, sourceTransform, "up"),
                    forward, Get<CaptureVector3>(_transform, sourceTransform, "position"),
                    orthographic, fieldOfView, options);
                anchor.Card = card; anchor.SourceCamera = source;
                anchor.CardInstanceId = CallValue<int>(_object, card, "GetInstanceID", NoArgs);
                anchor.SourceCameraInstanceId = CallValue<int>(_object, source, "GetInstanceID", NoArgs);
                anchor.Framing = framing.Copy(); anchor.Forward = forward; anchor.Rotation = rotation;
                anchor.Orthographic = orthographic; anchor.FieldOfView = fieldOfView;
            }
            framing.IncludeDepthBounds(visible.Select(r => Get<CaptureBounds>(_renderer, r, "bounds")).ToArray(), forward);
            return framing;
        }

        private void CreateRenderResources(SourceCamera selection, CaptureFraming framing, int mask)
        {
            var cameraObject = NewUnityObject(_gameObject, _resources, new[] { "System.String" },
                Il2CppArgument.Ref(_runtime.String("SnapCardViewHook - card capture")));
            var camera = Call(_gameObject, cameraObject, "AddComponent", new[] { "System.Type" },
                Il2CppArgument.Ref(_runtime.TypeObject(_camera)));
            _captureCamera = camera;
            Set(_behaviour, camera, "enabled", "System.Boolean", false);
            Call(_camera, camera, "CopyFrom", new[] { "UnityEngine.Camera" }, Il2CppArgument.Ref(selection.Camera));
            Set(_behaviour, camera, "enabled", "System.Boolean", false);
            if (Get<int>(_camera, camera, "commandBufferCount") != 0)
                throw new NotSupportedException("The capture camera unexpectedly inherited command buffers.");
            var additional = Call(_gameObject, cameraObject, "AddComponent", new[] { "System.Type" },
                Il2CppArgument.Ref(_runtime.TypeObject(_cameraData)));
            Call(_cameraData, additional, "SetRenderer", new[] { "System.Int32" }, Il2CppArgument.Value(selection.RendererIndex));
            Set(_cameraData, additional, "renderType", UrpNamespace + ".CameraRenderType", 0); // Base, with a fresh empty stack.
            Set(_cameraData, additional, "renderPostProcessing", "System.Boolean", false);
            Set(_cameraData, additional, "volumeLayerMask", "UnityEngine.LayerMask", 0);
            Set(_cameraData, additional, "allowXRRendering", "System.Boolean", false);
            Set(_cameraData, additional, "antialiasing", UrpNamespace + ".AntialiasingMode", 0);
            // These textures must be generated from this camera, not inherited from the game framebuffer.
            Set(_cameraData, additional, "requiresColorOption", UrpNamespace + ".CameraOverrideOption", 1);
            Set(_cameraData, additional, "requiresDepthOption", UrpNamespace + ".CameraOverrideOption", 1);
            if (GetReference(_cameraData, additional, "scriptableRenderer") != selection.Renderer)
                throw new NotSupportedException("Could not bind the capture camera to the card's renderer.");

            var hdr = Get<bool>(_camera, selection.Camera, "allowHDR");
            if (hdr && !CallValue<bool>(Engine("SystemInfo"), IntPtr.Zero, "SupportsRenderTextureFormat",
                new[] { "UnityEngine.RenderTextureFormat" }, Il2CppArgument.Value(2)))
                throw new NotSupportedException("The GPU does not support the HDR render texture required by this card camera.");
            _target = NewRenderTexture(_options.Width, _options.Height, 24, hdr ? 2 : 0, _resources); // ARGBHalf / ARGB32.
            _resources.RestoreWith(() => SetReference(_camera, camera, "targetTexture", "UnityEngine.RenderTexture", IntPtr.Zero));
            ConfigureCamera(camera, cameraObject, _anchor.Rotation, _target, framing, mask,
                _anchor.Orthographic, _anchor.FieldOfView, _options);

            _request = _runtime.NewObject(_requestType);
            Call(_requestType, _request, ".ctor", NoArgs);
            _runtime.WriteReference(_request, _runtime.Field(_requestType, "destination"), _target);
            _linearColorSpace = Get<int>(Engine("QualitySettings"), IntPtr.Zero, "activeColorSpace") == 1;
            _readTarget = hdr ? NewRenderTexture(_options.Width, _options.Height, 0, 0, _resources) : _target;
            _writeSrgb = _linearColorSpace && Get<bool>(_renderTexture, _readTarget, "sRGB");
            _pixels = NewUnityObject(_texture2D, _resources,
                new[] { "System.Int32", "System.Int32", "UnityEngine.TextureFormat", "System.Boolean", "System.Boolean" },
                Il2CppArgument.Value(_options.Width), Il2CppArgument.Value(_options.Height), Il2CppArgument.Value(4),
                Il2CppArgument.Value(false), Il2CppArgument.Value(false)); // RGBA32; no mip chain; sRGB.
        }

        public CapturedCardPixels Capture(CancellationToken cancellation)
        {
            if (_resources == null) throw new InvalidOperationException("Prepare card capture before capturing a frame.");
            cancellation.ThrowIfCancellationRequested();
            if (GetReference(_camera, IntPtr.Zero, "current") != IntPtr.Zero)
                throw new InvalidOperationException("Card capture must be requested outside the camera rendering loop.");
            if (!Alive(_anchor.Card) || !Alive(_anchor.SourceCamera) || !Visible(_mainRenderer))
                throw new InvalidOperationException("The captured card or source camera is no longer available. Keep the card-details view open.");

            CapturedCardPixels result;
            using (var state = new CaptureStateScope())
            {
                var savedActive = GetReference(_renderTexture, IntPtr.Zero, "active");
                var savedSrgb = Get<bool>(_gl, IntPtr.Zero, "sRGBWrite");
                state.RestoreWith(() => SetReference(_renderTexture, IntPtr.Zero, "active", "UnityEngine.RenderTexture", Alive(savedActive) ? savedActive : IntPtr.Zero));
                state.RestoreWith(() => Set(_gl, IntPtr.Zero, "sRGBWrite", "System.Boolean", savedSrgb));

                SuppressUnrelatedRenderers(state);
                SuppressCanvases(state);
                SuppressTerrain(state);
                SuppressFeatures(_suppressedFeatures, state);
                ResetCameraInputs(state);
                cancellation.ThrowIfCancellationRequested();
                
                result = new CapturedCardPixels
                {
                    Rgba = RenderPixels(new CaptureColor(1), cancellation),
                    Width = _options.Width, Height = _options.Height, RendererCount = _rendererCount,
                    LinearColorSpace = _linearColorSpace,
                    SourceCamera = _sourceCameraName
                };
               
                if (!_anchor.Initialized && !HasVisiblePixels(result.Rgba))
                    throw new InvalidOperationException("The offscreen target is blank. No image was saved. Check the active card and renderer compatibility.");

                if (_options.TransparentBackground)
                {
                    using var matteState = new CaptureStateScope();
                   
                    SuppressFeatures(_matteFeatures, matteState);
                    ResetCameraInputs(matteState);
                    result.MatteBlackRgba = RenderPixels(new CaptureColor(1), cancellation);
                    ResetCameraInputs(matteState);
                    result.MatteWhiteRgba = RenderPixels(new CaptureColor(1, 1, 1, 1), cancellation);
                }
            }
            _anchor.Initialized = true;
            return result;
        }

        public void Dispose()
        {
            try { _resources?.Dispose(); }
            finally { _resources = null; }
        }

        private CaptureVector3[] CardFramingCorners(IntPtr card, IntPtr mainRenderer)
        {
            var meshFilterType = Engine("MeshFilter");
            var meshFilter = FieldReference(card, _cardRenderer, "_CardMeshFilter", true);
            if (!Alive(meshFilter)) meshFilter = ComponentOf(mainRenderer, meshFilterType);
            if (Alive(meshFilter))
            {
                var mesh = GetReference(meshFilterType, meshFilter, "sharedMesh");
                if (Alive(mesh))
                {
                    var bounds = Get<CaptureBounds>(Engine("Mesh"), mesh, "bounds");
                    if (!bounds.IsValid) throw new InvalidOperationException("The card mesh has invalid framing bounds.");
                    var transform = GetReference(_component, meshFilter, "transform");
                    return bounds.Corners().Select(point => CallValue<CaptureVector3>(_transform, transform,
                        "TransformPoint", new[] { "UnityEngine.Vector3" }, Il2CppArgument.Value(point))).ToArray();
                }
            }
            
            var fallback = Get<CaptureBounds>(_renderer, mainRenderer, "bounds");
            if (!fallback.IsValid) throw new InvalidOperationException("The card renderer has invalid framing bounds.");
            return fallback.Corners().ToArray();
        }

        private byte[] RenderPixels(CaptureColor background, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            Set(_camera, _captureCamera, "backgroundColor", "UnityEngine.Color", background);
            SetReference(_renderTexture, IntPtr.Zero, "active", "UnityEngine.RenderTexture", _target);
            Call(_gl, IntPtr.Zero, "Clear", new[] { "System.Boolean", "System.Boolean", "UnityEngine.Color", "System.Single" },
                Il2CppArgument.Value(true), Il2CppArgument.Value(true), Il2CppArgument.Value(background), Il2CppArgument.Value(1f));
           
            Call(_camera, _captureCamera, "SubmitRenderRequestsInternal", new[] { "System.Object" }, Il2CppArgument.Ref(_request));
            cancellation.ThrowIfCancellationRequested();
            if (_readTarget != _target)
            {
                Set(_gl, IntPtr.Zero, "sRGBWrite", "System.Boolean", _writeSrgb);
                Call(_graphics, IntPtr.Zero, "Blit", new[] { "UnityEngine.Texture", "UnityEngine.RenderTexture" },
                    Il2CppArgument.Ref(_target), Il2CppArgument.Ref(_readTarget));
            }
            SetReference(_renderTexture, IntPtr.Zero, "active", "UnityEngine.RenderTexture", _readTarget);
            Call(_texture2D, _pixels, "ReadPixels", new[] { "UnityEngine.Rect", "System.Int32", "System.Int32", "System.Boolean" },
                Il2CppArgument.Value(_readRect), Il2CppArgument.Value(0),
                Il2CppArgument.Value(0), Il2CppArgument.Value(false));
            
            return _runtime.InvokeByteArray(_runtime.Method(_texture2D, "GetRawTextureData", false), _pixels, _pixelByteCount);
        }

        private IntPtr FindSelectedCard()
        {
            var candidates = FindObjects(_details).Where(d => Alive(d) && Get<bool>(_behaviour, d, "isActiveAndEnabled") &&
                Get<bool>(_details, d, "IsVisible")).ToArray();
            var stored = SnapTypeDataCollector.CardDetailsCardView_InstancePtr;
            if (candidates.Contains(stored)) candidates = new[] { stored };
            if (candidates.Length != 1)
                throw new InvalidOperationException("Open a single card-details view in the game before capturing.");
            
            var entity = GetReference(_details, candidates[0], "CurrentEntityView");
            var card = Alive(entity) ? ComponentOf(entity, _cardView) : IntPtr.Zero;
            if (!Alive(card)) throw new InvalidOperationException("The selected details entity is not a CardView (it may be a location).");
            return card;
        }

        private void EnsureReady(IntPtr card)
        {
            if (!Alive(card) || !Get<bool>(_cardView, card, "Initialized") || !Get<bool>(_cardView, card, "HasLoadedMaterials") ||
                FieldReference(card, _cardView, "_loadAssetsCoroutine") != IntPtr.Zero ||
                FieldReference(card, _cardRenderer, "_loadBorderCoroutine", true) != IntPtr.Zero)
                throw new InvalidOperationException("The card or its border is still loading. Wait for it to finish, then capture again.");
            var reveal = _runtime.StringValue(GetReference(_cardRenderer, card, "RevealEffect"));
            var incomplete = _runtime.Field(_cardView, "_portraitMaterialsIncomplete", true);
            if (incomplete != null && _runtime.ReadField<bool>(card, incomplete))
                throw new InvalidOperationException("The card's portrait materials are incomplete. Wait for loading to finish, then try again.");
            if (!string.IsNullOrEmpty(reveal) && !string.Equals(reveal, "None", StringComparison.OrdinalIgnoreCase) &&
                !_runtime.ReadField<bool>(card, _runtime.Field(_cardRenderer, "IsCardRevealEffectLoaded")))
                throw new InvalidOperationException("The card's reveal effect is still loading. Wait, then capture again.");
        }

        private List<IntPtr> CardRoots(IntPtr card)
        {
            var roots = new List<IntPtr> { GameOf(card) };
            foreach (var name in new[] { "_Root", "_CardNameBone", "_AbilityTextObject", "_cardRevealEffectGameObject", "_CardShadow", "_CardNameShadow" })
            {
                var root = FieldReference(card, _cardRenderer, name);
                if (Alive(root)) roots.Add(root);
            }
            var values = FieldReference(card, _cardView, "_ValuesRoot", true);
            if (Alive(values)) roots.Add(values);
           
            foreach (var name in new[] { "_CostValueView", "_PowerValueView", "_AbilityText", "_VariantLabel", "_CardName" })
            {
                var component = FieldReference(card, _cardRenderer, name, true);
                if (Alive(component)) roots.Add(GameOf(component));
            }
            foreach (var name in new[] { "_CardBackRenderer", "_SkillVariantLabel" })
            {
                var component = FieldReference(card, _cardView, name, true);
                if (Alive(component)) roots.Add(GameOf(component));
            }
            return roots.Distinct().ToList();
        }

        private IEnumerable<IntPtr> ExcludedRoots(IntPtr card, CardCaptureOptions options)
        {
            foreach (var name in new[] { "_TooltipIcon", "_energyTutorialView", "_powerTutorialView", "_safeFrameGameObject" })
            {
                var root = FieldReference(card, _cardView, name, true);
                if (Alive(root)) yield return root;
            }
            foreach (var name in new[] { "_localCardReaction", "_enemyCardReaction", "_TooltipListAnchor",
                "_ReactionBoneLocal", "_ReactionBoneLocalFaceDown", "_ReactionBoneEnemy", "_ReactionBoneEnemyFaceDown" })
            {
                var component = FieldReference(card, _cardView, name, true);
                if (Alive(component)) yield return GameOf(component);
            }
            if (!options.IncludeShadow)
            {
                foreach (var name in new[] { "_CardShadow", "_CardNameShadow" })
                {
                    var root = FieldReference(card, _cardRenderer, name, true);
                    if (Alive(root)) yield return root;
                }
            }
        }

        private sealed class SourceCamera
        {
            public IntPtr Camera, Renderer;
            public IntPtr[] Features;
            public int RendererIndex;
        }

        private SourceCamera FindSourceCamera(IntPtr mainRenderer)
        {
            var layer = Get<int>(_gameObject, GameOf(mainRenderer), "layer");
            var layerBit = 1 << layer;
            var center = Get<CaptureBounds>(_renderer, mainRenderer, "bounds").Center;
            var mainCamera = GetReference(_camera, IntPtr.Zero, "main");
            var report = new StringBuilder();
            report.AppendLine("Card renderer: " + ObjectName(mainRenderer) + "; layer " + layer + "; center " + FormatVector(center));
            SourceCamera result = null;
            var highestRank = -1;
            var highestDepth = float.MinValue;
            foreach (var camera in FindObjects(_camera))
            {
                if (!Alive(camera)) continue;
                report.AppendLine();
                report.AppendLine("Camera: " + ObjectName(camera) + (camera == mainCamera ? " [MainCamera]" : ""));
                try
                {
                    var enabled = Get<bool>(_behaviour, camera, "isActiveAndEnabled");
                    var mask = Get<int>(_camera, camera, "cullingMask");
                    var includesLayer = (mask & layerBit) != 0;
                    var viewport = CallValue<CaptureVector3>(_camera, camera, "WorldToViewportPoint",
                        new[] { "UnityEngine.Vector3" }, Il2CppArgument.Value(center));
                    var inViewport = viewport.IsFinite && viewport.Z > 0 && viewport.X >= 0 && viewport.X <= 1 && viewport.Y >= 0 && viewport.Y <= 1;
                    var depth = Get<float>(_camera, camera, "depth");
                    report.AppendLine("  Enabled=" + enabled + "; mask=0x" + mask.ToString("X8") + "; includes card layer=" + includesLayer +
                        "; viewport=" + FormatVector(viewport) + "; depth=" + depth.ToString("G4", CultureInfo.InvariantCulture));

                    var data = ComponentOf(camera, _cameraData);
                    var rendererIndex = Alive(data) ? _runtime.ReadField<int>(data, _runtime.Field(_cameraData, "m_RendererIndex")) : -1;
                    
                    var renderer = Call(_pipeline, IntPtr.Zero, "GetRenderer",
                        new[] { "UnityEngine.Camera", UrpNamespace + ".UniversalAdditionalCameraData" },
                        Il2CppArgument.Ref(camera), Il2CppArgument.Ref(Alive(data) ? data : IntPtr.Zero));
                    report.AppendLine("  URP data=" + Alive(data) + "; renderer index=" + rendererIndex + "; renderer=" + _runtime.ObjectTypeName(renderer));
                    if (renderer == IntPtr.Zero)
                    {
                        report.AppendLine("  Rejected: no URP renderer.");
                        continue;
                    }
                    var features = Features(renderer);
                    foreach (var feature in features)
                    {
                        if (!Alive(feature)) { report.AppendLine("  Feature: missing/destroyed"); continue; }
                        var active = Get<bool>(_feature, feature, "isActive");
                        var description = "  Feature: " + _runtime.ObjectTypeName(feature) + " (" + ObjectName(feature) + "); active=" + active;
                        if (_multiPass != null && _runtime.IsInstance(feature, _multiPass))
                        {
                            var featureMask = _runtime.ReadField<int>(feature, _runtime.Field(_multiPass, "_LayerMask"));
                            description += "; card-pass mask=0x" + featureMask.ToString("X8");
                        }
                        if (_runtime.IsInstance(feature, _renderObjects))
                            description += "; preserved object/stencil pass";
                        report.AppendLine(description);
                    }
                    if (features.Length == 0) report.AppendLine("  Features: none.");
                   
                    if (!enabled || !viewport.IsFinite || viewport.Z <= 0 || !CaptureVector3.Finite(depth))
                    {
                        report.AppendLine("  Rejected:" + (!enabled ? " camera disabled;" : "") +
                            (!viewport.IsFinite || viewport.Z <= 0 ? " card center is behind the camera or invalid;" : "") +
                            (!CaptureVector3.Finite(depth) ? " invalid depth;" : ""));
                        continue;
                    }
                    
                    var rank = (inViewport ? 4 : 0) + (includesLayer ? 2 : 0) + (camera == mainCamera ? 1 : 0);
                    report.AppendLine("  Eligible: rank=" + rank + ".");
                    if (rank < highestRank || (rank == highestRank && depth <= highestDepth)) continue;
                    highestRank = rank;
                    highestDepth = depth;
                    result = new SourceCamera { Camera = camera, Renderer = renderer, RendererIndex = rendererIndex, Features = features };
                }
                catch (Exception e)
                {
                    report.AppendLine("  Inspection failed: " + e.Message);
                }
            }
            if (result == null)
                throw new InvalidOperationException("No compatible card camera was found. Copy the camera diagnostics below. No offscreen render was submitted.\n\n" + report);
            return result;
        }

        private string ObjectName(IntPtr obj) => _runtime.StringValue(GetReference(_object, obj, "name"));
        private static string FormatVector(CaptureVector3 value) => string.Format(CultureInfo.InvariantCulture,
            "({0:G4}, {1:G4}, {2:G4})", value.X, value.Y, value.Z);

        private void ConfigureCamera(IntPtr camera, IntPtr cameraObject, CaptureQuaternion rotation, IntPtr target,
            CaptureFraming framing, int mask, bool orthographic, float fieldOfView, CardCaptureOptions options)
        {
            var transform = GetReference(_gameObject, cameraObject, "transform");
            Set(_transform, transform, "position", "UnityEngine.Vector3", framing.Position);
            Set(_transform, transform, "rotation", "UnityEngine.Quaternion", rotation);
            Call(_camera, camera, "ResetWorldToCameraMatrix", NoArgs);
            Call(_camera, camera, "ResetProjectionMatrix", NoArgs);
            Call(_camera, camera, "ResetCullingMatrix", NoArgs);
            Set(_camera, camera, "cullingMask", "System.Int32", mask);
            Set(_camera, camera, "useOcclusionCulling", "System.Boolean", false);
            Set(_camera, camera, "allowMSAA", "System.Boolean", false);
            Set(_camera, camera, "allowDynamicResolution", "System.Boolean", false);
            Set(_camera, camera, "stereoTargetEye", "UnityEngine.StereoTargetEyeMask", 0);
            Set(_camera, camera, "clearFlags", "UnityEngine.CameraClearFlags", 2); // Solid color, not skybox/depth-only.
            Set(_camera, camera, "backgroundColor", "UnityEngine.Color", new CaptureColor(1));
            Set(_camera, camera, "rect", "UnityEngine.Rect", new CaptureRect(1, 1));
            SetReference(_camera, camera, "targetTexture", "UnityEngine.RenderTexture", target);
            Set(_camera, camera, "aspect", "System.Single", (float)options.Width / options.Height);
            Set(_camera, camera, "orthographic", "System.Boolean", orthographic);
            Set(_camera, camera, "orthographicSize", "System.Single", framing.OrthographicSize);
            Set(_camera, camera, "fieldOfView", "System.Single", fieldOfView);
            Set(_camera, camera, "nearClipPlane", "System.Single", framing.Near);
            Set(_camera, camera, "farClipPlane", "System.Single", framing.Far);
        }

        private void PrepareScene(List<IntPtr> roots, IntPtr[] exclusions, HashSet<IntPtr> allowed, IntPtr[] features)
        {
            _unrelatedRenderers = FindObjects(_renderer).Where(r => !allowed.Contains(r)).ToArray();
            var allowedCanvases = new HashSet<IntPtr>(roots.SelectMany(r => Descendants(r, _canvas)));
            foreach (var excluded in exclusions)
                foreach (var canvas in Descendants(excluded, _canvas)) allowedCanvases.Remove(canvas);
            _unrelatedCanvases = FindObjects(_canvas).Where(c => Alive(c) &&
                (!allowedCanvases.Contains(c) || Get<int>(_canvas, c, "renderMode") != 2)).ToArray(); // WorldSpace only.
            var terrainType = _runtime.Class("UnityEngine.TerrainModule.dll", "UnityEngine", "Terrain", true);
            _terrains = terrainType == null ? Array.Empty<IntPtr>() : FindObjects(terrainType);
            _suppressedFeatures = features.Where(f => Alive(f) && !_permittedFeatures.Any(t => _runtime.IsInstance(f, t))).ToArray();
            _matteFeatures = features.Where(f => Alive(f) && !_coverageFeatures.Any(t => _runtime.IsInstance(f, t))).ToArray();

            var black = GetReference(_texture2D, IntPtr.Zero, "blackTexture");
            var reversed = Get<bool>(Engine("SystemInfo"), IntPtr.Zero, "usesReversedZBuffer");
            var depth = reversed ? black : GetReference(_texture2D, IntPtr.Zero, "whiteTexture");
            _cameraInputs = new[] { "_CameraOpaqueTexture", "_CameraDepthTexture", "_CameraNormalsTexture", "_AfterPostProcessTexture" }
                .Select(name => (Key: _runtime.String(name), Texture: name == "_CameraDepthTexture" ? depth : black)).ToArray();
        }

        private void SuppressUnrelatedRenderers(CaptureStateScope state)
        {
            foreach (var renderer in _unrelatedRenderers)
            {
                if (!Alive(renderer) || Get<bool>(_renderer, renderer, "forceRenderingOff")) continue;
                var captured = renderer;
                state.RestoreWith(() => { if (Alive(captured)) Set(_renderer, captured, "forceRenderingOff", "System.Boolean", false); });
                Set(_renderer, renderer, "forceRenderingOff", "System.Boolean", true);
            }
        }

        private void SuppressCanvases(CaptureStateScope state)
        {
            foreach (var canvas in _unrelatedCanvases)
            {
                if (!Alive(canvas) || !Get<bool>(_behaviour, canvas, "enabled")) continue;
                var captured = canvas;
                state.RestoreWith(() => { if (Alive(captured)) Set(_behaviour, captured, "enabled", "System.Boolean", true); });
                Set(_behaviour, canvas, "enabled", "System.Boolean", false);
            }
        }

        private void SuppressTerrain(CaptureStateScope state)
        {
            foreach (var terrain in _terrains)
            {
                if (!Alive(terrain) || !Get<bool>(_behaviour, terrain, "enabled")) continue;
                
                var captured = terrain;
                state.RestoreWith(() => { if (Alive(captured)) Set(_behaviour, captured, "enabled", "System.Boolean", true); });
                Set(_behaviour, terrain, "enabled", "System.Boolean", false);
            }
        }

        private void SuppressFeatures(IntPtr[] features, CaptureStateScope state)
        {
            foreach (var feature in features)
            {
                if (!Alive(feature) || !Get<bool>(_feature, feature, "isActive")) continue;
                var captured = feature;
                state.RestoreWith(() => { if (Alive(captured)) Call(_feature, captured, "SetActive", new[] { "System.Boolean" }, Il2CppArgument.Value(true)); });
                Call(_feature, feature, "SetActive", new[] { "System.Boolean" }, Il2CppArgument.Value(false));
            }
        }

        private void ResetCameraInputs(CaptureStateScope state)
        {
            foreach (var input in _cameraInputs)
            {
                var original = Call(_shader, IntPtr.Zero, "GetGlobalTexture", new[] { "System.String" }, Il2CppArgument.Ref(input.Key));
                state.RestoreWith(() => Call(_shader, IntPtr.Zero, "SetGlobalTexture", new[] { "System.String", "UnityEngine.Texture" },
                    Il2CppArgument.Ref(input.Key), Il2CppArgument.Ref(Alive(original) ? original : IntPtr.Zero)));
                Call(_shader, IntPtr.Zero, "SetGlobalTexture", new[] { "System.String", "UnityEngine.Texture" },
                    Il2CppArgument.Ref(input.Key), Il2CppArgument.Ref(input.Texture));
            }
        }

        private IntPtr NewRenderTexture(int width, int height, int depth, int format, CaptureStateScope resources)
        {
            var target = NewUnityObject(_renderTexture, resources,
                new[] { "System.Int32", "System.Int32", "System.Int32", "UnityEngine.RenderTextureFormat", "UnityEngine.RenderTextureReadWrite" },
                Il2CppArgument.Value(width), Il2CppArgument.Value(height), Il2CppArgument.Value(depth),
                Il2CppArgument.Value(format), Il2CppArgument.Value(0));
            resources.RestoreWith(() => { if (Alive(target)) Call(_renderTexture, target, "Release", NoArgs); });
            Set(_renderTexture, target, "antiAliasing", "System.Int32", 1);
            Set(_renderTexture, target, "useMipMap", "System.Boolean", false);
            Set(_renderTexture, target, "autoGenerateMips", "System.Boolean", false);
            if (!CallValue<bool>(_renderTexture, target, "Create", NoArgs))
                throw new InvalidOperationException("Unity could not allocate the capture render texture.");
            return target;
        }

        private static bool HasVisiblePixels(byte[] rgba)
        {
            for (var i = 0; i < rgba.Length; i += 4)
                if (rgba[i] > 2 || rgba[i + 1] > 2 || rgba[i + 2] > 2) return true;
            return false;
        }

        private IntPtr NewUnityObject(IL2CppClassWrapper type, CaptureStateScope resources, string[] signature, params Il2CppArgument[] args)
        {
            var obj = _runtime.NewObject(type);
            resources.RestoreWith(() => { if (Alive(obj)) Call(_object, IntPtr.Zero, "Destroy", new[] { "UnityEngine.Object" }, Il2CppArgument.Ref(obj)); });
            Call(type, obj, ".ctor", signature, args);
            return obj;
        }

        private IntPtr[] Features(IntPtr renderer)
        {
            var getter = _runtime.Method(_scriptableRenderer, "get_rendererFeatures", false);
            var list = _runtime.Invoke(getter, renderer);
            return _runtime.ListReferences(list, _runtime.ClassFromType(getter.ReturnType));
        }

        private IntPtr[] FindObjects(IL2CppClassWrapper type) => _runtime.ReferenceArray(Call(_object, IntPtr.Zero,
            "FindObjectsOfType", new[] { "System.Type", "System.Boolean" }, Il2CppArgument.Ref(_runtime.TypeObject(type)), Il2CppArgument.Value(false)));
        private IntPtr[] Descendants(IntPtr root, IL2CppClassWrapper type) => !Alive(root) ? Array.Empty<IntPtr>() : _runtime.ReferenceArray(
            Call(_gameObject, root, "GetComponentsInChildren", new[] { "System.Type", "System.Boolean" },
                Il2CppArgument.Ref(_runtime.TypeObject(type)), Il2CppArgument.Value(true)));
        private IntPtr ComponentOf(IntPtr component, IL2CppClassWrapper type) => Call(_component, component,
            "GetComponent", new[] { "System.Type" }, Il2CppArgument.Ref(_runtime.TypeObject(type)));
        private IntPtr GameOf(IntPtr component) => GetReference(_component, component, "gameObject");
        private bool Alive(IntPtr value) => value != IntPtr.Zero && CallValue<bool>(_object, IntPtr.Zero,
            "op_Implicit", new[] { "UnityEngine.Object" }, Il2CppArgument.Ref(value));
        private bool Visible(IntPtr renderer) => Alive(renderer) && Get<bool>(_renderer, renderer, "enabled") &&
            !Get<bool>(_renderer, renderer, "forceRenderingOff") && Get<bool>(_gameObject, GameOf(renderer), "activeInHierarchy");
        private IntPtr FieldReference(IntPtr obj, IL2CppClassWrapper owner, string name, bool optional = false) =>
            _runtime.ReadReference(obj, _runtime.Field(owner, name, optional));
        private IL2CppClassWrapper Engine(string name) => _runtime.Class(Core, "UnityEngine", name);
        private void RequireSize(IL2CppClassWrapper type, int size)
        {
            if (!type.IsValueType || type.ValueSize != size) throw new NotSupportedException("Unexpected Unity layout: " + type.Name);
        }
        private IntPtr Call(IL2CppClassWrapper type, IntPtr obj, string name, string[] signature, params Il2CppArgument[] args) =>
            _runtime.Invoke(_runtime.Method(type, name, obj == IntPtr.Zero, signature), obj, args);
        private T CallValue<T>(IL2CppClassWrapper type, IntPtr obj, string name, string[] signature, params Il2CppArgument[] args) where T : unmanaged =>
            _runtime.InvokeValue<T>(_runtime.Method(type, name, obj == IntPtr.Zero, signature), obj, args);
        private IntPtr GetReference(IL2CppClassWrapper type, IntPtr obj, string property) => Call(type, obj, "get_" + property, NoArgs);
        private T Get<T>(IL2CppClassWrapper type, IntPtr obj, string property) where T : unmanaged => CallValue<T>(type, obj, "get_" + property, NoArgs);
        private void Set<T>(IL2CppClassWrapper owner, IntPtr obj, string property, string valueType, T value) where T : unmanaged =>
            Call(owner, obj, "set_" + property, new[] { valueType }, Il2CppArgument.Value(value));
        private void SetReference(IL2CppClassWrapper owner, IntPtr obj, string property, string valueType, IntPtr value) =>
            Call(owner, obj, "set_" + property, new[] { valueType }, Il2CppArgument.Ref(value));
    }
}
