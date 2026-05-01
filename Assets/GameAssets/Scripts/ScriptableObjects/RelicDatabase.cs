using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "RelicDatabase", menuName = "Data/RelicDatabase")]
public class RelicDatabase : ScriptableObject
{
    public List<RelicData> relics = new List<RelicData>();
}