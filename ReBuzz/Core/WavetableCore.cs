﻿using BuzzGUI.Common;
using BuzzGUI.Interfaces;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Speech.Synthesis;

namespace ReBuzz.Core
{
    internal class WavetableCore : IWavetable
    {
        public static readonly int NUM_WAVES = 200;
        public ISong Song { get => buzz.Song; }

        readonly List<WaveCore> waves = new List<WaveCore>();
        private readonly ReBuzzCore buzz;

        public List<WaveCore> WavesList { get => waves; }
        public ReadOnlyCollection<IWave> Waves { get => waves.Cast<IWave>().ToReadOnlyCollection(); }

        private float volume;
        public float Volume { get => volume; set => volume = value; }

        public event Action<int> WaveChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public WavetableCore(ReBuzzCore bc)
        {
            this.buzz = bc;

            for (int i = 0; i < NUM_WAVES; i++)
            {
                waves.Add(null);
                WaveChanged?.Invoke(i);
            }
        }

        private void W_WaveChanged(int waveIndex)
        {
            WaveChanged?.Invoke(waveIndex);
        }

        public void AllocateWave(int index, string path, string name, int size, WaveFormat format, bool stereo, int root, bool add, bool wavechangedevent)
        {
            if (index < 0 || index >= waves.Count)
                return;

            var w = WavesList[index];
            if (WavesList[index] == null)
            {
                w = new WaveCore(buzz);
                w.WaveChanged += W_WaveChanged;
                WavesList[index] = w;
            }

            w.Index = index;
            w.Name = name;
            w.FileName = path;
            w.Volume = volume;

            int size16Bit = size;

            switch (format)
            {
                case WaveFormat.Int24:
                    size16Bit = (int)Math.Ceiling(size * 3 / 2.0) + 4;
                    break;
                case WaveFormat.Int32:
                case WaveFormat.Float32:
                    size16Bit = size16Bit * 2 + 4;
                    break;
                case WaveFormat.Int16:
                    break;
            }

            if (add) 
            {
                bool ok = w.Flags.HasFlag(WaveFlags.Stereo) && stereo || !stereo && !w.Flags.HasFlag(WaveFlags.Stereo);
                ok &= !w.Flags.HasFlag(WaveFlags.Not16Bit) && format == WaveFormat.Int16;
                ok |= w.Layers.Count == 0;
                if (ok)
                {
                    WaveLayerCore layer = new WaveLayerCore(w, path, format, root, stereo, size16Bit);
                    w.LayersList.Add(layer);
                    layer.LayerIndex = w.LayersList.Count - 1;
                }
            }
            // Wave needs to be same format & type
            else
            {   
                w.Flags = 0;
                if (stereo)
                    w.Flags |= WaveFlags.Stereo;
                if (format != WaveFormat.Int16)
                    w.Flags |= WaveFlags.Not16Bit;

                var layer = w.LayersList.FirstOrDefault();
                if (layer == null)
                {
                    layer = new WaveLayerCore(w, path, format, root, stereo, size16Bit);
                    w.LayersList.Add(layer);
                }
                else
                {
                    layer.Init(path, format, root, stereo, size16Bit);
                }
            }

            if (wavechangedevent)
            {
                WaveChanged?.Invoke(index);
            }

            PropertyChanged.Raise(this, "Waves");
        }

        public void LoadWave(int index, string path, string name, bool add)
        {
            lock (ReBuzzCore.AudioLock)
            {
                if (index < 0 || index >= waves.Count)
                    return;

                if (path == null) // Clear
                {
                    var w = WavesList[index];
                    if (w != null)
                    {
                        w.WaveChanged -= W_WaveChanged;
                        w.Clear();
                    }
                    WavesList[index] = null;

                    WaveChanged?.Invoke(index);
                }
                else
                {
                    var audioFileReader = new NAudio.Wave.AudioFileReader(path);
                    var fileWaveFormat = audioFileReader.WaveFormat;
                    var wholeFile = new List<float>((int)(audioFileReader.Length / 4));
                    var readBuffer = new float[audioFileReader.WaveFormat.SampleRate * audioFileReader.WaveFormat.Channels];
                    int samplesRead;
                    while ((samplesRead = audioFileReader.Read(readBuffer, 0, readBuffer.Length)) > 0)
                    {
                        wholeFile.AddRange(readBuffer.Take(samplesRead));
                    }
                    var AudioData = wholeFile.ToArray();

                    WaveFormat format = WaveFormat.Float32;
                    switch (fileWaveFormat.BitsPerSample)
                    {
                        case 24:
                            format = WaveFormat.Int24;
                            break;
                        case 32:
                            format = WaveFormat.Int32;
                            break;
                        case 16:
                            format = WaveFormat.Int16;
                            break;
                    }

                    AllocateWave(index, path, name, AudioData.Length, format, fileWaveFormat.Channels == 2, BuzzNote.Parse("C-4"), add, false);
                    var w = waves[index];
                    var layer = w.Layers.Last();
                    layer.SampleRate = fileWaveFormat.SampleRate;
                    layer.LoopStart = 0;
                    layer.LoopEnd = AudioData.Length;
                    if (fileWaveFormat.Channels == 2)
                    {
                        layer.SetDataAsFloat(AudioData, 0, 2, 0, 0, AudioData.Length / 2);
                        layer.SetDataAsFloat(AudioData, 1, 2, 1, 0, AudioData.Length / 2);
                    }
                    else
                    {
                        layer.SetDataAsFloat(AudioData, 0, 0, 0, 0, AudioData.Length);
                    }
                }
            }
        }

        public void PlayWave(string path)
        {
            // Use master tap?
        }

        internal WaveCore CreateWave(ushort index)
        {
            var w = new WaveCore(buzz);
            w.WaveChanged += W_WaveChanged;
            WavesList[index] = w;

            return w;
        }
    }
}
