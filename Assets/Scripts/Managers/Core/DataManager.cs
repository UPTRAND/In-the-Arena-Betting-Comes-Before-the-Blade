using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;

public class DataManager
{
    int DataNum = 1;
    string FilePath;

    public void Init()
    {
        FilePath = Application.persistentDataPath + "/save";
    }

    public void SaveData()
    {
        PlayerData saveData = new PlayerData();

        if (Managers.nowPlayerData == null) return;

        saveData = Managers.nowPlayerData;

        string data = JsonUtility.ToJson(saveData);
        File.WriteAllText(FilePath + DataNum, data);
    }

    public void LoadData()
    {
        if (!File.Exists(FilePath + DataNum)) return;

        string data = File.ReadAllText(FilePath);

        PlayerData LoadData = JsonUtility.FromJson<PlayerData>(data);

        Managers.nowPlayerData = LoadData;

        Managers.Sound.BGMSoundVolume = Managers.nowPlayerData.BGMSound;
        Managers.Sound.SFXSoundVolume = Managers.nowPlayerData.SFXSound;
    }
}
