#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    [CreateAssetMenu(fileName = "DebuffData_", menuName = "In The Arena/Unit/Status Effect/Debuff Data")]
    public sealed class DebuffData : StatusEffectData
    {
        public override StatusEffectType EffectType => StatusEffectType.Debuff;
    }
}
#endif
