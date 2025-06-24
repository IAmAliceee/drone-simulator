using UnityEngine;

public class UIScript : MonoBehaviour
{
    public GameObject options;
    public GameObject drone;

    public void closeButtonAction()
    {
        options.SetActive(false);
    }

    public void arudinoSupportAction(bool b)
    {
        drone.GetComponent<DroneScript>().arduinoEnabled = b;
    }
}
