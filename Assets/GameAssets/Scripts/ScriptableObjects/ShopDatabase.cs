using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ShopDatabase", menuName = "Data/ShopDatabase")]
public class ShopDatabase : ScriptableObject
{
    public List<ShopData> products = new List<ShopData>();
}
