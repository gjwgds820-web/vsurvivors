using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Data/CharacterDatabase")]
public class CharacterDatabase : ScriptableObject
{
    public List<CharacterData> characters = new List<CharacterData>();
}