using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SnapCardViewHook.Core.AssetLab
{
    internal sealed class AssetLabController
    {
        private AssetLabScan _scan;
        private AssetLabPreview _preview;
        private volatile bool _enabled;
        private int _generation;
        private volatile string _error;

        public string Error => _error;
        public bool Active => _enabled;

        public async Task<AssetLabEntry[]> ScanAsync(IProgress<string> progress, CancellationToken cancellation)
        {
            var generation = Interlocked.Increment(ref _generation);
            var scan = await Run(() =>
            {
                cancellation.ThrowIfCancellationRequested();
                RequireGeneration(generation);
                StopCore();
                _error = null;
                _scan?.Dispose();
                return _scan = new AssetLabScan(progress, cancellation);
            }).ConfigureAwait(false);
            try { return await scan.Completion.ConfigureAwait(false); }
            catch
            {
                await Run(() =>
                {
                    if (ReferenceEquals(_scan, scan)) { _scan = null; scan.Dispose(); }
                    return true;
                }).ConfigureAwait(false);
                throw;
            }
        }

        public Task<string> PreviewAsync(AssetLabEntry entry, string name, string description, int cost, int power, AssetLabBake bake)
        {
            var generation = Volatile.Read(ref _generation);
            return Run(() =>
            {
                RequireGeneration(generation);
                if (_scan == null) throw new InvalidOperationException("Scan the local assets before previewing.");
                var next = new AssetLabPreview(_scan, entry, name, description, cost, power, bake);
                StopCore();
                _preview = next;
                next.Activate();
                _error = null;
                _enabled = true;
                try
                {
                    return AssetLabPreview.RefreshOpenCard()
                        ? "Previewing " + entry.AssetId + (bake != null ? " using " + bake + " baked artwork (2D)." : " using 3D materials.") +
                            " Card views are overridden while Asset Lab is active."
                        : "Preview active: open any card in the game to view " + entry.AssetId + ".";
                }
                catch { StopCore(); throw; }
            });
        }

        public Task<bool> StopAsync()
        {
            _enabled = false;
            return Run(() => { StopCore(); _error = null; return true; });
        }

        public void Close()
        {
            _enabled = false;
            var generation = Interlocked.Increment(ref _generation);
            SnapTypeDataCollector.ExecuteActionInGameUiThread(() =>
            {
                if (generation != Volatile.Read(ref _generation)) return;
                try { StopCore(); _scan?.Dispose(); _scan = null; }
                catch (Exception error) { Debug.WriteLine("Asset Lab cleanup: " + error); }
            });
        }

        private void StopCore()
        {
            _enabled = false;
            var previous = _preview;
            _preview = null;
            previous?.Dispose();
        }

        public bool TryOverride(IntPtr view, IntPtr card, int cost, int power, int rarity,
            IntPtr border, IntPtr variant, IntPtr surface, IntPtr reveal, int revealType, bool showReveal,
            int logo, IntPtr back, bool morph, bool transparent, IntPtr faction, IntPtr methodInfo)
        {
            if (!_enabled || _preview == null) return false;
            try
            {
                _preview.Initialize(new AssetLabPreview.ViewState
                {
                    View = view, Card = card, Cost = cost, Power = power, Rarity = rarity, Border = border,
                    Variant = variant, Surface = surface, Reveal = reveal, RevealType = revealType, ShowReveal = showReveal,
                    Logo = logo, Back = back, Morph = morph, Transparent = transparent, Faction = faction, MethodInfo = methodInfo
                });
                return true;
            }
            catch (Exception error)
            {
                _enabled = false;
                _error = error.Message;
                Debug.WriteLine("Asset Lab preview: " + error);
                var failed = _preview;
                SnapTypeDataCollector.ExecuteActionInGameUiThread(() =>
                {
                    if (ReferenceEquals(_preview, failed)) StopCore();
                });
                return false;
            }
        }

        public string DescriptionOverride(IntPtr descriptionEvent) =>
            _enabled ? _preview?.Description(descriptionEvent) : null;

        private void RequireGeneration(int generation)
        {
            if (generation != Volatile.Read(ref _generation)) throw new OperationCanceledException();
        }

        private static Task<T> Run<T>(Func<T> action)
        {
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            SnapTypeDataCollector.ExecuteActionInGameUiThread(() =>
            {
                try { completion.TrySetResult(action()); }
                catch (OperationCanceledException) { completion.TrySetCanceled(); }
                catch (Exception error) { completion.TrySetException(error); }
            });
            return completion.Task;
        }
    }
}
