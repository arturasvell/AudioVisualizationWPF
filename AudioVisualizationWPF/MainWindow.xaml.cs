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
        private void PlotSegmentXY(double[] x, double[] y, WpfPlot plot, string plotName = "Plot")
        {
            try
            {
                plot.Plot.Clear();
                ScottPlot.Plottable.ScatterPlot signal = plot.Plot.AddScatter(y, x, System.Drawing.Color.Blue);
                plot.Plot.Title(plotName);
                plot.Plot.XLabel("Time (seconds)");
                plot.Plot.YLabel("Audio Value");
                plot.Plot.AxisAuto(0);
                plot.Plot.Benchmark(true);
                plot.Refresh();
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
                plot.Plot.Benchmark(true);
                plot.Refresh();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
        private void PlotAudio(double[]? left, double[]? right, int duration)
        {
            try
            {
                mainPlot.Plot.Clear();
                mainPlot2.Plot.Clear();
                
                ScottPlot.Plottable.SignalPlot signal = mainPlot.Plot.AddSignal(left, duration, System.Drawing.Color.Blue);
                
                if (right != null)
                {
                    
                    secondChannelExists = true;
                    ScottPlot.Plottable.SignalPlot signal2 = mainPlot2.Plot.AddSignal(right, duration, System.Drawing.Color.Red);
                    
                }
                else
                {
                    secondChannelExists = false;
                }
                mainPlot.Plot.Title("WAV File Data - Left Channel");
                mainPlot.Plot.XLabel("Time (seconds)");
                mainPlot.Plot.YLabel("Audio Value");
                mainPlot.Plot.AxisAuto(0);
                mainPlot.Plot.Benchmark(true);

                mainPlot2.Plot.Title("WAV File Data - Right Channel");
                mainPlot2.Plot.XLabel("Time (seconds)");
                mainPlot2.Plot.YLabel("Audio Value");
                mainPlot2.Plot.AxisAuto(0);
                mainPlot2.Plot.Benchmark(true);

                mainPlot2.Render();

                mainPlot.Render();
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
                    PlotAudio(file.leftChannel, file.rightChannel, file.sampleRate);
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

        private void AddMarkerAtX(double x, bool addToSecondPlot=false)
        {
            if(!addToSecondPlot)
            {
                var plottables = mainPlot.Plot.GetPlottables();
                for (int i = 0; i < plottables.Length; i++)
                {
                    if (plottables[i].GetType() == typeof(ScottPlot.Plottable.MarkerPlot))
                    {
                        mainPlot.Plot.RemoveAt(i);
                    }
                }
                var marker = mainPlot.Plot.AddMarker(x, -1, ScottPlot.MarkerShape.verticalBar, 1000, System.Drawing.Color.Red);
                mainPlot.Render();
            }
            else
            {
                if (secondChannelExists)
                {
                    var plottables = mainPlot2.Plot.GetPlottables();
                    for (int i = 0; i < plottables.Length; i++)
                    {
                        if (plottables[i].GetType() == typeof(ScottPlot.Plottable.MarkerPlot))
                        {
                            mainPlot2.Plot.RemoveAt(i);
                        }
                    }
                    var marker = mainPlot2.Plot.AddMarker(x, -1, ScottPlot.MarkerShape.verticalBar, 1000, System.Drawing.Color.Red);
                    mainPlot2.Render();
                }
                
            }
        }
        private void mainPlot_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Point point = e.GetPosition(this);
                double x = mainPlot.Plot.GetCoordinateX((float)point.X);
                AddMarkerAtX(x);
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
                double x = mainPlot2.Plot.GetCoordinateX((float)point.X);
                AddMarkerAtX(x,true);
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
                for (int i = 0; i < signal.Length-1; i++)
                {
                    result[i] = signal[i]*2/1000000;
                }
            }
            else
            {
                for (int i = 0; i < signal.Length; i++)
                {
                    result[i] = signal[i] * 2 / 1000000;
                }
            }
            return result;
        }
        private void btnGetAudioFragment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int timeFrom = int.Parse(txtFrom.Text);
                int timeTo = int.Parse(txtTo.Text);
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
                
                int length = segment.Count;
                PlotSegment(arraySegment, timeTo - timeFrom,mainPlot5);
                double[] hammingMultiplier = MathNet.Numerics.Window.HammingPeriodic(length);
                for (int i = 0; i < length; i++)
                {
                    arraySegment[i] = arraySegment[i] * hammingMultiplier[i];
                }
                double[] plottableSegment = DeepClone<double[]>(arraySegment);
                
                PlotSegment(plottableSegment, timeTo - timeFrom, mainPlot6);
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
                double[] linspaced = Generate.LinearSpaced(plottableSegment.Length, 0, file.sampleRate / 2);
                PlotSegmentXY(plottableSegment, linspaced, mainPlot6);
                plottableSegment = ScaleSignal(SliceSignal(plottableSegment));
                linspaced = Generate.LinearSpaced(plottableSegment.Length, 0, file.sampleRate / 2);
                PlotSegmentXY(plottableSegment, linspaced, mainPlot6);
                //MathNet.Numerics.IntegralTransforms.Fourier.ForwardReal(arraySegment, segment.Count-2);

                //for (int i = 0; i < arraySegment.Length; i++)
                //{
                //    arraySegment[i] = Math.Abs(arraySegment[i]);
                //}


                ////PlotSegment(ScaleSignal(SliceSignal(arraySegment)), timeTo - timeFrom, mainPlot6);
                //double[] linspaced = Generate.LinearSpaced(SliceSignal(arraySegment).Length, 0, file.sampleRate / 2);
                ////PlotSegmentXY(ScaleSignal(SliceSignal(arraySegment)), linspaced, mainPlot6);
                //List<double> cutArray=new List<double>();
                //if(arraySegment.Length%2==0)
                //{
                //    for (int i = 0; i < arraySegment.Length/2+1; i++)
                //    {
                //        cutArray.Add(arraySegment[i]);
                //    }
                //    for (int i = 1; i < cutArray.Count-1; i++)
                //    {
                //        cutArray[i] *= 2;
                //    }
                //}
                //else
                //{
                //    for (int i = 0; i < (arraySegment.Length+1)/2; i++)
                //    {
                //        cutArray.Add(arraySegment[i]);
                //    }
                //    for (int i = 1; i < cutArray.Count; i++)
                //    {
                //        cutArray[i] *= 2;
                //    }
                //}
                //Debug.WriteLine(file.sampleRate);
                //output.transformed = cutArray.ToArray();
                //output.sampleRate = file.sampleRate;
                //string strJson=Newtonsoft.Json.JsonConvert.SerializeObject(output);
                ////PlotSegment(output.transformed, timeTo - timeFrom, mainPlot6);
                //Debug.WriteLine(strJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
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
