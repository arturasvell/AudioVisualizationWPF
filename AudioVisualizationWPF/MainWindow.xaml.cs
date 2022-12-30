using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Accord.Math;
using System.Drawing;
using Color = System.Drawing.Color;
using Point = System.Windows.Point;
using SoundPlayer = System.Media.SoundPlayer;
using NAudio.Wave;
using ScottPlot;
using static Accord.Math.FourierTransform;
using AForge.Math;
using Spectrogram;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.Serialization.Formatters.Binary;
using MathNet.Numerics;
using Window = System.Windows.Window;
using Newtonsoft.Json;
using System.Net.Http;
using System.Windows.Media.Animation;

namespace AudioVisualizationWPF
{
    public class WAVFile
    {
        public double duration { get; set; }
        public int chunkID { get; set; }
        public int fileSize { get; set; }
        public int riffType { get; set; }
        public int fmtID { get; set; }
        public int fmtSize { get; set; }
        public int fmtCode { get; set; }
        public int channels { get; set; }
        public int sampleRate { get; set; }
        public int byteRate { get; set; }
        public int fmtBlockAlign { get; set; }
        public int bitDepth { get; set; }
        public int fmtExtraSize { get; set; }
        public int dataID { get; set; }

        public int bytes { get; set; }
        public byte[] byteArray { get; set; }
        public int byteLength { get; set; }
        public double[]? leftChannel { get; set; }
        public double[]? rightChannel { get; set; }
        public bool Read(string filename)
        {
            sampleRate = 0;
            byteLength = 0;
            byteRate = 0;
            leftChannel = null;
            rightChannel = null;
            try
            {
                using (FileStream fs = File.Open(filename, FileMode.Open))
                {
                    BinaryReader reader = new BinaryReader(fs);

                    // chunk 0
                    chunkID = reader.ReadInt32();
                    fileSize = reader.ReadInt32();
                    riffType = reader.ReadInt32();
                    byteLength += 12;

                    // chunk 1
                    fmtID = reader.ReadInt32();
                    fmtSize = reader.ReadInt32(); // bytes for this chunk (expect 16 or 18)
                    byteLength += 8;

                    // 16 bytes.
                    fmtCode = reader.ReadInt16();
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    byteRate = reader.ReadInt32();
                    fmtBlockAlign = reader.ReadInt16();
                    bitDepth = reader.ReadInt16();
                    byteLength += 16;

                    if (fmtSize == 18)
                    {
                        fmtExtraSize = reader.ReadInt16();
                        reader.ReadBytes(fmtExtraSize);
                        byteLength += fmtExtraSize;
                    }

                    // chunk 2
                    dataID = reader.ReadInt32();
                    bytes = reader.ReadInt32();
                    byteLength += 8 + bytes;
                    duration = (byteLength - 8) / byteRate;
                    // data
                    byteArray = reader.ReadBytes(bytes);

                    int bytesForSamp = bitDepth / 8;
                    int nValues = bytes / bytesForSamp;


                    double[] asDouble = null;
                    switch (bitDepth)
                    {
                        case 64:
                            asDouble = new double[nValues];
                            Buffer.BlockCopy(byteArray, 0, asDouble, 0, bytes);
                            asDouble = Array.ConvertAll(asDouble, e => (double)e);
                            break;
                        case 32:
                            asDouble = new double[nValues];
                            Buffer.BlockCopy(byteArray, 0, asDouble, 0, bytes);
                            break;
                        case 16:
                            Int16[]
                                asInt16 = new Int16[nValues];
                            Buffer.BlockCopy(byteArray, 0, asInt16, 0, bytes);
                            asDouble = Array.ConvertAll(asInt16, e => e / (double)(Int16.MaxValue + 1));
                            break;
                        default:
                            return false;
                    }

                    switch (channels)
                    {
                        case 1:
                            leftChannel = asDouble;
                            rightChannel = null;
                            return true;
                        case 2:
                            // de-interleave
                            int nSamps = nValues / 2;
                            leftChannel = new double[nSamps];
                            rightChannel = new double[nSamps];
                            for (int s = 0, v = 0; s < nSamps; s++)
                            {
                                leftChannel[s] = asDouble[v++];
                                rightChannel[s] = asDouble[v++];
                            }
                            return true;
                        default:
                            return false;
                    }
                }
            }
            catch
            {
                Debug.WriteLine("Failed to load: " + filename);
                return false;
            }
        }

        public void AddFadeIn(double timeMarker)
        {

        }
        public void CreateWAV(string filename)
        {
            try
            {
                int bytesForSamp = bitDepth / 8;
                int nValues = bytes / bytesForSamp;
                double[] combinedArray = new double[nValues];
                switch (channels)
                {
                    case 1:
                        combinedArray = leftChannel;
                        break;
                    case 2:
                        break;
                    default:
                        break;
                }
                byte[] toWrite = new byte[bytes];
                switch (bitDepth)
                {
                    case 64:
                        Buffer.BlockCopy(combinedArray, 0, toWrite, 0, bytes);
                        break;
                    case 32:
                        Buffer.BlockCopy(combinedArray, 0, toWrite, 0, bytes);
                        break;
                    case 16:
                        Int16[]
                            asInt16 = new Int16[nValues];
                        byte[] convertedBack = new byte[bytes];
                        for (int i = 0; i < combinedArray.Length; i++)
                        {
                            combinedArray[i] *= (double)(Int16.MaxValue + 1);
                            asInt16[i] = (Int16)combinedArray[i];
                        }
                        Buffer.BlockCopy(asInt16, 0, toWrite, 0, bytes);
                        break;
                    default:
                        break;
                }
                WaveFormat waveFormat = new WaveFormat(sampleRate, channels);
                using (WaveFileWriter writer = new WaveFileWriter("test.wav", waveFormat))
                {
                    writer.Write(toWrite, 0, bytes);
                }
                
            }
            catch
            {
                Debug.Fail("Failed to write file.");
            }


        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string audioFilePath = string.Empty;
        private double audioFileDuration= double.NaN;
        private bool secondChannelExists = false;
        private SoundPlayer currentSoundPlayer = null;
        private double[] currentL = null;
        private double[] currentR = null;
        private WAVFile? file = null;
        private int timeFrom = 0;
        private int timeTo = 0;
        private double[] transformedSignal = null;
        public MainWindow()
        {
            InitializeComponent();
        }
        private void btnBrowseFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "WAV Files (.wav)|*.wav";
                ofd.Title = "Open WAV file";
                ofd.Multiselect = false;
                ofd.CheckFileExists = true;
                if (ofd.ShowDialog() == true)
                {
                    string filePath = ofd.FileName;
                    txtFilePath.Text = filePath;
                    audioFilePath = filePath;
                    currentSoundPlayer = new SoundPlayer(filePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        private void PlotSegmentXY(double[] x, double[] y, WpfPlot plot, string plotName = "Plot", string xLabel="None", string yLabel="None")
        {
            try
            {
                plot.Plot.Clear();
                ScottPlot.Plottable.ScatterPlot signal = plot.Plot.AddScatter(y, x, System.Drawing.Color.Blue);
                plot.Plot.Title(plotName);
                plot.Plot.XLabel(xLabel);
                plot.Plot.YLabel(yLabel);
                plot.Plot.XAxis.TickDensity(3);
                plot.Plot.AxisAuto(0);
                plot.Plot.Benchmark(false);
                plot.Refresh();
                plot.Render();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
        private void PlotSegment(double[] audio, int duration, WpfPlot plot,
            string plotName="Plot")
        {
            try
            {
                plot.Plot.Clear();
                ScottPlot.Plottable.SignalPlot signal = plot.Plot.AddSignal(audio, file.sampleRate, System.Drawing.Color.Blue);
                plot.Plot.Title(plotName);
                plot.Plot.XLabel("Time (seconds)");
                plot.Plot.YLabel("Audio Value");
                plot.Plot.AxisAuto(0);
                plot.Plot.Benchmark(false);
                plot.Refresh();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
        private void PlotAudio(double[]? array, int duration, WpfPlot plot, string plotName="Signal Vizualization - Left Channel",
            string xLabel="Time (seconds)", string yLabel="Audio Value")
        {
            try
            {
                if(array == null)
                {
                    throw new ArgumentException("Signal is empty");
                }
                plot.Plot.Clear();
                
                plot.Plot.AddSignal(array, duration, System.Drawing.Color.Blue);

                plot.Plot.Title(plotName);
                plot.Plot.XLabel(xLabel);
                plot.Plot.YLabel(yLabel);
                plot.Plot.AxisAuto(0);
                plot.Plot.Benchmark(false);

                plot.Render();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

        }
        private void PlotAudio()
        {
            try
            {
                if (File.Exists(audioFilePath))
                {
                    file = new WAVFile();
                    file.Read(audioFilePath);
                    if (file.leftChannel != null)
                    {
                        currentL = file.leftChannel;
                    }
                    if (file.rightChannel != null)
                    {
                        currentR = file.rightChannel;
                    }

                    audioFileDuration = (file.byteLength - 8) / file.byteRate;
                    Debug.WriteLine("Duration is " + audioFileDuration.ToString());
                    PlotAudio(file.leftChannel, file.sampleRate, SignalPlotLeft);
                    //FrequencyDomainPlotLeft.Plot.Clear();
                    //ModifiedFrequencyPlotLeft.Plot.Clear();
                    //FrequencyDomainPlotLeft.Render();
                    //ModifiedFrequencyPlotLeft.Render();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(ex.Message);
            }
        }
        private void btnPlotAudio_Click(object sender, RoutedEventArgs e)
        {
            PlotAudio();
        }

        private void ComputeZeroCrossRate(double[] signal, double duration, int frameLength, out List<double> zeroCross)
        {
            double divider = duration / frameLength;
            double signalCount = signal.Length / divider;
            int signalCountCorrected = 0;
            zeroCross = new List<double>();
            while (true)
            {
                if (Math.Ceiling(signalCount) % 2 != 0)
                {
                    signalCount++;
                }
                else
                {
                    signalCountCorrected = (int)Math.Ceiling(signalCount);
                    break;
                }
            }

            for (int i = 0; i < signal.Length; i += signalCountCorrected / 2)
            {
                double sum = 0;
                for (int j = i; j < i + signalCountCorrected; j++)
                {
                    if (j+1 >= signal.Length)
                    {
                        break;

                    }
                    if (signal[j] != 0)
                    {
                        sum += Math.Abs(ReturnSumPartForZCR(signal[j]) - ReturnSumPartForZCR(signal[j+1]));
                    }

                }
                zeroCross.Add((double)(1 / (double)(2 * signalCountCorrected)) * sum);

            }
            Debug.WriteLine("Done");
        }
        private int ReturnSumPartForZCR(double input)
        {
            if(input>=0)
            {
                return 1;
            }
            else
            {
                return -1;
            }
        }

        private void AddMarkerAtX(double x, WpfPlot plot)
        {
            var plottables = plot.Plot.GetPlottables();
            for (int i = 0; i < plottables.Length; i++)
            {
                if (plottables[i].GetType() == typeof(ScottPlot.Plottable.MarkerPlot))
                {
                    plot.Plot.RemoveAt(i);
                }
            }
            var marker = plot.Plot.AddMarker(x, -1, ScottPlot.MarkerShape.verticalBar, 1000, System.Drawing.Color.Red);
            plot.Render();
        }
        private void mainPlot_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Point point = e.GetPosition(this);
                double x = SignalPlotLeft.Plot.GetCoordinateX((float)point.X);
                AddMarkerAtX(x,SignalPlotLeft);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        private void mainPlot2_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Point point = e.GetPosition(this);
                double x = SubsignalLeft.Plot.GetCoordinateX((float)point.X);
                AddMarkerAtX(x, SubsignalLeft);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="signal"></param>
        /// <param name="duration">Audio duration, in milliseconds</param>
        /// <param name="frameLength">Frame length, in milliseconds</param>
        private void ComputeFrames(double[] signal, double duration, int frameLength, out List<double> energy)
        {
            double divider=duration/frameLength;
            double signalCount = signal.Length / divider;
            int signalCountCorrected = 0;
            energy = new List<double>();
            while(true)
            {
                if(Math.Ceiling(signalCount)%2!=0)
                {
                    signalCount++;
                }
                else
                {
                    signalCountCorrected = (int)Math.Ceiling(signalCount);
                    break;
                }
            }

            for (int i = 0; i < signal.Length; i+=signalCountCorrected/2)
            {
                double sum = 0;
                for (int j = i; j < i+signalCountCorrected; j++)
                {
                    if(j>=signal.Length)
                    {
                        break;
                       
                    }
                    if (signal[j] != 0)
                    {
                        sum += Math.Pow(signal[j], 2);
                    }

                }
                energy.Add((double)(1 / (double)signalCountCorrected) * sum);

            }
            Debug.WriteLine("Done");
        }
        public static IEnumerable<int> Range(int start, int stop, int step = 1)
        {
            if (step == 0)
                throw new ArgumentException(nameof(step));

            return RangeIterator(start, stop, step);
        }
        private static IEnumerable<int> RangeIterator(int start, int stop, int step)
        {
            int x = start;

            do
            {
                yield return x;
                x += step;
                if (step < 0 && x <= stop || 0 < step && stop <= x)
                    break;
            }
            while (true);
        }
        private void ClearPlottables(ScottPlot.WpfPlot plot)
        {
            var plottables = plot.Plot.GetPlottables();
            for (int i = 0; i < plottables.Length; i++)
            {
                if (plottables[i].GetType() == typeof(ScottPlot.Plottable.ScatterPlot))
                {
                    plot.Plot.Remove(plottables[i]);
                }
            }
            plot.Render();
        }
        private void AddMarkerToPlot(ScottPlot.WpfPlot plot, bool isHorizontal, double value, Color color)
        {
            
            if(isHorizontal)
            {
                var marker = plot.Plot.AddLine(0, value, 1000, value, color);
            }
            else
            {
                var marker = plot.Plot.AddLine(value, 0, value,1000, color);
            }

            plot.Render();
        }
        private void ConnectSegments(double value1, double value2, double minValue, ScottPlot.WpfPlot plot)
        {
            var marker = plot.Plot.AddLine(value1, 0, value2, 0, System.Drawing.Color.Blue);
            plot.Render();
        }

        private void btnPlayAudio_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentSoundPlayer == null)
                {
                    throw new Exception("Sound file not opened succesfully!");
                }
                if(currentL==null)
                {
                    throw new Exception("Left channel does not exist!");
                }
                file.CreateWAV("test.wav");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
        }

        private void btnPlayAudioRight_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentSoundPlayer == null)
                {
                    throw new Exception("Sound file not opened succesfully!");
                }
                if (currentL == null)
                {
                    throw new Exception("Right channel does not exist!");
                }
                using (MemoryStream ms = new MemoryStream(currentR.ToByte()))
                {
                    SoundPlayer player = new SoundPlayer(ms);
                    player.Play();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private List<double> GetSegmentFromTo(double[] array, int from, int to)
        {
            List<double> result = new List<double>();
            int indexFrom = (int)(from* array.Length) / (int)(audioFileDuration * 1000);
            int indexTo = (int)(to * array.Length) / (int)(audioFileDuration * 1000);
            for (int i = indexFrom; i <= indexTo; i++)
            {
                result.Add(array[i]);
            }
            return result;
        }
        private T DeepClone<T>(T obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;

                return (T)formatter.Deserialize(ms);
            }
        }
        private double[] SliceSignal(double[] signal)
        {
            int length = signal.Length;
            int limit = 0;
            if(length%2==0)
            {
                limit = length / 2 + 1;
            }
            else
            {
                limit = (length + 1) / 2;
            }
            double[] result = new double[limit];
            for (int i = 0; i < limit; i++)
            {
                result[i] = signal[i];
            }
            return result;
        }
        private double[] ClipArray(double[] array, double min, double max)
        {
            double[] result = new double[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                double element = array[i];
                if(element < min)
                {
                    element = min;
                }
                if(element > max)
                {
                    element = max;
                }
                result[i] = element;
            }
            return result;
        }
        private (double[], double[]) TransformAudio(double[] signal)
        {
            double[] arrayToClip = new double[signal.Length];
            for (int i = 0; i < signal.Length; i++)
            {
                arrayToClip[i] = signal[i] * Math.Pow(2, file.bitDepth - 1);
            }
            double minimum = Math.Pow(-2, file.bitDepth - 1);
            double maximum = Math.Pow(2, file.bitDepth - 1);
            arrayToClip = ClipArray(arrayToClip, minimum, maximum);
            double[] time = Generate.LinearSpaced(arrayToClip.Length, 0, file.duration);
            return (arrayToClip, time);
        }
        private double[] ScaleSignal(double[] signal)
        {
            double[] result = new double[signal.Length];
            if(signal.Length%2==0)
            {
                for (int i = 1; i < signal.Length-1; i++)
                {
                    result[i] = signal[i]*2;
                }
            }
            else
            {
                for (int i = 1; i < signal.Length; i++)
                {
                    result[i] = signal[i] * 2;
                }
            }
            return result;
        }
        private double[] ReverseScaleSignal(double[] signal)
        {
            double[] result = new double[signal.Length];
            if (signal.Length % 2 == 0)
            {
                for (int i = 1; i < signal.Length - 1; i++)
                {
                    result[i] = signal[i] / 2;
                }
            }
            else
            {
                for (int i = 1; i < signal.Length; i++)
                {
                    result[i] = signal[i] / 2;
                }
            }
            return result;
        }
        private void btnGetAudioFragment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                timeFrom = int.Parse(txtFrom.Text);
                timeTo = int.Parse(txtTo.Text);
                if(timeFrom<0)
                {
                    throw new ArgumentException("Time from cannot be less than zero!");
                }
                if (timeTo/1000>audioFileDuration)
                {
                    throw new ArgumentException("Time to cannot be greater than audio file length!");
                }
                (double[] transformed, double[] time) = TransformAudio(currentL);
                List<double> segment=GetSegmentFromTo(transformed, timeFrom, timeTo);
                double[] arraySegment = segment.ToArray();
                JsonOutput output = new JsonOutput();
                output.nontransformed = arraySegment;
                
                double[] subsignalForVisualisation = DeepClone(arraySegment);
                PlotSegment(subsignalForVisualisation, timeTo - timeFrom, SubsignalLeft,String.Format("Subsignal ({0} ms length) Visualization",timeTo-timeFrom));




                double[] hammingMultiplier = MathNet.Numerics.Window.Hamming(transformed.Length);
                double[] plottableSegment = DeepClone<double[]>(transformed);
                for (int i = 0; i < transformed.Length; i++)
                {
                    plottableSegment[i] = plottableSegment[i] * hammingMultiplier[i];
                }
                
                


                string array = JsonConvert.SerializeObject(plottableSegment);
                var url= "http://127.0.0.1:5000/fft";
                var client = new HttpClient();
                var content = new StringContent("{\"input\":" + array + "}",Encoding.UTF8,"application/json");
                Task<HttpResponseMessage> task=client.PostAsync(url, content);
                task.Wait();
                var response = task.Result;
                string stringToDeserialize = string.Empty;
                if ((int)response.StatusCode==200)
                {
                    var result = response.Content.ReadAsStringAsync().Result;
                    string tempString = result[13..];
                    stringToDeserialize = tempString.Remove(tempString.Length - 2);
                }
                plottableSegment = JsonConvert.DeserializeObject<double[]>(stringToDeserialize);
                double[] linspaced = Generate.LinearSpaced(plottableSegment.Length, 0, file.sampleRate);
                plottableSegment = ScaleSignal(SliceSignal(plottableSegment));
                linspaced = Generate.LinearSpaced(plottableSegment.Length, 0, file.sampleRate / 2);
                PlotSegmentXY(plottableSegment, linspaced, FrequencyDomainPlotLeft,"Frequency Domain", "Frequency (Hz)","Amplitude");
                transformedSignal = plottableSegment;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Debug.WriteLine(ex);
            }
            

        }
        private int FrequencyToIndex(double frequency, int sampleRate, int length)
        {
            return Convert.ToInt32((double)(frequency / (double)((double)sampleRate / (double)length)));
        }
        private double[] AddAtFrequency(double frequency, double[] signal)
        {
            double[] result=DeepClone(signal);
            int index = FrequencyToIndex(frequency, file.sampleRate/2, signal.Length);
            if (result[index]!=signal.Max())
                result[index] = signal.Max();
            return result;
        }
        private double[] RemoveAtFrequency(double frequency, double[] signal)
        {
            double[] result = DeepClone(signal);
            int index = FrequencyToIndex(frequency, file.sampleRate / 2, signal.Length);
            if (result[index] != signal.Min())
                result[index] = signal.Min();
            return result;
        }
        private void btnChangeAudio_Click(object sender, RoutedEventArgs e)
        {
            string addAtFrequencyText = txtAddFrequency.Text;
            string removeAtFrequencyText = txtRemoveFrequency.Text;
            List<double> addAtFrequencies = new List<double>();
            List<double> removeAtFrequencies = new List<double>();
            if (!string.IsNullOrWhiteSpace(addAtFrequencyText))
            {
                string[] splitText=addAtFrequencyText.Split(',');
                foreach (var item in splitText)
                {
                    addAtFrequencies.Add(double.Parse(item));
                }
            }
            if (!string.IsNullOrWhiteSpace(removeAtFrequencyText))
            {
                string[] splitText = removeAtFrequencyText.Split(',');
                foreach (var item in splitText)
                {
                    removeAtFrequencies.Add(double.Parse(item));
                }
            }
            double[] modifiedFrequency = null;
            if (addAtFrequencies.Count>0)
            {
                foreach (var item in addAtFrequencies)
                {
                    modifiedFrequency = AddAtFrequency(item, transformedSignal);
                    transformedSignal = modifiedFrequency;
                }
            }
            if(removeAtFrequencies.Count>0)
            {
                foreach (var item in removeAtFrequencies)
                {
                    modifiedFrequency = RemoveAtFrequency(item, transformedSignal);
                    transformedSignal = modifiedFrequency;
                }
            }
            if(modifiedFrequency==null)
            {
                return;
            }
            double[] linspaced = Generate.LinearSpaced(modifiedFrequency.Length, 0, file.sampleRate / 2);
            PlotSegmentXY(modifiedFrequency, linspaced, ModifiedFrequencyPlotLeft, "Modified Frequency Domain", "Frequency (Hz)", "Amplitude");
        }
        private double[] RestoreSignal(double[] modifiedSignal)
        {
            double[] result = modifiedSignal;

            return result;

        }
        private void btnRestoreSignal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string array = JsonConvert.SerializeObject(ReverseScaleSignal(transformedSignal));
                var url = "http://127.0.0.1:5000/inverse_fft";
                var client = new HttpClient();
                var content = new StringContent("{\"input\":" + array +", \"sample_rate\":"+file.sampleRate+ "}", Encoding.UTF8, "application/json");
                Task<HttpResponseMessage> task = client.PostAsync(url, content);
                task.Wait();
                var response = task.Result;
                string stringToDeserialize = string.Empty;
                if ((int)response.StatusCode == 200)
                {
                    var result = response.Content.ReadAsStringAsync().Result;
                    string tempString = result[13..];
                    stringToDeserialize = tempString.Remove(tempString.Length - 2);
                }
                double[] plottableSegment = JsonConvert.DeserializeObject<double[]>(stringToDeserialize);
                double[] hammingMultiplier = MathNet.Numerics.Window.HammingPeriodic(plottableSegment.Length);
                for (int i = 0; i < plottableSegment.Length; i++)
                {
                    plottableSegment[i] = plottableSegment[i] * hammingMultiplier[i];
                }
                double[] time = Generate.LinearSpaced(plottableSegment.Length, 0, file.duration*1000);
                PlotSegmentXY(ReverseScaleSignal(plottableSegment), time, RestoredSignalLeft, String.Format("Restored Signal Visualization"),"Duration (ms)","Amplitude");

                array = JsonConvert.SerializeObject(ReverseScaleSignal(plottableSegment));
                url = "http://127.0.0.1:5000/gen_audio";
                client = new HttpClient();
                content = new StringContent("{\"input\":" + array + ", \"sample_rate\":" + file.sampleRate + "}", Encoding.UTF8, "application/json");
                task = client.PostAsync(url, content);
                task.Wait();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            
        }
    }
    public class JsonOutput
    {
        public double[] transformed;
        public double[] nontransformed;
        public int sampleRate;
    }
}
