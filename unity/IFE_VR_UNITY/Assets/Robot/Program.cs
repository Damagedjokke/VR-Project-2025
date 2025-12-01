
using UnityEngine;
using System;
using System.Diagnostics;
using Valve.VR;
using Debug = UnityEngine.Debug;


namespace Robot
{
    public class Program : MonoBehaviour
    {
        //Robot IP Configuration - Change this to connect to different robots
        private static string robotIP = "192.168.56.101";   //School robot: 158.39.162.177  |   School robot (Gripper): 158.39.162.151  |   URSim: 192.168.56.101

        // Debug toggles - set to true to enable specific debug categories
        private static bool debugConnectivity = true;     // Connection status, setup, socket events
        private static bool debugData = true;              // Data values, outputs, inputs, forces
        private static bool debugPerformance = true;       // Timing, framerate measurements
        private static bool debugTroubleshooting = true;   // Error states, forced triggers

        [SerializeField] private GameObject rightController;
        [SerializeField] private GameObject vrCamera;
        [SerializeField] private GameObject lockedVrCameraCopy;
        private RobotArmUnity robotArmUnity;
        private bool startTransmittingData;
        private int gripperButton = -1;
        private Stopwatch stopwatch;

        static UniversalRobot_Outputs UrOutputs = new UniversalRobot_Outputs();
        UniversalRobot_Inputs UrInputs = new UniversalRobot_Inputs();

        private bool isConnected;
       
       static void Ur3_OnSockClosed(object sender, EventArgs e)
       {
           if (debugConnectivity) Debug.Log("Closed");
       }

       // Data change Event (fires at ~500Hz - very noisy, usually leave this disabled)
       static void Ur3_OnDataReceive(object sender, EventArgs e)
       {
          // Uncomment below to see high-frequency data updates
          // if (debugData) Debug.Log(UrOutputs.actual_TCP_pose[0]);
       }
        
       RtdeClient Ur3 = new RtdeClient();
        // Start is called before the first frame update

        async void Start()
        {

            SteamVR.settings.lockPhysicsUpdateRateToRenderFrequency = false;
            
            stopwatch= new Stopwatch();

            robotArmUnity = new RobotArmUnity(rightController ,vrCamera, lockedVrCameraCopy);
            
            // Connection using the protocol version 2 (allows update frequency less or equal to 125 Hz)
            
            Ur3.OnSockClosed += Ur3_OnSockClosed;

            isConnected = await Ur3.ConnectAsync(robotIP, 2); 
            if (isConnected) {
                if (debugConnectivity) Debug.Log("Successfully connected.");
            } else {
                if (debugConnectivity) Debug.LogError("Failed to connect.");
            }

            // Register Inputs (UR point of view)
            if (debugConnectivity) Debug.Log(Ur3.Setup_Ur_Inputs(UrInputs));


            UrInputs.input_int_register_24 = 0;


            // Register Outputs (UR point of view)
            if (debugConnectivity) Debug.Log(Ur3.Setup_Ur_Outputs(UrOutputs,500));
            Ur3.OnDataReceive += Ur3_OnDataReceive;

            // Request the UR to send back Outputs periodically
            Ur3.Ur_ControlStart();
            
            //SetLockedCameraTransform();
        }


        private float currentValue = 0;
        private float targetValue;
        bool hasRun = false;
        void Update()
        {

            stopwatch.Restart();
            
            //Checks if second bit is 0, checking if the program on robot is running.
            if (((UrOutputs.robot_status_bits & (1 << 1)) == 0 || UrOutputs.output_int_register_25 == 1) && !hasRun)
            {
                hasRun = true; 
                robotArmUnity.FreezeRobot();
                robotArmUnity.ResetPose();
                UrInputs.input_double_register_24 = 0;
                UrInputs.input_double_register_25 = 0;
                UrInputs.input_double_register_26 = 0;
                UrInputs.input_double_register_27 = 0;
                UrInputs.input_double_register_28 = 3.14;
                UrInputs.input_double_register_29 = 0;
                UrInputs.input_double_register_47 = 0; //Reset pkt number
                if (debugTroubleshooting) Debug.LogError("Forced off triggered");
            }
            else if (UrOutputs.output_int_register_24 == 1)
            {
                hasRun = false;
                robotArmUnity.UnFreezeRobot();
                UrInputs.input_int_register_24 = 1;
            }
            
            else if (UrOutputs.output_int_register_24 == 0)
            {
                UrInputs.input_int_register_24 = 0;
            }

            bool isControllingRobot = robotArmUnity.UpdateRobotPose();

            if (isControllingRobot)
            {
                UrInputs.input_double_register_24 = robotArmUnity.PosVector[0];
                UrInputs.input_double_register_25 = robotArmUnity.PosVector[1];
                UrInputs.input_double_register_26 = robotArmUnity.PosVector[2];
                UrInputs.input_double_register_27 = robotArmUnity.AxisVector[0];
                UrInputs.input_double_register_28 = robotArmUnity.AxisVector[1];
                UrInputs.input_double_register_29 = robotArmUnity.AxisVector[2];
            }
            else
            {
                UrInputs.input_double_register_24 = 0;
                UrInputs.input_double_register_25 = 0;
                UrInputs.input_double_register_26 = 0;
            }

            UrInputs.input_double_register_30 = robotArmUnity.RobotArmGripperValue;
            if (UrInputs.input_double_register_47 > 99) UrInputs.input_double_register_47 = 0;
            ++UrInputs.input_double_register_47;
            if (isConnected) Ur3.Send_Ur_Inputs();

            if (debugPerformance && isControllingRobot) {
                TimeSpan ts = stopwatch.Elapsed;
                double totalSeconds = ts.TotalSeconds;
                if (totalSeconds > 0)
                {
                    Debug.Log(string.Format("FPS: {0:F2}", 1.0 / totalSeconds));
                }

                // Log time and robot TCP pose for latency measurement (only when controlling)
                Debug.Log(string.Format("Time: {0:F3}s | TCP Pose: [{1:F3}, {2:F3}, {3:F3}, {4:F3}, {5:F3}, {6:F3}]",
                    Time.time,
                    UrOutputs.actual_TCP_pose[0], UrOutputs.actual_TCP_pose[1], UrOutputs.actual_TCP_pose[2],
                    UrOutputs.actual_TCP_pose[3], UrOutputs.actual_TCP_pose[4], UrOutputs.actual_TCP_pose[5]));
            }

            if (debugData && isControllingRobot) Debug.Log(string.Format("Force values: {0:F2} .. {1:F2} .. {2:F2}",UrOutputs.actual_TCP_force[0], UrOutputs.actual_TCP_force[1], UrOutputs.actual_TCP_force[2]));
           
        
        }

        private void FixedUpdate()
        {
            robotArmUnity.VibrationController(UrOutputs.actual_TCP_force);
        }

        private void OnApplicationQuit()
        {
            UrInputs.input_double_register_46 = 1;
            if (debugConnectivity) Debug.Log(Ur3.Send_Ur_Inputs());
            Ur3.Disconnect();
        }


        public void SetLockedCameraTransform()
        {
            robotArmUnity.SetLockedCameraTransform();
        }
    }
}

