using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class XRCheck : MonoBehaviour
{
    void Start()
    {
        Debug.Log("========== XR CHECK ==========");
        Debug.Log("XR Enabled: " + XRSettings.enabled);
        Debug.Log("XR Device: " + XRSettings.loadedDeviceName);
        Debug.Log("XR Stereo Rendering Mode: " + XRSettings.stereoRenderingMode);

        List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetInstances(displays);
        Debug.Log("XR Display Subsystems Found: " + displays.Count);

        ZEDManager manager = GetComponent<ZEDManager>();
        if (manager)
        {
            Debug.Log("ZED IsStereoRig: " + manager.IsStereoRig);
            Debug.Log("ZED allowARPassThrough: " + manager.allowARPassThrough);
        }
        else
        {
            Debug.LogError("ZEDManager not found!");
        }
    }
}