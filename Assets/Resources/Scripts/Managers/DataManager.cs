using UnityEngine;
using System.IO;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    public UserData currentUserData;
    private String saveFilePath;
    private Dictionary<int, SkillData> skillDatabase = new Dictionary<int, SkillData>();

    public List<SkillData> SelectedOptions = new List<SkillData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            saveFilePath = Path.Combine(Application.persistentDataPath, "UserData.json");
            LoadGame();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #region Save and Load
    public void SaveGame()
    {
        try
        {
            string jsonData = JsonConvert.SerializeObject(currentUserData, Formatting.Indented);
            File.WriteAllText(saveFilePath, jsonData);
            // Debug.Log("Game saved successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to save game: " + e.Message);
        }
    }

    public void LoadGame()
    {
        if (File.Exists(saveFilePath))
        {
            try
            {
                string jsonData = File.ReadAllText(saveFilePath);
                currentUserData = JsonConvert.DeserializeObject<UserData>(jsonData);
                //Debug.Log("Game loaded successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to load game: " + e.Message);
            }
        }
        else
        {
            // Debug.Log("No save file found. Starting a new game.");
            currentUserData = new UserData();
            // SaveGame();
        }
    }

    public List<int> GetFormation(int formationIndex)
    {
        int[] formation = currentUserData.FormationData[formationIndex];
        List<int> shadowIDs = new List<int>();
        for (int i = 0; i < formation.Length; i++)
        {
            shadowIDs.Add(formation[i]);
        }
        return shadowIDs;
    }

    private void OnApplicationQuit()
    {
        // SaveGame();
    }
    #endregion

    #region Skill Database
    public void LoadDataFromCSV()
    {
        // TODO: CSV 파일에서 스킬 데이터를 읽어와 skillDatabase 딕셔너리에 저장하는 로직 구현
    }

    public SkillData GetSkillData(int id)
    {
        return skillDatabase.ContainsKey(id) ? skillDatabase[id] : null;
    }

    public List<SkillData> GetAllPassiveSkills()
    {
        List<SkillData> passiveSkills = new List<SkillData>();
        foreach (var skill in skillDatabase.Values)
        {
            if (skill.Type == SkillType.Passive)
            {
                passiveSkills.Add(skill);
            }
        }
        return passiveSkills;
    }

    #endregion
}