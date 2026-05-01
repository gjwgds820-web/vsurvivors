using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "StageDatabase", menuName = "Database/Stage Database")]
public class StageDatabase : ScriptableObject
{
    public List<StageData> stages = new List<StageData>();
}