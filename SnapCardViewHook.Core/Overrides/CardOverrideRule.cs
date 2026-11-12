namespace SnapCardViewHook.Core.Overrides
{
    internal sealed class CardOverrideRule
    {
        public string TargetCardId { get; set; }
        public bool Enabled { get; set; } = true;
        public string ReplacementCardId { get; set; }
        public string VariantId { get; set; }
        public bool EnsureVariantMatches { get; set; } = true;
        public string SurfaceEffectId { get; set; }
        public string RevealEffectId { get; set; }
        public string BorderId { get; set; }
        public string CardBackId { get; set; }
        public string FactionId { get; set; }
        public int? Cost { get; set; }
        public int? Power { get; set; }
        public bool Force3D { get; set; }
        public string Description { get; set; }

        public CardOverrideRule Copy() => (CardOverrideRule)MemberwiseClone();

        public override string ToString()
        {
            var replacement = ReplacementCardId == null ? "" : " -> " + ReplacementCardId;
            return TargetCardId + replacement;
        }
    }
}
