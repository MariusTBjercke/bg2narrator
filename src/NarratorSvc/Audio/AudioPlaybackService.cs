using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NAudio.Wave;

namespace NarratorSvc.Audio
{
    internal sealed class AudioPlaybackService : IDisposable
    {
        private readonly Settings _settings;
        private readonly Queue<PendingClip> _pendingFiles = new Queue<PendingClip>();
        private readonly object _queueGate = new object();
        private readonly Timer _pumpTimer;
        private WaveOutEvent _waveOut;
        private WaveFileReader _reader;
        private MemoryStream _playerStream;
        private volatile bool _playbackActive;
        private volatile bool _needsCleanup;
        private long _playbackEndsAtMs;
        private int _generation;

        public AudioPlaybackService(Settings settings)
        {
            _settings = settings;
            _pumpTimer = new Timer(_ => PumpQueue(), null, 50, 50);
        }

        public int CurrentGeneration
        {
            get { return _generation; }
        }

        public bool IsActivelyPlaying()
        {
            if (_playbackActive)
            {
                return true;
            }

            lock (_queueGate)
            {
                return _pendingFiles.Count > 0;
            }
        }

        public void UpdateVolume(float volume)
        {
            if (_waveOut != null)
            {
                _waveOut.Volume = ClampVolume(volume);
            }
        }

        public void EnqueuePlayFile(string absoluteFilePath, int generation)
        {
            if (string.IsNullOrWhiteSpace(absoluteFilePath) || !File.Exists(absoluteFilePath))
            {
                Console.WriteLine("[NarratorSvc] Missing audio file: " + absoluteFilePath);
                return;
            }

            if (generation != _generation)
            {
                return;
            }

            lock (_queueGate)
            {
                _pendingFiles.Enqueue(new PendingClip(Path.GetFullPath(absoluteFilePath), generation));
            }
        }

        public void Stop(string reason)
        {
            SetGeneration(_generation + 1);
        }

        public void SetGeneration(int generation)
        {
            _generation = generation;
            lock (_queueGate)
            {
                _pendingFiles.Clear();
            }

            _playbackActive = false;
            _needsCleanup = false;
            DisposePlayer();
        }

        private void PumpQueue()
        {
            if (_needsCleanup && !_playbackActive)
            {
                _needsCleanup = false;
                DisposePlayer();
            }

            if (_playbackActive && Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency >= _playbackEndsAtMs)
            {
                _playbackActive = false;
                _needsCleanup = true;
            }

            if (_playbackActive)
            {
                return;
            }

            string nextPath = null;
            int nextGeneration = _generation;
            lock (_queueGate)
            {
                while (_pendingFiles.Count > 0)
                {
                    PendingClip clip = _pendingFiles.Dequeue();
                    if (clip.Generation != _generation)
                    {
                        continue;
                    }

                    nextPath = clip.Path;
                    nextGeneration = clip.Generation;
                    break;
                }
            }

            if (nextPath != null)
            {
                StartPlayback(nextPath, nextGeneration);
            }
        }

        private void StartPlayback(string absoluteFilePath, int generation)
        {
            try
            {
                byte[] wav = BuildPlayableWav(absoluteFilePath);
                if (wav == null)
                {
                    return;
                }

                float seconds;
                if (!WavUtil.TryGetWavDurationSeconds(wav, out seconds) || seconds <= 0f)
                {
                    Console.WriteLine("[NarratorSvc] Could not read WAV duration: " + absoluteFilePath);
                    return;
                }

                DisposePlayer();
                _playerStream = new MemoryStream(wav, false);
                _reader = new WaveFileReader(_playerStream);
                _waveOut = new WaveOutEvent();
                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _waveOut.Init(_reader);
                _waveOut.Volume = ClampVolume(_settings != null ? _settings.Volume : 1f);
                _waveOut.Play();
                _playbackActive = true;
                _needsCleanup = false;
                _playbackEndsAtMs = Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency + (long)(seconds * 1000) + 2000;
                Console.WriteLine("[NarratorSvc] Playback started (" + seconds.ToString("0.0") + "s) gen=" + generation);
            }
            catch (Exception ex)
            {
                _playbackActive = false;
                _needsCleanup = false;
                DisposePlayer();
                Console.WriteLine("[NarratorSvc] Playback failed: " + ex.Message);
            }
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            _playbackActive = false;
            _needsCleanup = true;

            if (e != null && e.Exception != null)
            {
                Console.WriteLine("[NarratorSvc] Playback error: " + e.Exception.Message);
            }
        }

        private static byte[] BuildPlayableWav(string absoluteFilePath)
        {
            byte[] data = File.ReadAllBytes(absoluteFilePath);

            if (absoluteFilePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || Mp3Decoder.LooksLikeMp3(data))
            {
                return Mp3Decoder.ToWav(data, 1f);
            }

            if (absoluteFilePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                return data;
            }

            Console.WriteLine("[NarratorSvc] Unsupported audio type: " + absoluteFilePath);
            return null;
        }

        private static float ClampVolume(float volume)
        {
            if (volume < 0f)
            {
                return 0f;
            }

            if (volume > 1f)
            {
                return 1f;
            }

            return volume;
        }

        private void DisposePlayer()
        {
            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= OnPlaybackStopped;
                try
                {
                    _waveOut.Stop();
                }
                catch
                {
                }

                _waveOut.Dispose();
                _waveOut = null;
            }

            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }

            if (_playerStream != null)
            {
                _playerStream.Dispose();
                _playerStream = null;
            }
        }

        public void Dispose()
        {
            Stop("dispose");
            _pumpTimer.Dispose();
        }

        private struct PendingClip
        {
            public readonly string Path;
            public readonly int Generation;

            public PendingClip(string path, int generation)
            {
                Path = path;
                Generation = generation;
            }
        }
    }
}
