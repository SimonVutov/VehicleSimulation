using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static Unity.VisualScripting.Member;

public class car : MonoBehaviour
{
    public Text text;
    bool tractionControl = true;
    bool turningControl = true;
    float thresholdToTurnOff = 5.0f;

    float weightFeel = 0.0005f;
    float stopWheelSpinFeel = 0.01f;

    Rigidbody rb;
    Ray ray;

    //SUSPENSION
    float length = 1.05f;
    float damp = 500f;
    float springForce = 380f;

    //DRIVING
    float wheelRadius = 0.5f;

    //CAR
    float fLen = 1.8f;
    float sLen = 1.15f;
    Vector3[] wheelPos = new Vector3[4];

    //ENGINE
    float enginerps = 0;
    float smoothedEnginerps = 0f;
    float[] wheelrps = new float[4];
    float engineTorque = 0.14f;

    int mode = 0; //0 is drive, 1 is reverse, 2 is neutral, 3 is parked

    public GameObject wheel;
    GameObject[] wheels = new GameObject[4];
    private float[] lastVelocity = new float[4];

    Vector3 relitiveMovement;
    Vector3[] RRRrelitiveMovement = new Vector3[4];
    Vector3[] GGGlobalMovement = new Vector3[4];
    Vector2 input;

    string[] slip = new string[4];
    float[] slipAmount = new float[4];

    float currentGearRatio = 1;
    float smoothedTurn = 0;

    float runningTotalTurnSlip = 1;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        ray = new Ray(transform.position, -transform.up);
        for (int i = 0; i < 4; i++) { wheels[i] = Instantiate(wheel, transform.position, Quaternion.identity); wheels[i].transform.parent = transform; }
        rb.centerOfMass = new Vector3(0f, -0.37f, 0f);
    }

    void FixedUpdate()
    {
        rb.AddForce(-transform.up * transform.InverseTransformDirection(rb.velocity).z);

        input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        input.x /= (1 + runningTotalTurnSlip * slipAmount[0]);
        if (slipAmount[0] > 0.9f || slipAmount[1] > 0.9f) runningTotalTurnSlip += 0.1f;
        else if (runningTotalTurnSlip > 0.4f) runningTotalTurnSlip -= 0.1f;
        input.x = Mathf.Clamp(input.x, -1, 1);
        smoothedTurn = Mathf.Lerp(smoothedTurn, input.x, 0.28f);

        Debug.Log(RRRrelitiveMovement[0]);

        Vector3[] positions = { new Vector3(sLen, 0, fLen), new Vector3(-sLen, 0, fLen), new Vector3(sLen, 0, -fLen), new Vector3(-sLen, 0, -fLen) };
        for (int j = 0; j < 4; j++) wheelPos[j] = transform.position + transform.right * positions[j].x + transform.forward * positions[j].z;

        int i = 0;
        foreach (Vector3 pos in wheelPos)
        {
            RaycastHit hit;
            if (Physics.Raycast(pos, -transform.up, out hit, length))
            {
                // Suspension
                float compression = (length - hit.distance) / length;
                float dampingForce = damp * (lastVelocity[i] - compression);
                if (lastVelocity[i] - compression > 0) dampingForce = 0;//-dampingForce;
                else dampingForce = -dampingForce;
                float springForceMagnitude = Mathf.Clamp(springForce * compression + dampingForce, 0, Mathf.Infinity);
                rb.AddForceAtPosition(hit.normal * springForceMagnitude, hit.point);
                lastVelocity[i] = compression;

                // Driving
                Vector3 tangentForword = -Vector3.Cross(hit.normal, transform.right);
                Vector3 tangentRight = Vector3.Cross(hit.normal, transform.forward);

                relitiveMovement = -1 * rb.GetPointVelocity(pos);
                Vector3 forward = (i == 2 || i == 3) ? tangentForword : (tangentForword + tangentRight * smoothedTurn).normalized; //turning
                relitiveMovement += forward * wheelrps[i] * 2 * wheelRadius * Mathf.PI; //add forwards and backwards tire spinning, control rps
                Debug.DrawLine(pos, pos + forward * 100);

                // Ff = mgμ
                float Fn = springForceMagnitude;
                float μs = 3.2f; // 0.8 for asphalt
                float μk = 0.6f; // 0.4 for asphalt
                float MaximumForce = Fn * μs;
                float threshold = 350f; //higher threshold means easier slip
                slipAmount[i] = relitiveMovement.magnitude * threshold / MaximumForce; // above 1 means sliding

                if (slipAmount[i] < 1.0f)
                {
                    rb.AddForceAtPosition(relitiveMovement * MaximumForce, hit.point);
                    slip[i] = "-";
                }
                else
                {
                    rb.AddForceAtPosition(relitiveMovement.normalized * (MaximumForce / threshold) * μk * Fn, hit.point);
                    slip[i] = ".";
                }

                // Wheel
                wheels[i].transform.position = hit.point + transform.up * wheelRadius;
                wheels[i].transform.rotation = transform.rotation;
                wheels[i].transform.GetChild(0).transform.Rotate(new Vector3(0, -wheelrps[i] * 360 / 50, 0));
                if (i == 0 || i == 1) wheels[i].transform.localEulerAngles = new Vector3(0, 45 * smoothedTurn, 0);
            }
            else
            {
                lastVelocity[i] = 0f; // Reset velocity if not grounded
            }

            RRRrelitiveMovement[i] = transform.InverseTransformDirection(relitiveMovement);
            GGGlobalMovement[i] = relitiveMovement;
            i++; //KEEP THIS AT THE END OF THE LOOP
        }
        text.text = slip[0] + slip[1] + "\n" + slip[2] + slip[3];

        //if (wheelrps < 0.1f) mode = 3;
        //else if (wheelrps < 0.1f && input.y > 0.1f) mode = 0;
        //else if (wheelrps < 0.1f && input.y < 0.1f) mode = 1;

        if (mode == 0) drive();
        else if (mode == 1) reverse();
        else if (mode == 2) neutral();
        else if (mode == 3) parked();

        smoothedEnginerps = Mathf.Lerp(smoothedEnginerps, enginerps, 0.8f);
    }

    void drive()
    {
        Debug.Log("drive");
        for (int i = 0; i < 4; i++)
        {
            if (slipAmount[i] < 1)
            {
                wheelrps[i] -= (RRRrelitiveMovement[2].z + RRRrelitiveMovement[3].z) * weightFeel * 10;
                enginerps -= ((RRRrelitiveMovement[2].z + RRRrelitiveMovement[3].z) * weightFeel) / 4;
            } else
            {
                wheelrps[i] -= (RRRrelitiveMovement[2].z + RRRrelitiveMovement[3].z) * stopWheelSpinFeel * 10;
                enginerps -= ((RRRrelitiveMovement[2].z + RRRrelitiveMovement[3].z) * stopWheelSpinFeel) / 4;
            }

            wheelrps[i] = smoothedEnginerps * currentGearRatio;
            enginerps += engineTorque * input.y / (1 + 3 * slipAmount[i]);
        }
    }

    void reverse()
    {
        Debug.Log("reverse");
        neutral();
    }

    void neutral()
    {
        enginerps -= (RRRrelitiveMovement[2].z + RRRrelitiveMovement[3].z) * 0.01f;
    }

    void parked()
    {
        Debug.Log("park");
        neutral();
    }

    private void OnDrawGizmos()
    {
        for (int i = 0; i < 4; i++) Gizmos.DrawSphere(wheelPos[i] + GGGlobalMovement[i] * 10f, 1f);
    }
}