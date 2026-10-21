using SixConsole.App.Services;
using SixConsole.App.Services.Shortcuts;

namespace SixConsole.Tests;

public sealed class SafeConfigImportTests : IDisposable
{
    private readonly string _dir;

    public SafeConfigImportTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "sixconsole-import-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Valid_benign_configuration_validates_and_imports_successfully()
    {
        var store = new SettingsStore(_dir);
        var services = new AppServices(store);

        string validJson = """
        {
          "schemaVersion": 2,
          "profileName": "Tournament Settings",
          "themePreference": "Dark",
          "restoreLastSelection": true,
          "shortcuts": [
            {
              "actionId": "NavigateToSettings",
              "virtualKey": 188,
              "modifiers": 1,
              "key": "Comma",
              "scope": 0,
              "isEnabled": true
            }
          ]
        }
        """;

        var validation = SafeConfigImporter.Validate(validJson);
        Assert.True(validation.IsValid);
        Assert.Empty(validation.Errors);
        Assert.NotEmpty(validation.AcceptedChanges);

        bool applied = SafeConfigImporter.Apply(validation, store, services, out var error);
        Assert.True(applied);
        Assert.Empty(error);

        var loaded = store.Load();
        Assert.Equal("Dark", loaded.ThemePreference);
        Assert.True(loaded.RestoreLastSelection);
    }

    [Fact]
    public void Executable_Lua_and_automation_tokens_are_strictly_rejected()
    {
        string luaPayload = """
        function OnEvent(event, arg)
            if event == "MOUSE_BUTTON_PRESSED" and arg == 1 then
                repeat
                    PressKey("p")
                    MoveMouseRelative(0, 40)
                until not IsMouseButtonPressed(1)
            end
        end
        """;

        var validation = SafeConfigImporter.Validate(luaPayload);

        Assert.False(validation.IsValid);
        Assert.NotEmpty(validation.Errors);
        Assert.Contains(validation.Errors, e => e.Contains("prohibited automation or script token"));
    }

    [Fact]
    public void Recoil_tuning_parameters_in_import_are_strictly_rejected()
    {
        string recoilJson = """
        {
          "schemaVersion": 2,
          "recoilMultiplier": 2.5,
          "vertVariance": 10
        }
        """;

        var validation = SafeConfigImporter.Validate(recoilJson);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Contains("prohibited automation"));
    }

    [Fact]
    public void Oversized_payload_exceeding_64kb_is_rejected()
    {
        string largeContent = new string('x', SafeConfigImporter.MaxFileSizeBytes + 10);
        var validation = SafeConfigImporter.Validate(largeContent);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Contains("exceeds safe size limit"));
    }

    [Fact]
    public void Unsupported_schema_version_is_rejected()
    {
        string badVersionJson = """
        {
          "schemaVersion": 999,
          "themePreference": "Light"
        }
        """;

        var validation = SafeConfigImporter.Validate(badVersionJson);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Contains("Unsupported schema version"));
    }

    [Fact]
    public void Failed_import_leaves_existing_settings_unchanged()
    {
        var store = new SettingsStore(_dir);
        var original = new SettingsStore.PersistedSettings
        {
            ThemePreference = "Light",
            RecoilMultiplier = 0.9,
        };
        store.Save(original);
        var services = new AppServices(store);

        string invalidJson = """
        {
          "schemaVersion": 2,
          "themePreference": "InvalidThemeName"
        }
        """;

        var validation = SafeConfigImporter.Validate(invalidJson);
        Assert.False(validation.IsValid);

        bool applied = SafeConfigImporter.Apply(validation, store, services, out _);
        Assert.False(applied);

        var current = store.Load();
        Assert.Equal("Light", current.ThemePreference);
        Assert.Equal(0.9, current.RecoilMultiplier);
    }
}
