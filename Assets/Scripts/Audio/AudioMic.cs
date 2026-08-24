using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class AudioMic
{
    // Configuration variables
    public string DeviceName { get; private set; }
    public int SampleRate { get; private set; }
    public int MaxRecordingLengthSeconds { get; private set; }

    /// <summary>
    /// Callback invoked after audio is recorded and trimmed.
    /// </summary>
    public Action<AudioClip> ProcessAudioClip;

    private AudioClip recordedClip;
    private bool isRecording = false;

    /// <summary>
    /// Constructor to set up AudioMic configuration parameters.
    /// </summary>
    /// <param name="deviceName">Microphone device name (pass null/empty for default system mic).</param>
    /// <param name="sampleRate">Sampling frequency in Hz (default: 44100).</param>
    /// <param name="maxRecordingLengthSeconds">Maximum buffer duration in seconds (default: 300).</param>
    /// <param name="onAudioRecorded">Optional callback for processed AudioClip.</param>
    public AudioMic(
        string deviceName = null, 
        int sampleRate = 44100, 
        int maxRecordingLengthSeconds = 300, 
        Action<AudioClip> onAudioRecorded = null)
    {
        DeviceName = deviceName;
        SampleRate = sampleRate;
        MaxRecordingLengthSeconds = maxRecordingLengthSeconds;
        ProcessAudioClip = onAudioRecorded ?? DefaultProcessAudioHandler;
    }

    /// <summary>
    /// Call inside a MonoBehaviour's Update() method to check inputs.
    /// </summary>
    public void Update()
    {
        if (Keyboard.current == null) return;

        // Spacebar Pressed -> Start Recording
        if (Keyboard.current.spaceKey.wasPressedThisFrame && !isRecording)
        {
            StartRecording();
        }

        // Spacebar Released -> Stop Recording & Process
        if (Keyboard.current.spaceKey.wasReleasedThisFrame && isRecording)
        {
            StopAndProcessRecording();
        }
    }

    public void StartRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("AudioMic: No microphone detected on this system.");
            return;
        }

        string selectedDevice = string.IsNullOrEmpty(DeviceName) 
            ? Microphone.devices[0] 
            : DeviceName;

        recordedClip = Microphone.Start(selectedDevice, false, MaxRecordingLengthSeconds, SampleRate);
        isRecording = true;
        Debug.Log($"AudioMic: Recording started on device '{selectedDevice}' at {SampleRate}Hz...");
    }

    public void StopAndProcessRecording()
    {
        string selectedDevice = string.IsNullOrEmpty(DeviceName) && Microphone.devices.Length > 0 
            ? Microphone.devices[0] 
            : DeviceName;

        // Get sample position BEFORE stopping the mic
        int finalPosition = Microphone.GetPosition(selectedDevice);
        Microphone.End(selectedDevice);
        isRecording = false;

        if (finalPosition <= 0)
        {
            Debug.LogWarning("AudioMic: Recording was too short or contained no data.");
            return;
        }

        // Trim AudioClip to exact recorded sample length
        AudioClip trimmedClip = TrimClip(recordedClip, finalPosition);
        
        Debug.Log($"AudioMic: Recording stopped. Trimmed clip duration: {trimmedClip.length:F2} seconds.");

        // Invoke callback
        ProcessAudioClip?.Invoke(trimmedClip);
    }

    private void DefaultProcessAudioHandler(AudioClip clip)
    {
        Debug.Log($"Default Handler executed: Received AudioClip '{clip.name}' (Length: {clip.length:F2}s).");
    }

    private AudioClip TrimClip(AudioClip clip, int samples)
    {
        float[] soundData = new float[samples * clip.channels];
        clip.GetData(soundData, 0);

        AudioClip trimmed = AudioClip.Create(
            clip.name + "_Trimmed", 
            samples, 
            clip.channels, 
            clip.frequency, 
            false
        );
        trimmed.SetData(soundData, 0);
        return trimmed;
    }
}