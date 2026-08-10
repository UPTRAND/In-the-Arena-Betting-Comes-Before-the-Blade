#if UNITY_6000_0_OR_NEWER
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SoundManagerTests
{
    [Test]
    public void LinearToDecibels_ZeroIsFiniteAndOneIsZeroDb()
    {
        float zero = SoundManager.LinearToDecibels(0f);
        Assert.That(float.IsNaN(zero) || float.IsInfinity(zero), Is.False);
        Assert.That(SoundManager.LinearToDecibels(1f), Is.EqualTo(0f).Within(0.001f));
    }

    [TestCase("main_title", "Assets/Resources/BGM/MainTitle.mp3")]
    [TestCase("betting_phase", "Assets/Resources/BGM/BettingPase.mp3")]
    [TestCase("battle_phase_1", "Assets/Resources/BGM/BattlePase_1.mp3")]
    [TestCase("battle_phase_2", "Assets/Resources/BGM/BattlePase_2.mp3")]
    [TestCase("battle_phase_3", "Assets/Resources/BGM/BattlePase_3.mp3")]
    public void Catalog_ResolvesConfiguredBgm(string id, string clipPath)
    {
        SoundCatalog catalog =
            AssetDatabase.LoadAssetAtPath<SoundCatalog>("Assets/ScriptableObject/SoundCatalog.asset");
        AudioClip expected = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);

        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.TryGetBgm(id, out AudioClip actual), Is.True);
        Assert.That(actual, Is.SameAs(expected));
    }

    [TestCase("betting_win", "Assets/Resources/SFX/BettingWin.mp3")]
    [TestCase("betting_fail", "Assets/Resources/SFX/BettingFail.mp3")]
    [TestCase("button_positive", "Assets/Resources/SFX/Button_Positive.mp3")]
    [TestCase("button_negative", "Assets/Resources/SFX/Button_Negative.mp3")]
    [TestCase("unit_death", "Assets/Resources/SFX/Unit_Deat.mp3")]
    [TestCase("unit_hit", "Assets/Resources/SFX/Unit_Hit.mp3")]
    [TestCase("unit_skill", "Assets/Resources/SFX/Unit_Skill.mp3")]
    public void Catalog_ResolvesConfiguredSfx(string id, string clipPath)
    {
        SoundCatalog catalog =
            AssetDatabase.LoadAssetAtPath<SoundCatalog>("Assets/ScriptableObject/SoundCatalog.asset");
        AudioClip expected = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);

        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.TryGetSfx(id, out AudioClip actual), Is.True);
        Assert.That(actual, Is.SameAs(expected));
    }
}
#endif
