using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FamiStudio
{
    public class OscilloscopeGenerator : IOscilloscope
    {
        private const float SampleScale = 1.9f; 

        private class SamplesAndTrigger
        {
            public short[][] samples;
            public int[] triggers;
        };

        private Task task;
        private ManualResetEvent stopEvent    = new ManualResetEvent(false);
        private AutoResetEvent   samplesEvent = new AutoResetEvent(false);
        private ConcurrentQueue<SamplesAndTrigger> sampleQueue;
        private OscilloscopeTrigger[] triggerFunctions;
        private int[] lastTriggers;
        private int[] lastSampleCounts;
        private int[] holdFrameCounts;
        private bool stereo;
        private volatile float[][] geometries;
        private volatile bool[] hasNonZeroDatas;

        private Dictionary<int, short[]> mixDownBuffers = new Dictionary<int, short[]>();

        private int[] bufferPos;
        private short[][] sampleBuffers;
        private int numChannels;

        public OscilloscopeGenerator(int channels, bool stereo)
        {
            this.numChannels = channels;
            this.stereo = stereo;
            
            triggerFunctions = new OscilloscopeTrigger[channels];
            lastTriggers = new int[channels];
            lastSampleCounts = new int[channels];
            holdFrameCounts = new int[channels];
            bufferPos = new int[channels];
            sampleBuffers = new short[channels][];
            geometries = new float[channels][];
            hasNonZeroDatas = new bool[channels];

            for (int i = 0; i < channels; i++)
            {
                lastTriggers[i] = -1;
                sampleBuffers[i] = new short[16384];
                triggerFunctions[i] = new PeakSpeedTrigger(sampleBuffers[i], true);
            }
        }

        public void Start()
        {
            task = Task.Factory.StartNew(OscilloscopeThread, TaskCreationOptions.LongRunning);
            sampleQueue = new ConcurrentQueue<SamplesAndTrigger>();
        }

        public void Stop()
        {
            if (task != null)
            {
                stopEvent.Set();
                task.Wait();
                task = null;
                sampleQueue = null;
            }
        }

        public void AddSamples(short[] samples, int trigger = NesApu.TRIGGER_NONE)
        {
            AddSamples(new[] { samples }, new[] { trigger });
        }

        public void AddSamples(short[][] samples, int[] triggers)
        {
            if (task != null)
            {
                sampleQueue.Enqueue(new SamplesAndTrigger() { samples = samples, triggers = triggers });
                samplesEvent.Set();
            }
        }

        public float[][] GetGeometries(out bool[] outHasNonZeroSamples)
        {
            outHasNonZeroSamples = hasNonZeroDatas;
            return geometries;
        }

        public bool HasNonZeroSample
        {
            get
            {
                if (hasNonZeroDatas != null)
                {
                    foreach (var hasData in hasNonZeroDatas)
                        if (hasData) return true;
                }
                return false;
            }
        }

        public float[] GetGeometry(out bool outHasNonZeroSample)
        {
            outHasNonZeroSample = hasNonZeroDatas != null && hasNonZeroDatas.Length > 0 ? hasNonZeroDatas[0] : false;
            return geometries != null && geometries.Length > 0 ? geometries[0] : null;
        }

        private void OscilloscopeThread()
        {
            var waitEvents = new WaitHandle[] { stopEvent, samplesEvent };

            while (true)
            {
                int idx = WaitHandle.WaitAny(waitEvents);

                if (idx == 0)
                    break;

                do
                {
                    if (Platform.IsDesktop && (FamiStudioWindow.Instance.IsOutOfProcessDialogInProgress || (FamiStudioWindow.Instance.IsAsyncDialogInProgress && !(FamiStudioWindow.Instance.TopDialog is OscilloscopeFullscreenDialog)))) 
                        break;
                    
                    if (sampleQueue.TryDequeue(out var pair))
                    {
                        for (int ch = 0; ch < numChannels; ch++)
                        {
                            if (ch >= pair.samples.Length) continue;

                            var samples = pair.samples[ch];
                            
                            if (samples == null)
                                continue;

                            if (stereo)
                            {
                                if (!mixDownBuffers.TryGetValue(samples.Length, out var mixDownBuffer))
                                {
                                    mixDownBuffer = new short[samples.Length / 2];
                                    mixDownBuffers.Add(samples.Length, mixDownBuffer);
                                }

                                WaveUtils.MixDown(samples, mixDownBuffer);
                                samples = mixDownBuffer;
                            }

                            var startBufferPos = bufferPos[ch];

                            if (bufferPos[ch] + samples.Length < sampleBuffers[ch].Length)
                            {
                                Array.Copy(samples, 0, sampleBuffers[ch], bufferPos[ch], samples.Length);
                                bufferPos[ch] += samples.Length;
                            }
                            else
                            {
                                int batchSize1 = sampleBuffers[ch].Length - bufferPos[ch];
                                int batchSize2 = samples.Length - batchSize1;

                                Array.Copy(samples, 0, sampleBuffers[ch], bufferPos[ch], batchSize1);
                                Array.Copy(samples, batchSize1, sampleBuffers[ch], 0, batchSize2);

                                bufferPos[ch] = batchSize2;
                            }

                            var newTrigger = pair.triggers != null && ch < pair.triggers.Length ? pair.triggers[ch] : NesApu.TRIGGER_NONE;

                            if (newTrigger == NesApu.TRIGGER_NONE)
                            {
                                newTrigger = triggerFunctions[ch].Detect(startBufferPos, samples.Length);

                                if (newTrigger < 0)
                                    newTrigger = startBufferPos;

                                holdFrameCounts[ch] = 0;
                            }
                            else if (newTrigger >= 0)
                            {
                                newTrigger = (startBufferPos + newTrigger) % sampleBuffers[ch].Length;
                                holdFrameCounts[ch] = 0;
                            }
                            else
                            {
                                holdFrameCounts[ch]++;
                            }

                            if (holdFrameCounts[ch] >= 10)
                                Debug.WriteLine($"WARNING, oscilloscope triggers on hold for {holdFrameCounts[ch]} frames. Check emulation code.");

                            if (lastTriggers[ch] >= 0)
                            {
                                var newHasNonZeroData = false;
                                var vertices = new float[lastSampleCounts[ch] * 2];

                                var j = lastTriggers[ch] - lastSampleCounts[ch] / 2; 
                                if (j < 0) j += sampleBuffers[ch].Length;

                                for (int i = 0; i < lastSampleCounts[ch]; i++, j = (j + 1) % sampleBuffers[ch].Length)
                                {
                                    var samp = (int)sampleBuffers[ch][j];

                                    vertices[i * 2 + 0] = i / (float)(lastSampleCounts[ch] - 1);
                                    vertices[i * 2 + 1] = Utils.Clamp(samp / 32768.0f * SampleScale, -1.0f, 1.0f);

                                    newHasNonZeroData |= Math.Abs(samp) > 1024;
                                }

                                geometries[ch] = vertices;
                                hasNonZeroDatas[ch] = newHasNonZeroData;
                            }

                            lastTriggers[ch] = newTrigger;
                            lastSampleCounts[ch] = samples.Length;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                while (!sampleQueue.IsEmpty);
            }
        }
    }
}
