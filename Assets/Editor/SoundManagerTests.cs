#if UNITY_6000_0_OR_NEWER
using NUnit.Framework;

public sealed class SoundManagerTests
{
    [Test]
    public void LinearToDecibels_ZeroIsFiniteAndOneIsZeroDb()
    {
        float zero = SoundManager.LinearToDecibels(0f);
        Assert.That(float.IsNaN(zero) || float.IsInfinity(zero), Is.False);
        Assert.That(SoundManager.LinearToDecibels(1f), Is.EqualTo(0f).Within(0.001f));
    }
}
#endif
