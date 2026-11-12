using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SnapCardViewHook.Core.AssetLab
{
    internal sealed class AssetLabEntry
    {
        public string AssetId { get; set; }
        public string CardId { get; set; }
        public string Kind => Variant ? "Variant" : "Card";
        public string Status { get; set; }
        public string Availability { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Cost { get; set; }
        public int Power { get; set; }
        public bool Variant { get; set; }
        public bool Stray { get; set; }
        public bool CanPreview { get; set; }
        public bool CanPreviewMaterials { get; set; }
        public bool CanPreviewBaked { get; set; }
        public bool HasWipMaterials { get; set; }
        public bool HasCardData { get; set; }
        public IntPtr CardDefinition { get; set; }
        public IntPtr RenderDefinition { get; set; }
        public Dictionary<string, AssetLabResource> Materials { get; } =
            new Dictionary<string, AssetLabResource>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, AssetLabResource> Textures { get; } =
            new Dictionary<string, AssetLabResource>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, Dictionary<string, AssetLabResource>> BakeSets { get; } =
            new Dictionary<int, Dictionary<string, AssetLabResource>>();
        public List<AssetLabBake> Bakes { get; } = new List<AssetLabBake>();

        public string Details => Status + Environment.NewLine + Availability + Environment.NewLine +
            (HasCardData ? "Card data: client definition." : "Card data: inferred name and placeholder 0/0 stats; no ability is inferred.") +
            Environment.NewLine + ResourceDetails("3D materials", Materials) + Environment.NewLine +
            (Bakes.Count == 0 ? ResourceDetails("Baked artwork (incomplete)", Textures) :
                string.Join(Environment.NewLine, Bakes.Select(bake => ResourceDetails(bake.ToString(), bake.Textures))));

        private static string ResourceDetails(string title, Dictionary<string, AssetLabResource> resources) =>
            title + Environment.NewLine + string.Join(Environment.NewLine, resources.OrderBy(p => p.Key).Select(p =>
                p.Key + (p.Value.Placeholder ? " (WIP/placeholder reference)" : string.Empty) + ": " + p.Value.Path +
                Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", p.Value.Bundles)));

        public static string FriendlyName(string id)
        {
            return Regex.Replace(Regex.Replace(id ?? string.Empty, "([A-Z]+)([A-Z][a-z])", "$1 $2"),
                "([a-z0-9])([A-Z])", "$1 $2").Replace('_', ' ');
        }
    }

    internal sealed class AssetLabResource
    {
        public string Key;
        public string Path;
        public bool Local;
        public bool Placeholder;
        public int Priority;
        public string[] Bundles = Array.Empty<string>();
    }

    internal sealed class AssetLabBake
    {
        public string Name;
        public Dictionary<string, AssetLabResource> Textures;
        public bool Placeholder => Textures.Values.Any(resource => resource.Placeholder);
        public override string ToString() => Name + (Placeholder ? " (WIP reference)" : string.Empty);
    }

    internal sealed class AssetLabCardData
    {
        public IntPtr Definition;
        public string Id, Name, Description;
        public int Cost, Power;
    }
}
