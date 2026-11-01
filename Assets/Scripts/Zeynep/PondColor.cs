
using UnityEngine;

[ExecuteAlways]
public class PondColor : MonoBehaviour
{
    [Header("Pond Color")]
    [SerializeField]
    private Color pondColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    private Renderer pondRenderer;
    private MaterialPropertyBlock propertyBlock;

    private void OnEnable()
    {
        ApplyColor();
    }

    private void OnValidate()
    {
        ApplyColor();
    }

    private void ApplyColor()
    {
        if (pondRenderer == null)
            pondRenderer = GetComponent<Renderer>();

        if (pondRenderer == null)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        pondRenderer.GetPropertyBlock(propertyBlock);

        propertyBlock.SetColor("_BaseColor", pondColor);

        pondRenderer.SetPropertyBlock(propertyBlock);
    }
}
