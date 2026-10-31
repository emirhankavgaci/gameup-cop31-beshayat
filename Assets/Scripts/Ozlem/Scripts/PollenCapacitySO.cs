using System.Collections.Generic;
using UnityEngine;

[
    CreateAssetMenu
    (
        fileName = "PollenCapacitySO",
        menuName = "Scriptable Objects/PollenCapacitySO"
    )
    
]


public class PollenCapacitySO : ScriptableObject
{

    public List<PollenCapacity> capacities;

}
