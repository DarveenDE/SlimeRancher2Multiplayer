using MelonLoader.Utils;
using Newtonsoft.Json;

namespace SR2MP.Shared.Utils;

public static class RuntimeReferenceDump
{
    private const string DumpSchemaVersion = "1";

    public static string DefaultDirectory
    {
        get
        {
            var configuredDirectory = Environment.GetEnvironmentVariable("SR2MP_REFERENCE_DUMP_DIR");
            return string.IsNullOrWhiteSpace(configuredDirectory)
                ? Path.Combine(MelonEnvironment.UserDataDirectory, "SR2MP", "reference-dumps")
                : Environment.ExpandEnvironmentVariables(configuredDirectory);
        }
    }

    public static string Write(string? outputDirectory = null)
    {
        outputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? DefaultDirectory
            : Environment.ExpandEnvironmentVariables(outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        var document = BuildDocument();
        var fileName = $"runtime-lookups-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        var path = Path.Combine(outputDirectory, fileName);

        File.WriteAllText(path, JsonConvert.SerializeObject(document, Formatting.Indented));
        return path;
    }

    private static RuntimeReferenceDumpDocument BuildDocument()
    {
        var context = GameContext.Instance;
        if (context == null)
            throw new InvalidOperationException("GameContext is not initialized.");

        var translation = context.AutoSaveDirector?._saveReferenceTranslation;
        if (translation == null)
            throw new InvalidOperationException("SaveReferenceTranslation is not initialized.");

        var document = new RuntimeReferenceDumpDocument
        {
            SchemaVersion = DumpSchemaVersion,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("o"),
        };

        foreach (var entry in translation._identifiableTypeLookup)
        {
            AddEntry(document.IdentifiableTypes, entry.key, translation.GetPersistenceId(entry.value), entry.value);
        }

        foreach (var entry in translation._pediaEntryLookup)
        {
            AddEntry(document.PediaEntries, entry.key, null, entry.value);
        }

        foreach (var entry in translation._resourceGrowerTranslation.RawLookupDictionary)
        {
            AddEntry(document.ResourceGrowers, entry.key, translation.GetPersistenceId(entry.value), entry.value);
        }

        foreach (var entry in translation._sceneGroupTranslation.RawLookupDictionary)
        {
            AddEntry(document.SceneGroups, entry.key, translation.GetPersistenceId(entry.value), entry.value);
        }

        foreach (var entry in translation._componentTranslation.RawLookupDictionary)
        {
            AddEntry(document.UpgradeComponents, entry.key, translation.GetPersistenceId(entry.value), entry.value);
        }

        foreach (var entry in translation._statusEffectTranslation.RawLookupDictionary)
        {
            AddEntry(document.StatusEffects, entry.key, translation.GetPersistenceId(entry.value), entry.value);
        }

        foreach (var entry in translation._gameIconTranslation.RawLookupDictionary)
        {
            AddEntry(document.GameIcons, entry.key, translation.GetPersistenceId(entry.value), entry.value);
        }

        foreach (var entry in translation._zoneDefinitionTranslation.RawLookupDictionary)
        {
            AddEntry(document.ZoneDefinitions, entry.key, translation.GetPersistenceId(entry.value), entry.value);
        }

        foreach (var entry in translation._weatherPatternTranslation.RawLookupDictionary)
        {
            AddEntry(document.WeatherPatterns, entry.key, translation.GetPersistenceId(entry.value), entry.value);
        }

        foreach (var entry in translation._weatherStateTranslation.RawLookupDictionary)
        {
            AddEntry(document.WeatherStates, entry.key, translation.GetPersistenceId(entry.value), entry.value);
        }

        foreach (var entry in translation._disruptionAreaTranslation.RawLookupDictionary)
        {
            AddEntry(document.DisruptionAreas, entry.key, translation.GetPersistenceId(entry.value), entry.value);
        }

        foreach (var entry in translation._themeDefinitionTranslation.RawLookupDictionary)
        {
            AddEntry(document.ThemeDefinitions, entry.key, translation.GetPersistenceId(entry.value), entry.value);
        }

        return document;
    }

    private static void AddEntry(List<RuntimeReferenceEntry> entries, string key, int? persistenceId, object? value)
    {
        entries.Add(new RuntimeReferenceEntry
        {
            Key = key,
            PersistenceId = persistenceId,
            Name = GetObjectName(value),
            RuntimeType = value?.GetType().FullName ?? string.Empty,
        });
    }

    private static string GetObjectName(object? value)
    {
        if (value is Object unityObject && unityObject)
            return unityObject.name;

        return value?.ToString() ?? string.Empty;
    }

    private sealed class RuntimeReferenceDumpDocument
    {
        public string SchemaVersion { get; set; } = DumpSchemaVersion;
        public string GeneratedAtUtc { get; set; } = string.Empty;
        public List<RuntimeReferenceEntry> IdentifiableTypes { get; } = new();
        public List<RuntimeReferenceEntry> PediaEntries { get; } = new();
        public List<RuntimeReferenceEntry> ResourceGrowers { get; } = new();
        public List<RuntimeReferenceEntry> SceneGroups { get; } = new();
        public List<RuntimeReferenceEntry> UpgradeComponents { get; } = new();
        public List<RuntimeReferenceEntry> StatusEffects { get; } = new();
        public List<RuntimeReferenceEntry> GameIcons { get; } = new();
        public List<RuntimeReferenceEntry> ZoneDefinitions { get; } = new();
        public List<RuntimeReferenceEntry> WeatherPatterns { get; } = new();
        public List<RuntimeReferenceEntry> WeatherStates { get; } = new();
        public List<RuntimeReferenceEntry> DisruptionAreas { get; } = new();
        public List<RuntimeReferenceEntry> ThemeDefinitions { get; } = new();
    }

    private sealed class RuntimeReferenceEntry
    {
        public string Key { get; set; } = string.Empty;
        public int? PersistenceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RuntimeType { get; set; } = string.Empty;
    }
}
