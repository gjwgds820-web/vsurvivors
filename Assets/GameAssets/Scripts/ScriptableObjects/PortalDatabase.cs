using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "PortalDatabase", menuName = "Database/Portal Database")]
public class PortalDatabase : ScriptableObject
{
    public List<PortalData> portals = new List<PortalData>();
}