using UnityEngine;

public enum PollenCapacityType

{
    Bee,
    Hive,
    Flower
}

[System.Serializable]
public class PollenCapacity
{
    public PollenCapacityType type;
    public int capacity;
}