
using System.Collections.Generic;
using UnityEngine;

public class CameraObstacleFade : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private LayerMask fadeLayer;

    [Header("Fade")]
    [SerializeField, Range(0.1f, 1f)]
    private float fadedAlpha = 0.3f;

    [SerializeField] private float fadeSpeed = 5f;
    [SerializeField] private float sphereRadius = 0.25f;

    private readonly Dictionary<Renderer, Material[]> originalMaterials = new();
    private readonly Dictionary<Renderer, Material[]> fadedMaterials = new();

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 direction = target.position - transform.position;
        float distance = direction.magnitude;

        if (distance < 0.01f)
            return;

        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            sphereRadius,
            direction.normalized,
            distance,
            fadeLayer,
            QueryTriggerInteraction.Ignore
        );

        HashSet<Renderer> visibleObstacles = new();

        foreach (RaycastHit hit in hits)
        {
            Renderer renderer =
                hit.collider.GetComponent<Renderer>();

            if (renderer == null)
                renderer =
                    hit.collider.GetComponentInChildren<Renderer>();

            if (renderer == null)
                continue;

            visibleObstacles.Add(renderer);

            if (!originalMaterials.ContainsKey(renderer))
            {
                originalMaterials[renderer] =
                    renderer.sharedMaterials;

                Material[] materials = renderer.materials;

                foreach (Material mat in materials)
                {
                    mat.SetFloat("_Surface", 1f);
                    mat.SetFloat("_SrcBlend",
                        (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend",
                        (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0f);

                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = 3000;
                }

                fadedMaterials[renderer] = materials;
            }

            foreach (Material mat in fadedMaterials[renderer])
            {
                Color color = mat.GetColor("_BaseColor");

                color.a = Mathf.MoveTowards(
                    color.a,
                    fadedAlpha,
                    fadeSpeed * Time.deltaTime
                );

                mat.SetColor("_BaseColor", color);
            }
        }

        foreach (Renderer renderer in new List<Renderer>(
            fadedMaterials.Keys))
        {
            if (visibleObstacles.Contains(renderer))
                continue;

            Material[] materials = fadedMaterials[renderer];

            foreach (Material mat in materials)
            {
                Color color = mat.GetColor("_BaseColor");

                color.a = Mathf.MoveTowards(
                    color.a,
                    1f,
                    fadeSpeed * Time.deltaTime
                );

                mat.SetColor("_BaseColor", color);
            }

            if (materials.Length > 0 &&
                Mathf.Approximately(
                    materials[0].GetColor("_BaseColor").a,
                    1f))
            {
                renderer.sharedMaterials =
                    originalMaterials[renderer];

                fadedMaterials.Remove(renderer);
                originalMaterials.Remove(renderer);
            }
        }
    }
}
