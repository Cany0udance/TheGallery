using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace TheGallery;

[HarmonyPatch(typeof(NCompendiumSubmenu), "_Ready")]
public class AddGalleryButtonPatch
{
    private static NCompendiumBottomButton _galleryButton;

    public static void Postfix(NCompendiumSubmenu __instance)
    {
        try
        {
            var runHistoryButtonField = typeof(NCompendiumSubmenu).GetField("_runHistoryButton",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var runHistoryButton = (NCompendiumBottomButton)runHistoryButtonField.GetValue(__instance);

            _galleryButton = (NCompendiumBottomButton)runHistoryButton.Duplicate();
            _galleryButton.Name = "GalleryButton";

            runHistoryButton.GetParent().AddChild(_galleryButton);

            // Duplicate the material so shader state isn't shared
            var bgPanel = _galleryButton.GetNode<Control>((NodePath)"BgPanel");
            bgPanel.Material = (ShaderMaterial)bgPanel.Material.Duplicate();

            var hsvField = typeof(NCompendiumBottomButton).GetField("_hsv",
                BindingFlags.NonPublic | BindingFlags.Instance);
            hsvField?.SetValue(_galleryButton, bgPanel.Material);
            
            // icon
            
            var icon = _galleryButton.GetNode<TextureRect>((NodePath)"Icon");
            var iconTexture = ResourceLoader.Load<Texture2D>("res://TheGallery/gallery.png");
            if (iconTexture != null)
            {
                icon.Texture = iconTexture;
            }

            // Adjust button color
            var hsvMaterial = (ShaderMaterial)bgPanel.Material;
            hsvMaterial.SetShaderParameter("h", (Variant)0.4f);
            hsvMaterial.SetShaderParameter("s", (Variant)2.5f);
            hsvMaterial.SetShaderParameter("v", (Variant)1.0f);

            // Update cached brightness values so focus/press tweens match
            var defaultV = (float)hsvMaterial.GetShaderParameter("v");
            var defaultVField = typeof(NCompendiumBottomButton).GetField("_defaultV",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var focusVField = typeof(NCompendiumBottomButton).GetField("_focusV",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var pressVField = typeof(NCompendiumBottomButton).GetField("_pressV",
                BindingFlags.NonPublic | BindingFlags.Instance);
            defaultVField?.SetValue(_galleryButton, defaultV);
            focusVField?.SetValue(_galleryButton, defaultV + 0.2f);
            pressVField?.SetValue(_galleryButton, defaultV - 0.2f);

            // Connect click handler
            _galleryButton.Connect(
                NClickableControl.SignalName.Released,
                Callable.From(new Action<NButton>(_ => OpenGalleryScreen(__instance)))
            );

            _galleryButton.SetLocalization("THE_GALLERY");

            // Fix up focus neighbors
            var statisticsButtonField = typeof(NCompendiumSubmenu).GetField("_statisticsButton",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var leaderboardsButtonField = typeof(NCompendiumSubmenu).GetField("_leaderboardsButton",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var bestiaryButtonField = typeof(NCompendiumSubmenu).GetField("_bestiaryButton",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var statisticsButton = (NCompendiumBottomButton)statisticsButtonField.GetValue(__instance);
            var leaderboardsButton = (NCompendiumBottomButton)leaderboardsButtonField.GetValue(__instance);
            var bestiaryButton = (NShortSubmenuButton)bestiaryButtonField.GetValue(__instance);

            // Bottom row: Leaderboards, Statistics, Run History, Gallery
            ((Control)runHistoryButton).FocusNeighborRight = ((Control)_galleryButton).GetPath();
            ((Control)_galleryButton).FocusNeighborLeft = ((Control)runHistoryButton).GetPath();
            ((Control)_galleryButton).FocusNeighborRight = ((Control)_galleryButton).GetPath();
            ((Control)_galleryButton).FocusNeighborBottom = ((Control)_galleryButton).GetPath();
            ((Control)_galleryButton).FocusNeighborTop = ((Control)bestiaryButton).GetPath();
            ((Control)bestiaryButton).FocusNeighborBottom = ((Control)_galleryButton).GetPath();
        }
        catch (Exception e)
        {
            Log.Error($"[Gallery] Failed to add button: {e}");
        }
    }

    private static void OpenGalleryScreen(NCompendiumSubmenu instance)
    {
        var stackField = typeof(NSubmenu).GetField("_stack",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var stack = stackField?.GetValue(instance);

        if (stack != null)
        {
            var screen = new NVfxBrowserScreen();
            ((Control)stack).AddChild(screen);
            var pushMethod = stack.GetType().GetMethod("Push");
            pushMethod?.Invoke(stack, new object[] { screen });
        }
    }

    public static NCompendiumBottomButton GetButton() => _galleryButton;
}

[HarmonyPatch(typeof(NCompendiumSubmenu), "OnSubmenuOpened")]
public class GalleryOnSubmenuOpenedPatch
{
    public static void Postfix()
    {
        var button = AddGalleryButtonPatch.GetButton();
        if (button != null)
        {
            button.Visible = true;
        }
    }
}