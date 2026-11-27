// Settings for optional sign-in and account saves. See "Accounts and cloud saves" in README.md.
//
// While firebaseConfig is null, sign-in is switched off and the arcade works as before: progress is
// kept in the player's browser only.
//
// These values are meant to be public. They identify the Firebase project but don't grant access to
// anything; who can read and write saved progress is controlled by firestore.rules.
export const firebaseConfig = {
  apiKey: "AIzaSyBBIyzp-Zu4Ezaz1oDq_65bER17qSMPGVs",
  authDomain: "orion-arcade-8fa58.firebaseapp.com",
  projectId: "orion-arcade-8fa58",
  storageBucket: "orion-arcade-8fa58.firebasestorage.app",
  messagingSenderId: "850869282863",
  appId: "1:850869282863:web:d82289bb121a5fec0c7eda",
};

// Sign-in options shown to players, in this order. Each one must also be enabled in the
// Firebase console under Authentication > Sign-in method.
// Available: "google", "facebook", "microsoft", "github", "apple", "email".
export const signInMethods = ["google", "facebook", "microsoft", "github", "email"];
