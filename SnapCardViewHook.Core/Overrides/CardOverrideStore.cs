using System;
using System.Collections.Generic;
using System.Linq;

namespace SnapCardViewHook.Core.Overrides
{
    internal sealed class CardOverrideStore
    {
        private volatile Dictionary<string, CardOverrideRule> _rules =
            new Dictionary<string, CardOverrideRule>(StringComparer.Ordinal);

        public CardOverrideRule[] GetRules() => _rules.Values.Select(rule => rule.Copy()).ToArray();

        public void Replace(IEnumerable<CardOverrideRule> rules)
        {
            _rules = rules.ToDictionary(rule => rule.TargetCardId, rule => rule.Copy(), StringComparer.Ordinal);
        }

        public void Save(CardOverrideRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.TargetCardId))
                throw new ArgumentException("Choose an original card.");
            var rules = new Dictionary<string, CardOverrideRule>(_rules, StringComparer.Ordinal);
            rules[rule.TargetCardId] = rule.Copy();
            _rules = rules;
        }

        public void Remove(string targetCardId)
        {
            var rules = new Dictionary<string, CardOverrideRule>(_rules, StringComparer.Ordinal);
            rules.Remove(targetCardId);
            _rules = rules;
        }

        public CardOverrideRule Resolve(string originalCardId)
        {
            var rules = _rules;
            return originalCardId != null && rules.TryGetValue(originalCardId, out var rule) && rule.Enabled
                ? rule : null;
        }

        public void Clear() => _rules = new Dictionary<string, CardOverrideRule>(StringComparer.Ordinal);
    }
}
