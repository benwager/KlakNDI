using UnityEngine;
using System.Collections.Generic;

public class NSDManager
{
#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaObject nsdManager = null;
#endif

    private static NSDManager instance = null;
    public static NSDManager Instance {
        get
        {
            if (instance == null)
            {
                instance = new NSDManager();
            }
            return instance;
        }
    }

    public void Init()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaObject activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity"))
        {
            using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            {
                using (AndroidJavaObject nsdManager = context.Call<AndroidJavaObject>("getSystemService", "servicediscovery"))
                {
                    this.nsdManager = nsdManager;
                }
            }
        }
#endif
    }
}