using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Audio;

namespace TheGallery;

[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.PlayMusic))]
public class TrackCurrentMusicPatch
{
    public static string LastMusicPath { get; private set; }

    public static void Prefix(string music)
    {
        LastMusicPath = music;
    }
}