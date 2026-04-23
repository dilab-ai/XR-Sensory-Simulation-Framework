using UnityEngine;
using Unity.WebRTC;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections;
using System.Collections.Generic;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

[RequireComponent(typeof(AudioSource))]
public class WebRTCReceiver : MonoBehaviour
{
    private RTCPeerConnection pc;
    private DatabaseReference dbRef;
    private AudioSource audioSource;
    
    [Header("Audio Settings")]
    [Tooltip("Drag your 'EnvironmentalAudio' Mixer Group here")]
    public UnityEngine.Audio.AudioMixerGroup effectGroup;

    private void Awake()
    {
        // Keeps audio active even if headset enters standby
        AudioConfiguration config = AudioSettings.GetConfiguration();
        AudioSettings.Reset(config);
    }

    void Start() {
        // 1. Request Permissions for Quest 3
        #if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone)) {
            Permission.RequestUserPermission(Permission.Microphone);
        }
        #endif

        audioSource = GetComponent<AudioSource>();
        audioSource.spatialize = false; 
        audioSource.playOnAwake = false;
        
        if (effectGroup != null) {
            audioSource.outputAudioMixerGroup = effectGroup;
        }

        dbRef = FirebaseDatabase.DefaultInstance.RootReference;
        
        // In newer WebRTC versions, we just start the Update loop. 
        // Initialize() is no longer required.
        StartCoroutine(WebRTC.Update());
        SetupConnection();
    }

    void SetupConnection() {
        var conf = new RTCConfiguration {
            iceServers = new[] { 
                new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } 
            }
        };
        pc = new RTCPeerConnection(ref conf);

        pc.OnTrack = e => {
            if (e.Track is AudioStreamTrack track) {
                Debug.Log("WebRTC: Incoming Audio Track Found!");
                audioSource.SetTrack(track);
                audioSource.Play();
            }
        };

        pc.OnIceCandidate = candidate => {
            if (string.IsNullOrEmpty(candidate.Candidate) || candidate.Candidate.Contains("127.0.0.1")) return;

            var data = new RTCIceCandidateInit {
                candidate = candidate.Candidate,
                sdpMid = candidate.SdpMid,
                sdpMLineIndex = candidate.SdpMLineIndex
            };
            dbRef.Child("sessions/0000/unityCandidates").Push().SetRawJsonValueAsync(JsonUtility.ToJson(data));
        };

        dbRef.Child("sessions/0000/webCandidates").ChildAdded += (s, e) => {
            if (e.Snapshot.Exists) {
                var init = JsonUtility.FromJson<RTCIceCandidateInit>(e.Snapshot.GetRawJsonValue());
                pc.AddIceCandidate(new RTCIceCandidate(init));
            }
        };

        dbRef.Child("sessions/0000/offer").ValueChanged += (s, e) => {
            if (e.Snapshot.Exists) {
                string sdp = e.Snapshot.Child("sdp").Value.ToString();
                StartCoroutine(HandleOffer(sdp));
            }
        };
    }

    IEnumerator HandleOffer(string sdp) {
        var desc = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = sdp };
        var opRemote = pc.SetRemoteDescription(ref desc);
        yield return opRemote;
        
        var opAnswer = pc.CreateAnswer();
        yield return opAnswer;
        
        var answer = opAnswer.Desc;
        var opLocal = pc.SetLocalDescription(ref answer);
        yield return opLocal;

        string cleanSdp = answer.sdp.Replace("\n", "\\n").Replace("\r", "\\r");
        string jsonAnswer = "{\"type\":\"answer\", \"sdp\":\"" + cleanSdp + "\"}";
        
        dbRef.Child("sessions/0000/answer").SetRawJsonValueAsync(jsonAnswer);
        Debug.Log("WebRTC: Answer uploaded.");
    }

    private void OnDestroy() {
        pc?.Close();
        pc?.Dispose();
        // WebRTC.Dispose() is also removed in newer versions
    }
}