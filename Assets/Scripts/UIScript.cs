using UnityEngine;
using UnityEngine.SceneManagement;

public class UIScript : MonoBehaviour
{
    public GameObject options;
    public GameObject drone;

    public void closeButtonAction()
    {
        options.SetActive(false);
        PlayerPrefs.Save();
        SceneManager.LoadScene(0);
    }

    public void arudinoSupportAction(bool b)
    {
        PlayerPrefs.SetInt("arduinoEnabled", b ? 1 : 0);
    }

    public void stabilizationAction(bool b)
    {
        PlayerPrefs.SetInt("stabilizationEnabled", b ? 1 : 0);
    }

    public void baudRateChanged(string s)
    {
        if (s == "") return;
        PlayerPrefs.SetInt("arduinoBaudRate", int.Parse(s));
    }

    public void portNameChanged(string s)
    {
        if (s == "") return;
        PlayerPrefs.SetInt("arduinoPortName", int.Parse(s));
    }


}
