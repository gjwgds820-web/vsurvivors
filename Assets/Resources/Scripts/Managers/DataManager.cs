using UnityEngine;
using System.IO;
using System;
using Newtonsoft.Json;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    public UserData currentUserData;
    private String saveFilePath;

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

    private void OnApplicationQuit()
    {
        // SaveGame();
    }
}