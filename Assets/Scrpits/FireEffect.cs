using UnityEngine;

public class FireEffect : IBlockEffect
{
    public void Trigger(Block owner)
    {
        if (GridManager.Instance == null)
            return;

        GridManager.Instance.SetCurrentFireSource(owner);
        Color targetColor = GridManager.Instance.GetColorForGemColor(owner.fireTargetColor);
        GridManager.Instance.DestroyBlocksByColor(targetColor);
    }
}
