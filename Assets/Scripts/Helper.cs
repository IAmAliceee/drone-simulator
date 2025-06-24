using UnityEngine;

public class Helper : MonoBehaviour
{
    private void Start()
    {
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
    }

}
