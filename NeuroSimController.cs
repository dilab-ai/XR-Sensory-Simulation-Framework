using UnityEngine;
using UnityEngine.Audio;
using Firebase.Database;

public class NeuroSimController : MonoBehaviour
{
    public AudioMixer mainMixer;
    private DatabaseReference controlRef;

    // Local variables to hold data from Firebase
    private float targetDist = 0f;
    private float targetLow = 0f;
    private float targetEcho = 0f;

    void Start()
    {
        controlRef = FirebaseDatabase.DefaultInstance.GetReference("sessions/0000/controls");

        // Listen for Individual Parameter Changes
        controlRef.Child("dist").ValueChanged += (s, e) => {
            if (e.Snapshot.Exists) targetDist = float.Parse(e.Snapshot.Value.ToString());
        };

        controlRef.Child("low").ValueChanged += (s, e) => {
            if (e.Snapshot.Exists) targetLow = float.Parse(e.Snapshot.Value.ToString());
        };

        controlRef.Child("echo").ValueChanged += (s, e) => {
            if (e.Snapshot.Exists) targetEcho = float.Parse(e.Snapshot.Value.ToString());
        };
    }

    void Update()
    {
        // Update Distortion (0 to 0.8 intensity)
        mainMixer.SetFloat("MyDist", Mathf.Lerp(0f, 0.8f, targetDist));

        // Update Lowpass (5000Hz to 600Hz cutoff)
        mainMixer.SetFloat("MyLow", Mathf.Lerp(5000f, 600f, targetLow));

        // Update Echo Wet Mix (-80dB to 0dB)
        mainMixer.SetFloat("MyEcho", Mathf.Lerp(-80f, 0f, targetEcho));
    }
}