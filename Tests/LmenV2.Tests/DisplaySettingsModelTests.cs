using System.Text.Json;
using LmenV2.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LmenV2.Tests;

[TestClass]
public sealed class DisplaySettingsModelTests
{
    [TestMethod]
    public void PrimaryDimmingMapsToLegacyBackdropBrightnessKey()
    {
        var settings = new SettingsModel
        {
            PrimaryBrightness = 1.2,
            PrimaryContrast = 0.9,
            SecondaryBrightness = 0.8,
            SecondaryContrast = 1.1,
            SecondaryBackdropDimming = 0.4,
            PrimaryBackdropDimming = 0.25
        };

        Assert.AreEqual(0.75, settings.BackdropBrightness, 0.0001);
        Assert.AreEqual(0.25, settings.PrimaryBackdropDimming, 0.0001);

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(settings));
        JsonElement root = document.RootElement;
        Assert.IsTrue(root.TryGetProperty("BackdropBrightness", out _));
        Assert.IsFalse(root.TryGetProperty("PrimaryBackdropDimming", out _));
        Assert.AreEqual(1.2, root.GetProperty("PrimaryBrightness").GetDouble(), 0.0001);
        Assert.AreEqual(0.9, root.GetProperty("PrimaryContrast").GetDouble(), 0.0001);
        Assert.AreEqual(0.8, root.GetProperty("SecondaryBrightness").GetDouble(), 0.0001);
        Assert.AreEqual(1.1, root.GetProperty("SecondaryContrast").GetDouble(), 0.0001);
        Assert.AreEqual(0.4, root.GetProperty("SecondaryBackdropDimming").GetDouble(), 0.0001);
    }

    [TestMethod]
    public void LowLatencyModePersistsWithDisplaySettings()
    {
        var settings = new SettingsModel { LowLatencyMode = true };

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(settings));

        Assert.IsTrue(document.RootElement.GetProperty("LowLatencyMode").GetBoolean());
    }
}
