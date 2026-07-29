#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    [CreateAssetMenu(fileName = "BuffData_", menuName = "In The Arena/Unit/Status Effect/Buff Data")]
    public sealed class BuffData : StatusEffectData
    {
        public override StatusEffectType EffectType => StatusEffectType.Buff;
    }
}
#endif
