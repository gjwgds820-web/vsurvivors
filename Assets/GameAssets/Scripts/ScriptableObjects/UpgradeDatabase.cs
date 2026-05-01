using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "UpgradeDatabase", menuName = "Data/UpgradeDatabase")]
public class UpgradeDatabase : ScriptableObject
{
    public List<UpgradeData> upgrades = new List<UpgradeData>();
}