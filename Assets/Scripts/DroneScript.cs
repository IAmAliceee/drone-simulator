using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO.Ports;
using System.Threading;
using JetBrains.Annotations;

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
    public GameObject optionsUI;
    public TMP_Text statusText;
    public AudioSource droneSound;

    public Transform cameraTP;
    public Transform cameraFP;

    private float throttleInput;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool stabilizationEnabled = false;
    private bool firstPersonEnabled = true;
    private bool cameraRotationEnabled = false;
    private float battery;

    private CircularBuffer throttleAverage;

    public float pidScale;
    private float pidPositionY;
    private Vector2 movementInput;

    // Arduino
    public bool arduinoEnabled = false;
    public static int threadRefreshSpeed = 20;
    Thread IOThread = new(DataThread);
    private static SerialPort sp;
    private static string incomingMsg = "";
    private static string arduinoPortName = "COM0";
    private static int arduinoBaudRate = 0;

    private static void DataThread()
    {
        sp = new SerialPort(arduinoPortName, arduinoBaudRate);
        sp.Open();

        while (true)
        {
            incomingMsg = sp.ReadLine();
            Thread.Sleep(threadRefreshSpeed);
        }
    }


    private void Awake()
    {
        stabilizationEnabled = Helper.GetPrefInt("stabilizationEnabled") != 0;
        arduinoEnabled = Helper.GetPrefInt("arduinoEnabled") != 0;
        arduinoPortName = "COM" + Helper.GetPrefInt("arduinoPortName");
        arduinoBaudRate = Helper.GetPrefInt("arduinoBaudRate");


        controls = new();
        controls.Drone.Restart.performed += _ => SceneManager.LoadScene(0);
        controls.Drone.ThirdPerson.performed += _ => swapPerspective();
        controls.Drone.LockCamera.performed += _ => cameraRotationEnabled = !cameraRotationEnabled;

        controls.Player.Options.performed += ctx => optionsUI.SetActive(true);

        if (arduinoEnabled) return;

        if (stabilizationEnabled)
        {
            controls.DroneS.Look.performed += ctx =>
            {
                Vector2 v = ctx.ReadValue<Vector2>();
                movementInput.x = v.x;
                pidPositionY = transform.position.y + pidScale * v.y * v.y * v.y;
            };
            controls.DroneS.Move.performed += ctx =>
            {
                Vector2 v = ctx.ReadValue<Vector2>();

                movementInput.y = v.y;
                lookInput = new(v.x, 0f);
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

        /*
        statusText.text = string.Format(
            "Battery: {0}%\nThrottle: {1}%",
            Mathf.RoundToInt(battery / batteryMax * 100f).ToString(),
            Mathf.RoundToInt(throttleInput * 100f).ToString()
            );

        battery -= Time.deltaTime * .04f * Mathf.Pow(throttleInput, 2) * throttleInput * maxThrust;
        */
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
    PIDController pidPitch = new(4, 0, 1);
    PIDController pidRoll = new(4, 0, 1);
    private const float maxAngle = 50f;
    private const float Kp_roll_pitch = 2f;

    private void stabilizeDrone()
    {
        Vector3 pos = transform.position;

        throttleInput = Mathf.Clamp(pidY.UpdatePID(pidPositionY, pos.y, Time.fixedDeltaTime), 0f, 1f);

        float pitch = normalizeAngle(transform.rotation.eulerAngles.x);
        float roll = normalizeAngle(transform.rotation.eulerAngles.z);

        float desired_pitch = movementInput.y * maxAngle;
        float pitch_error = desired_pitch - pitch;

        float pitch_input = Mathf.Clamp(Kp_roll_pitch * pitch_error, -1f, 1f);
        moveInput.y = pitch_input;

        float desired_roll = -movementInput.x * maxAngle;
        float roll_error = desired_roll - roll;

        float roll_input = Mathf.Clamp(Kp_roll_pitch * roll_error, -1f, 1f);
        moveInput.x = -roll_input;
    }

    private float normalizeAngle(float angle)
    {
        return (angle > 180f) ? angle - 360f : angle;
    }
}
