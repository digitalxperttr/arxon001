using UnityEngine;

public class FireEffect : IBlockEffect
{
    public void Trigger(Block owner)
    {
        if (GridManager.Instance == null)
            return;

        GridManager.Instance.DestroyBlocksByColor(owner.blockColor);
    }
}