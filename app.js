import { initializeApp } from "https://www.gstatic.com/firebasejs/10.8.1/firebase-app.js";
import { getDatabase, ref, set, push, onValue, onChildAdded } from "https://www.gstatic.com/firebasejs/10.8.1/firebase-database.js";

const firebaseConfig = {
  apiKey: "AIzaSyAqpXGpex2fx-wBh6IuwQG5jgxrJul923Y",
  authDomain: "neuro-audio-signaling.firebaseapp.com",
  databaseURL: "https://neuro-audio-signaling-default-rtdb.firebaseio.com",
  projectId: "neuro-audio-signaling",
  storageBucket: "neuro-audio-signaling.firebasestorage.app",
  messagingSenderId: "1052165699020",
  appId: "1:1052165699020:web:7a3178cc87989f5c49d969",
  measurementId: "G-HQ1PNJTQQD"
};

const app = initializeApp(firebaseConfig);
const db = getDatabase(app);
const pc = new RTCPeerConnection({ iceServers: [{ urls: 'stun:stun.l.google.com:19302' }] });
const unityCandidatesQueue = [];

// 1. Sync Sliders to Firebase
const sync = (id, path) => {
    document.getElementById(id).oninput = function() {
        set(ref(db, `sessions/0000/controls/${path}`), parseFloat(this.value));
    };
};
sync('distSlider', 'dist');
sync('lowSlider', 'low');
sync('echoSlider', 'echo');

// 2. WebRTC Mic Capture
navigator.mediaDevices.getUserMedia({ audio: true }).then(stream => {
    stream.getTracks().forEach(track => pc.addTrack(track, stream));
    pc.createOffer().then(offer => {
        pc.setLocalDescription(offer);
        set(ref(db, 'sessions/0000/offer'), { type: offer.type, sdp: offer.sdp });
    });
});

pc.onicecandidate = e => { if (e.candidate) push(ref(db, 'sessions/0000/webCandidates'), e.candidate.toJSON()); };

onValue(ref(db, 'sessions/0000/answer'), (snap) => {
    const data = snap.val();
    if (data && !pc.currentRemoteDescription) {
        pc.setRemoteDescription(new RTCSessionDescription(data)).then(() => {
            while (unityCandidatesQueue.length > 0) pc.addIceCandidate(unityCandidatesQueue.shift());
            document.getElementById('connStatus').innerText = "Broadcasting Active";
        });
    }
});

onChildAdded(ref(db, 'sessions/0000/unityCandidates'), (snap) => {
    const cand = new RTCIceCandidate(snap.val());
    if (pc.remoteDescription) pc.addIceCandidate(cand);
    else unityCandidatesQueue.push(cand);
});