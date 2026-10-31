using System.Collections.Generic;
using UnityEngine;

public class FlowerArea : MonoBehaviour
{
    private readonly List<FlowerNectar> flowers = new();

    private void OnTriggerEnter(Collider other)
    {
        FlowerNectar flower = other.GetComponentInParent<FlowerNectar>();

        if (flower != null && !flowers.Contains(flower))
            flowers.Add(flower);
    }

    private void OnTriggerExit(Collider other)
    {
        FlowerNectar flower = other.GetComponentInParent<FlowerNectar>();

        if (flower != null)
            flowers.Remove(flower);
    }

    public bool TryCollectNectar()
    {
        for (int i = 0; i < flowers.Count; i++)
        {
            if (flowers[i] == null)
            {
                flowers.RemoveAt(i);
                i--;
                continue;
            }

            if (flowers[i].TryTakeNectar())
                return true;
        }

        return false;
    }

    public void RegisterFlower(FlowerNectar flower)
    {
        if (flower != null && !flowers.Contains(flower))
            flowers.Add(flower);
    }
}