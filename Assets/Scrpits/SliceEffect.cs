using UnityEngine;

public class SliceEffect : IBlockEffect
{
    public void Trigger(Block owner)
    {
        if (GridManager.Instance == null)
            return;

        GridManager.Instance.TriggerSlice(owner);
    }
}