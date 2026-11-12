using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SnapCardViewHook.Core.Overrides
{
    internal sealed class CardOverrideConfigs
    {
        private const int CurrentVersion = 1;
        private readonly CardOverrideStore _store;
        private CardOverrideRule[] _pendingRules;
        private bool _initialized;

        public string DirectoryPath { get; } = Path.Combine(
            Path.GetDirectoryName(typeof(CardOverrideConfigs).Assembly.Location), "OverrideConfigs");
        public string CurrentName { get; private set; }
        public bool HasPendingChanges => _pendingRules != null;

        public CardOverrideConfigs(CardOverrideStore store)
        {
            _store = store;
        }

        public string[] GetNames()
        {
            Directory.CreateDirectory(DirectoryPath);
            return Directory.GetFiles(DirectoryPath, "*.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public void Initialize()
        {
            if (_initialized) return;
            var names = GetNames();
            if (names.Length == 0)
            {
                Write("Default", _store.GetRules(), false);
                CurrentName = "Default";
            }
            else
            {
                Select(names.FirstOrDefault(name => string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase)) ?? names[0]);
            }
            _initialized = true;
        }

        public void Select(string name)
        {
            Flush();
            name = ValidateName(name);
            var document = JsonConvert.DeserializeObject<ConfigDocument>(File.ReadAllText(ConfigPath(name)),
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None });
            if (document == null || document.Version != CurrentVersion || document.Rules == null)
                throw new InvalidDataException("This is not a supported override config.");

            var targets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in document.Rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.TargetCardId) || !targets.Add(rule.TargetCardId))
                    throw new InvalidDataException("Every rule must have a unique original card ID.");
                if (rule.Cost < -99999 || rule.Cost > 99999 || rule.Power < -99999 || rule.Power > 99999)
                    throw new InvalidDataException("Card cost and power must be between -99999 and 99999.");
            }

            _store.Replace(document.Rules);
            CurrentName = name;
            _initialized = true;
        }

        public void Create(string name)
        {
            Flush();
            name = ValidateName(name);
            Write(name, Array.Empty<CardOverrideRule>(), false);
            _store.Clear();
            CurrentName = name;
            _initialized = true;
        }

        public void Rename(string name)
        {
            if (CurrentName == null) throw new InvalidOperationException("Select a config to rename.");
            Flush();
            name = ValidateName(name);
            if (name == CurrentName) return;
            var destination = ConfigPath(name);
            if (!string.Equals(name, CurrentName, StringComparison.OrdinalIgnoreCase) && File.Exists(destination))
                throw new IOException("A config with that name already exists.");
            File.Move(ConfigPath(CurrentName), destination);
            CurrentName = name;
        }

        public void Delete()
        {
            if (CurrentName == null) return;
            File.Delete(ConfigPath(CurrentName));
            _pendingRules = null;
            CurrentName = null;
            _store.Clear();
        }

        public void QueueSave()
        {
            if (CurrentName == null) throw new InvalidOperationException("Create or select a config first.");
            _pendingRules = _store.GetRules();
        }

        public void Flush()
        {
            if (_pendingRules == null) return;
            Write(CurrentName, _pendingRules, true);
            _pendingRules = null;
        }

        private string ConfigPath(string name) => Path.Combine(DirectoryPath, ValidateName(name) + ".json");

        private static string ValidateName(string name)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith("."))
                throw new ArgumentException("Enter a config name without filename characters such as /, \\, : or *.");
            return name;
        }

        private void Write(string name, CardOverrideRule[] rules, bool overwrite)
        {
            Directory.CreateDirectory(DirectoryPath);
            var path = ConfigPath(name);
            if (!overwrite && File.Exists(path)) throw new IOException("A config with that name already exists.");
            var json = JsonConvert.SerializeObject(new ConfigDocument
            {
                Version = CurrentVersion,
                Rules = rules.OrderBy(rule => rule.TargetCardId, StringComparer.OrdinalIgnoreCase).ToArray()
            }, Formatting.Indented);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                if (overwrite && File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private sealed class ConfigDocument
        {
            public int Version { get; set; }
            public CardOverrideRule[] Rules { get; set; }
        }
    }
}
