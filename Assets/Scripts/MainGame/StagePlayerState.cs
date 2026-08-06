
using System;

namespace InTheArena.MainGame
{
    public class StagePlayerState
    {
        public int Gold { get; set; }

        public StagePlayerState()
        {
        }

        public void CopyFrom(InTheArena.Save.PlayerProgressState data)
        {
            if (data != null)
            {
                Gold = data.Gold;
            }
        }

        public void ApplyTo(InTheArena.Save.PlayerProgressState data)
        {
            if (data != null)
            {
                data.SetGold(Gold);
            }
        }
    }
}

