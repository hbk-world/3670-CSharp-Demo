using System;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SimpleDemo
{
    public partial class Form1 : Form
    {
        private AsioOut _asioOut;
        private SignalGenerator _signalGenerator;
        private SignalGenerator _signalGenerator2;
        private WaveFileReader reader;
        private WaveFileWriter writer;
        private string OutputWaveFileName = "";
        public string _musicPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        private const int _MaxChannelNumber = 8;
        private int _channelsIn = 1;
        private int _channelsOut = 1;
        private int _sampleBufferSize = 2048; // set by Asio driver
        private float _FullScaleInput = 1;
        private float _FullScaleOutput = 1;
        private int _sampleRate;
        public volatile float[] _interleavedSamples;
        public volatile float[][] _timeSignal = new float[_MaxChannelNumber][];
        private bool _recording = false;

        public Form1()
        {
            InitializeComponent();
            SetSettings();
            try
            {
                foreach (var device in AsioOut.GetDriverNames())
                {
                    comboBoxAsioDevice.Items.Add(device);
                }
                if (comboBoxAsioDevice.Items.Count > 0)
                {
                    comboBoxAsioDevice.SelectedIndex = 0;
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void SetSettings()
        {
            numericUpDownChannelsIn.Value = Properties.Settings.Default.InputChannels;
            comboBoxSampleRate.SelectedIndex = Properties.Settings.Default.SamplingRateIndex;
            comboBoxFFTWindow.SelectedIndex = Properties.Settings.Default.WindowingIndex;
            comboBoxFuncion.SelectedIndex = Properties.Settings.Default.FunctionIndex;
            comboBoxFftLines.SelectedIndex = Properties.Settings.Default.FFTLinesIndex;
            numericUpDownAverages.Value = Properties.Settings.Default.Averages;
            numericUpDownNominalInputGain.Value = Properties.Settings.Default.FullScaleInput;
            comboBoxBufferSize.SelectedIndex = Properties.Settings.Default.BufferSizeSelectedIndex;
            numericUpDownChannelsOut.Value = Properties.Settings.Default.OutputChannels;
            numericUpDownFrequency.Value = Properties.Settings.Default.OutputFrequency;
            numericUpDownLevel.Value = Properties.Settings.Default.OutputAmplitude;
            comboBoxOutputSignalType.SelectedIndex = Properties.Settings.Default.OutputSignalTypeIndex;
            numericUpDownNominalOutputGain.Value = Properties.Settings.Default.FullScaleOutput;
            numericUpDownFrequency2.Value = Properties.Settings.Default.OutputFrequency2;
            numericUpDownLevel2.Value = Properties.Settings.Default.OutputAmplitude2;
            comboBoxOutputSignalType2.SelectedIndex = Properties.Settings.Default.OutputSignalTypeIndex2;
            _FullScaleInput = (float)Properties.Settings.Default.FullScaleInput;
            _FullScaleOutput = (float)Properties.Settings.Default.FullScaleOutput;
            _recording = Properties.Settings.Default.Recording;
        }

        private void SaveSettings() 
        {
            Properties.Settings.Default.InputChannels = (int)numericUpDownChannelsIn.Value;
            Properties.Settings.Default.SamplingRateIndex = comboBoxSampleRate.SelectedIndex;
            Properties.Settings.Default.WindowingIndex = comboBoxFFTWindow.SelectedIndex;
            Properties.Settings.Default.FunctionIndex = comboBoxFuncion.SelectedIndex;
            Properties.Settings.Default.FFTLinesIndex = comboBoxFftLines.SelectedIndex;
            Properties.Settings.Default.Averages = (int)numericUpDownAverages.Value;
            Properties.Settings.Default.OutputChannels = (int)numericUpDownChannelsOut.Value;
            Properties.Settings.Default.OutputFrequency = numericUpDownFrequency.Value;
            Properties.Settings.Default.OutputAmplitude = numericUpDownLevel.Value;
            Properties.Settings.Default.OutputSignalTypeIndex = comboBoxOutputSignalType.SelectedIndex;
            Properties.Settings.Default.BufferSizeSelectedIndex = comboBoxBufferSize.SelectedIndex;
            Properties.Settings.Default.OutputFrequency2 = numericUpDownFrequency2.Value;
            Properties.Settings.Default.OutputAmplitude2 = numericUpDownLevel2.Value;
            Properties.Settings.Default.OutputSignalTypeIndex2 = comboBoxOutputSignalType2.SelectedIndex;
            Properties.Settings.Default.FullScaleInput = numericUpDownNominalInputGain.Value;
            Properties.Settings.Default.FullScaleOutput = numericUpDownNominalOutputGain.Value;
            Properties.Settings.Default.Recording = _recording;
            Properties.Settings.Default.Save();
        }

        private void Cleanup()
        {
            if (_asioOut != null)
            {
                if (_asioOut.PlaybackState != PlaybackState.Stopped)
                {
                    _asioOut.Stop();
                }
                _asioOut.AudioAvailable -= OnAsioOutAudioAvailable;
                _asioOut.Dispose();
                _asioOut = null;
            }
            if (_signalGenerator != null)
            {
                _signalGenerator = null;
            }
            for (int ch = 0; ch < _MaxChannelNumber; ch++)
            {
                _timeSignal[ch] = null;
            }
            if (reader != null)
            {
                reader.Close();
                reader.Dispose();
                reader = null;
            }
            if (writer != null)
            {
                writer.Close();
                writer.Dispose();
                writer = null;
            }
        }

        private void timerShutdown_Tick(object sender, EventArgs e)
        {
            Close();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_asioOut != null)
            {
                if (_asioOut.PlaybackState == PlaybackState.Playing)
                {
                    Stop();
                    e.Cancel = true;
                    Debug.WriteLine("Please Wait for shutdown...");
                    timerShutdown.Interval = 2000;
                    timerShutdown.Enabled = true;
                }
                else
                {
                    Cleanup();
                    SaveSettings();
                }
            }
            else
            {
                SaveSettings();
            }
        }

        private void buttonRecord_Click(object sender, EventArgs e)
        {
            try
            {
                _recording = checkBoxRecord.Checked;

                _channelsIn = (int)numericUpDownChannelsIn.Value;
                _channelsOut = (int)numericUpDownChannelsOut.Value;

                Cleanup();
                _sampleRate = Convert.ToInt32(comboBoxSampleRate.Text);
                _asioOut = new AsioOut(comboBoxAsioDevice.SelectedIndex);
                _asioOut.InputChannelOffset = 0;
                //toolStripStatusLabelVolume.Text = "Volume: " + _asioOut.Volume.ToString();

                if (_recording)
                {
                    WaveFormat waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, _channelsIn);
                    string recordingFileName = _musicPath + "\\Demo Recording " + DateTime.Now.ToString("yyyy_MM_dd HH_mm_ss") + ".wav";
                    writer = new WaveFileWriter(recordingFileName, waveFormat);
                    toolStripStatusLabelOutputFile.Text = recordingFileName;
                }
                else
                {
                    toolStripStatusLabelOutputFile.Text = "";
                }

                _signalGenerator = new SignalGenerator(_sampleRate, 1);
                _signalGenerator.Type = (SignalGeneratorType)comboBoxOutputSignalType.SelectedIndex;
                _signalGenerator.Gain = (float)numericUpDownLevel.Value * (float)Math.Sqrt(2) / _FullScaleOutput;
                _signalGenerator.Frequency = (float)numericUpDownFrequency.Value;
                if (_signalGenerator.Type == SignalGeneratorType.Sweep)
                {
                    _signalGenerator.Frequency = 1;
                    _signalGenerator.FrequencyEnd = (float)numericUpDownFrequency.Value;
                }

                if (_channelsOut > 1) // if more than 1 output, then create one more signal generator and a multiplexer
                {

                    _signalGenerator2 = new SignalGenerator(_sampleRate, 1);
                    _signalGenerator2.Type = (SignalGeneratorType)comboBoxOutputSignalType2.SelectedIndex;
                    _signalGenerator2.Gain = (float)numericUpDownLevel2.Value * (float)Math.Sqrt(2) / _FullScaleOutput;
                    _signalGenerator2.Frequency = (float)numericUpDownFrequency2.Value;
                    if (_signalGenerator2.Type == SignalGeneratorType.Sweep)
                    {
                        _signalGenerator2.Frequency = 1;
                        _signalGenerator2.FrequencyEnd = (float)numericUpDownFrequency2.Value;
                    }

                    MultiplexingWaveProvider waveProvider;
                    if (OutputWaveFileName.Length > 0)
                    {
                        reader = new WaveFileReader(OutputWaveFileName);
                        waveProvider = new MultiplexingWaveProvider(new IWaveProvider[] { reader, _signalGenerator2.ToWaveProvider() }, 2);
                    }
                    else
                    {
                        waveProvider = new MultiplexingWaveProvider(new IWaveProvider[] { _signalGenerator.ToWaveProvider(), _signalGenerator2.ToWaveProvider() }, 2);
                    }

                    waveProvider.ConnectInputToOutput(0, 0);
                    waveProvider.ConnectInputToOutput(1, 1);
                    _asioOut.InitRecordAndPlayback(waveProvider, _channelsIn, _sampleRate);
                }
                else
                {
                    IWaveProvider waveProvider;
                    if (OutputWaveFileName.Length > 0)
                    {
                        reader = new WaveFileReader(OutputWaveFileName);
                        waveProvider = reader;
                    }
                    else
                    {
                        waveProvider = _signalGenerator.ToWaveProvider();
                    }
                    _asioOut.InitRecordAndPlayback(waveProvider, _channelsIn, _sampleRate);
                }

                _asioOut.AudioAvailable += OnAsioOutAudioAvailable;
                //_sampleBufferSize = _asioOut.BufferSize;
                _sampleBufferSize = Convert.ToInt32(comboBoxBufferSize.Text);

                _interleavedSamples = new float[_channelsIn * _sampleBufferSize];
                for (int ch = 0; ch < _channelsIn; ch++)
                {
                    _timeSignal[ch] = new float[_sampleBufferSize];
                }

                _asioOut.Play();
                timerTimeData.Enabled = true;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        void OnAsioOutAudioAvailable(object sender, AsioAudioAvailableEventArgs e)
        {
            int samples = e.GetAsInterleavedSamples(_interleavedSamples);

            // move samples to circular buffer for further processing

            if (_recording)
            {
                writer.WriteSamples(_interleavedSamples, 0, _interleavedSamples.Length);
            }
        }

        private void UpdateGUI()
        {
            for (int i = 0; i < _channelsIn * _sampleBufferSize; i++)
            {

                int ch = i % _channelsIn;
                int z = i  / _channelsIn;
                _timeSignal[ch][z] = _interleavedSamples[i] * _FullScaleInput;
            }
            UpdateSampleMonitor();
        }

        private void UpdateSampleMonitor()
        {
            // Simple graphic display
            chart1.SuspendLayout();
            for (int i = 0; i < chart1.Series.Count; i++)
                chart1.Series[i].Points.Clear();

            for (int ch = 0; ch < _channelsIn; ch++)
            {
                for (int i = 0; i < _sampleBufferSize; i++)
                {
                    chart1.Series[ch].Points.AddXY(i, _timeSignal[ch][i]);
                }
            }
            chart1.ResumeLayout();
        }

        private void numericUpDownFrequency_ValueChanged(object sender, EventArgs e)
        {
            if (_signalGenerator != null)
                _signalGenerator.Frequency = (float)numericUpDownFrequency.Value;
        }

        private void numericUpDownAmplitude_ValueChanged(object sender, EventArgs e)
        {
            if (_signalGenerator != null)
                _signalGenerator.Gain = (float)numericUpDownLevel.Value * (float)Math.Sqrt(2) / _FullScaleOutput;
        }

        private void comboBoxOutputSignalType_SelectedIndexChanged(object sender, EventArgs e)
        {
            OutputWaveFileName = "";
            numericUpDownFrequency.Enabled = true;
            numericUpDownLevel.Enabled = true;

            if (_signalGenerator != null)
                _signalGenerator.Type = (SignalGeneratorType)comboBoxOutputSignalType.SelectedIndex;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            UpdateGUI();
        }

        private void buttonControlPanel_Click(object sender, EventArgs e)
        {
            if (_asioOut == null)
            {
                _asioOut = new AsioOut(comboBoxAsioDevice.SelectedIndex);
            }
            _asioOut.ShowControlPanel();
        }

        private void buttonStop_Click_1(object sender, EventArgs e)
        {
            Stop();
        }

        private void Stop()
        {
            if (_asioOut != null)
            {
                if (_asioOut.PlaybackState != PlaybackState.Stopped)
                {
                    timerTimeData.Enabled = false;
                    _asioOut.Stop();
                }
            }
            if (_recording)
            {
                _recording = false;
                if (writer != null)
                {
                    writer.Close();
                }
            }
        }

        private void numericUpDownNominalInputGain_ValueChanged(object sender, EventArgs e)
        {
            _FullScaleInput = (float)numericUpDownNominalInputGain.Value;
        }

        private void numericUpDownNominalOutputGain_ValueChanged(object sender, EventArgs e)
        {
            _FullScaleOutput = (float)numericUpDownNominalOutputGain.Value;
            if (_signalGenerator != null)
                _signalGenerator.Gain = (float)numericUpDownLevel.Value * (float)Math.Sqrt(2) / _FullScaleOutput;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void logToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void numericUpDownChannelsIn_ValueChanged(object sender, EventArgs e)
        {
            Stop();
        }

        private void comboBoxFuncion_SelectedIndexChanged(object sender, EventArgs e)
        {
            Stop();
        }

        private void numericUpDownChannelsOut_ValueChanged(object sender, EventArgs e)
        {
            Stop();
        }

        private void comboBoxFftLines_SelectedIndexChanged(object sender, EventArgs e)
        {
            Stop();
        }

        private void comboBoxSampleRate_SelectedIndexChanged(object sender, EventArgs e)
        {
            Stop();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox1 MyAboutDlg = new AboutBox1();
            MyAboutDlg.ShowDialog();
        }

        private void comboBoxOutputSignalType2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_signalGenerator2 != null)
                _signalGenerator2.Type = (SignalGeneratorType)comboBoxOutputSignalType2.SelectedIndex;
        }

        private void numericUpDownFrequency2_ValueChanged(object sender, EventArgs e)
        {
            if (_signalGenerator2 != null)
                _signalGenerator2.Frequency = (float)numericUpDownFrequency2.Value;
        }

        private void numericUpDownLevel2_ValueChanged(object sender, EventArgs e)
        {
            if (_signalGenerator2 != null)
                _signalGenerator2.Gain = (float)numericUpDownLevel2.Value * (float)Math.Sqrt(2) / _FullScaleOutput;

        }

        private void button1_Click(object sender, EventArgs e)
        {
            numericUpDownFrequency.Enabled = false;
            numericUpDownLevel.Enabled = false;

            OpenFileDialog d = new OpenFileDialog();
            d.DefaultExt = "wav";
            d.CheckFileExists = true;
            d.CheckPathExists = true;
            d.Multiselect = false;
            d.InitialDirectory = _musicPath;
            if (d.ShowDialog() == DialogResult.OK)
            {
                OutputWaveFileName = d.FileName;
            }
            else
            {
                OutputWaveFileName = "";
            }
            toolStripStatusLabelOutputFile.Text = OutputWaveFileName;
        }

    }
}
