using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ShadowDatabase", menuName = "Data/ShadowDatabase")]
public class ShadowDatabase : ScriptableObject
{
    public List<ShadowData> shadows = new List<ShadowData>();
} 