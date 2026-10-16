using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace Wallpaper_UI;

public static class AudioEngine
{
    private static WasapiLoopbackCapture? capture;
    public static float[] AudioBins { get; set; } = new float[200];
    public static bool IsActive { get; set; } = false;

    private const int FFT_SIZE = 1024;
    private static Complex[] fftBuffer = new Complex[FFT_SIZE];
    private static int fftPos = 0;

    public static void Start()
    {
        if (IsActive) return; // Already running
        try
        {
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            capture = new WasapiLoopbackCapture(device);
            capture.DataAvailable += ProcessAudio;
            capture.StartRecording();
            IsActive = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Audio Engine Error: " + ex.Message);
            IsActive = false;
        }
    }

    public static void Stop()
    {
        IsActive = false;
        if (capture != null)
        {
            capture.StopRecording();
            capture.DataAvailable -= ProcessAudio;
            capture.Dispose();
            capture = null;
        }
        // Clear bins so lights don't get stuck on
        Array.Clear(AudioBins, 0, AudioBins.Length);
    }

    private static void ProcessAudio(object? sender, WaveInEventArgs e)
    {
        if (!IsActive) return;

        // Copy data quickly to a local array to free up the NAudio buffer
        int bytesToProcess = e.BytesRecorded;
        byte[] localBuffer = new byte[bytesToProcess];
        Array.Copy(e.Buffer, localBuffer, bytesToProcess);

        // Offload the heavy FFT math to a background thread to keep FPS at 60
        Task.Run(() =>
        {
            var waveBuffer = new WaveBuffer(localBuffer);
            int samplesCount = bytesToProcess / 4;

            for (int i = 0; i < samplesCount - 1; i++)
            {
                float sample = waveBuffer.FloatBuffer[i];
                double windowed = sample * FastFourierTransform.HammingWindow(fftPos, FFT_SIZE);

                fftBuffer[fftPos].X = (float)windowed;
                fftBuffer[fftPos].Y = 0;
                fftPos++;

                if (fftPos >= FFT_SIZE)
                {
                    FastFourierTransform.FFT(true, 10, fftBuffer);

                    int halfSize = FFT_SIZE / 2;
                    double binsPerIndex = halfSize / 200.0;

                    for (int j = 0; j < 200; j++)
                    {
                        int startIdx = (int)(j * binsPerIndex);
                        int endIdx = Math.Min((int)((j + 1) * binsPerIndex), halfSize - 1);
                        float maxMag = 0;

                        for (int k = startIdx; k <= endIdx; k++)
                        {
                            float mag = (float)Math.Sqrt(fftBuffer[k].X * fftBuffer[k].X + fftBuffer[k].Y * fftBuffer[k].Y);
                            if (mag > maxMag) maxMag = mag;
                        }

                        // Update the public bins
                        AudioBins[j] = Math.Max(maxMag, AudioBins[j] * 0.8f);
                    }
                    fftPos = 0;
                }
            }
        });
    }
}
