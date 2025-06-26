using UnityEngine;

public class Helper : MonoBehaviour
{
    private void Start()
    {
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
    }

    public static int GetPrefInt(string key)
    {
        if (!PlayerPrefs.HasKey(key)) return 0;
        return PlayerPrefs.GetInt(key);
    }
}
