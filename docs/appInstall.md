#Install 

This is a .NET 9 MAUI Android app. Here's how to build an APK you can install on your Motorola Razr 2024:

  Build a signed APK

  1. Generate a keystore (one-time setup, skip if you already have one):

  keytool -genkeypair -v -keystore medapp.keystore -alias medapp -keyalg RSA -keysize 2048 -validity 10000

  2. Build the Release APK:

  dotnet publish medAppSrc/MedApp/MedApp.csproj -f net9.0-android -c Release \
    -p:AndroidKeyStore=true \
    -p:AndroidSigningKeyStore=medapp.keystore \
    -p:AndroidSigningKeyAlias=medapp \
    -p:AndroidSigningKeyPass=YOUR_PASSWORD \
    -p:AndroidSigningStorePass=YOUR_PASSWORD

  The signed APK will be at:
  medAppSrc/MedApp/bin/Release/net9.0-android/publish/com.nextlevel.medapp-Signed.apk

  Install on your Razr

  Option A — USB cable:
  adb install com.nextlevel.medapp-Signed.apk

  Option B — Transfer the APK file via email, Google Drive, or USB, then open it on the phone. You'll need to enable
  "Install from unknown sources" in Settings > Security first.

  Notes for the Razr 2024

  - It runs Android 14, and your app targets min SDK 26 — so you're fully compatible.
  - The Razr 2024 is arm64-v8a architecture, which is covered by the default MAUI Android build.
  - If you just want a quick debug build (unsigned, no keystore needed): dotnet build medAppSrc/MedApp/MedApp.csproj -f
  net9.0-android -c Release — but you'll still need unknown sources enabled.

  Want me to run the build for you?

## Run the build for me.
Update to the Medications screen
- The name of the medication entered in the details screen does not show up the medication screen.
- There should be a pop screen 


- The Name of the medicine does not show up on the todays screen.  At the time for the medicine the status changes to pending and then transitions to missing no alarms is generates.

## Prompt
Look at the current code, generate a docs\softwareDesign.md document that describe the current design. I will make updates to this document on changes to make to the project.  Let me know the best way to make changes so that you can see the updates to make. New document, updates to the document with certain tag.  
