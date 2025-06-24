using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO.Ports;
using System.Threading;

public class DroneScript : MonoBehaviour
{
    public Transform propFL;
    public Transform propFR;
    public Transform propRL;
    public Transform propRR;

    public float maxThrust = 50f;
    public float pitchFactor = 2f;
    public float rollFactor = 2f;
    public float yawFactor = 5f;
    public float cameraSpeed = 5f;
    public float batteryMax = 100f;

    public Rigidbody rb;
    private InputSystem_Actions controls;
    public TMP_Text statusText;
    public AudioSource droneSound;

    public Transform cameraTP;
    public Transform cameraFP;

    private float throttleInput;
    private Vector2 moveInput;
    private Vector2 lookInput;
    public bool stabilizationEnabled = false;
    private bool firstPersonEnabled = true;
    private bool cameraRotationEnabled = false;
    private float battery;

    private CircularBuffer throttleAverage;

    public float pidScale;
    private Vector3 pidPosition;

    // Arduino
    public bool arduinoEnabled = false;
    public static int threadRefreshSpeed = 20;
    Thread IOThread = new(DataThread);
    private static SerialPort sp;
    private static string incomingMsg = "";

    private static void DataThread()
    {
        sp = new SerialPort("COM11", 9600);
        sp.Open();

        while (true)
        {
            incomingMsg = sp.ReadLine();
            Thread.Sleep(threadRefreshSpeed);
        }
    }


    private void Awake()
    {
        controls = new();
        controls.Drone.Restart.performed += _ => SceneManager.LoadScene(0);
        controls.Drone.ThirdPerson.performed += _ => swapPerspective();
        controls.Drone.LockCamera.performed += _ => cameraRotationEnabled = !cameraRotationEnabled;

        if (arduinoEnabled) return;

        if (stabilizationEnabled)
        {
            controls.DroneS.Look.performed += ctx =>
            {
                Vector2 v = ctx.ReadValue<Vector2>();
                pidPosition.y += pidScale * v.y;
                lookInput = new(v.x, 0f);
            };
            controls.DroneS.Move.performed += ctx =>
            {
                Vector2 v = ctx.ReadValue<Vector2>();
                pidPosition.x += pidScale * v.x;
                pidPosition.z += pidScale * v.y;
            };
            return;
        }

        controls.Drone.Throttle.performed += ctx => throttleInput = ctx.ReadValue<float>();
        controls.Drone.Throttle.canceled += _ => throttleInput = 0;

        controls.Drone.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Drone.Move.canceled += _ => moveInput = Vector2.zero;

        controls.Drone.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        controls.Drone.Look.canceled += _ => lookInput = Vector2.zero;

    }

    private void Start()
    {
        battery = batteryMax;
        if (!droneSound.isPlaying) droneSound.Play();
        if (arduinoEnabled) IOThread.Start();
        pidPosition = transform.position;
        throttleAverage = new();
    }

    private void OnDestroy()
    {
        if (!arduinoEnabled) return;
        IOThread.Abort();
        sp.Close();
    }
    private void OnEnable()
    {
        controls.Enable();
    }
    private void OnDisable()
    {
        controls.Disable();
    }

    private void FixedUpdate()
    {
        if (stabilizationEnabled) stabilizeDrone();

        float baseForce = throttleInput * maxThrust;

        float pitch = -moveInput.y * pitchFactor;
        float roll = moveInput.x * rollFactor;

        float flAdj = +pitch +roll;
        float frAdj = +pitch -roll;
        float rlAdj = -pitch +roll;
        float rrAdj = -pitch -roll;

        float maxAdj = Mathf.Max( Mathf.Abs(flAdj), Mathf.Abs(frAdj), Mathf.Abs(rlAdj), Mathf.Abs(rrAdj) );

        float scale = 1f;
        if(baseForce + maxAdj > maxThrust)
        {
            scale = (maxThrust - baseForce) / maxAdj;
        }

        flAdj *= scale;
        frAdj *= scale;
        rlAdj *= scale;
        rrAdj *= scale;

        float fl = baseForce + flAdj;
        float fr = baseForce + frAdj;
        float rl = baseForce + rlAdj;
        float rr = baseForce + rrAdj;

        rb.AddForceAtPosition(transform.up * fl, propFL.position);
        rb.AddForceAtPosition(transform.up * fr, propFR.position);
        rb.AddForceAtPosition(transform.up * rl, propRL.position);
        rb.AddForceAtPosition(transform.up * rr, propRR.position);

        rb.AddTorque(transform.up * lookInput.x * yawFactor);

        if(cameraRotationEnabled)
            Camera.main.transform.Rotate(Vector3.right * -lookInput.y * cameraSpeed);


        throttleAverage.AddValue(throttleInput);
        float avg = throttleAverage.GetAverage();
        droneSound.pitch = Mathf.Lerp(.8f, 2f, avg);
        droneSound.volume = Mathf.Lerp(.2f, 1f, avg);
    }

    private void Update()
    {
        arduinoUpdateControls();

        statusText.text = string.Format(
            "Battery: {0}%\nThrottle: {1}%",
            Mathf.RoundToInt(battery / batteryMax * 100f).ToString(),
            Mathf.RoundToInt(throttleInput * 100f).ToString()
            );

        battery -= Time.deltaTime * .04f * Mathf.Pow(throttleInput, 2) * throttleInput * maxThrust;
    }

    private void swapPerspective()
    {
        firstPersonEnabled = !firstPersonEnabled;
        Camera.main.gameObject.transform.position = (firstPersonEnabled) ? cameraFP.position : cameraTP.position;
    }

    private void arduinoUpdateControls()
    {
        if (!arduinoEnabled || incomingMsg == "") return;
        string[] inputs = incomingMsg.Split(","); // throttle, leftx, lefty, rightx
        throttleInput = float.Parse(inputs[3]) * .001f;
        moveInput = new Vector2(float.Parse(inputs[0]) / 256f, float.Parse(inputs[1]) / 256f);
        lookInput = new Vector2(float.Parse(inputs[2]) / 256f, 0f);
    }

    PIDController pidX = new(.5f, 0, .3f);
    PIDController pidY = new(1, 1, 1);
    PIDController pidZ = new(.5f, 0, .3f);

    private void stabilizeDrone()
    {
        Vector3 pos = transform.position;

        float maxAngle = Mathf.Deg2Rad * 10f;
        throttleInput = Mathf.Clamp(pidY.UpdatePID(pidPosition.y, pos.y, Time.fixedDeltaTime), 0f, 1f);

        
    }
}
