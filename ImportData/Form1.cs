using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ImportData
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string directory = textBox1.Text;
            string cygwinLocation = textBox2.Text;
            string destination = textBox4.Text;
            string classFileLocation = Path.Combine(destination, "tar_class_labels.csv");

            string freqDir = Path.Combine(directory, "freq");
            if (!Directory.Exists(freqDir))
                Directory.CreateDirectory(freqDir);

            string csvFilesDirectory = Path.Combine(directory, "csv");
            if (!Directory.Exists(csvFilesDirectory))
                Directory.CreateDirectory(csvFilesDirectory);

            LoadFrequencyFilesFromPhysioNet(freqDir, cygwinLocation);
            List<OutputLine> outputlines = new List<OutputLine>();
            LoadFromPhysioNet(directory, freqDir, csvFilesDirectory, outputlines);
            ExecuteCommand(cygwinLocation, outputlines, (li) => { });
            GenerateNeucomFiles(classFileLocation, outputlines, destination);
        }

        private void LoadFrequencyFilesFromPhysioNet(string freqDir, string cygwinLocation)
        {
            List<OutputLine> lines = new List<OutputLine>();
            for (int i = 1; i < 12; i++)
            {
                string num = i.ToString();
                if (i < 10)
                    num = "0" + num;
                string path = Path.Combine(freqDir, string.Format("S0{0}a_freq.txt", num));
                string command = string.Format("rdann -r mssvepdb/S001a -a freq -v >'{0}'", path);
                lines.Add(new OutputLine(command, path));
            }

            ExecuteCommand(cygwinLocation, lines, (li) => { });
        }

        private void LoadFromPhysioNet(string directory, string freqDir, string destFolder, List<OutputLine> outputlines)
        {
            List<double> classes = new List<double> { 6.66, 7.5, 8.57, 10, 12 };
            if (!Directory.Exists(Path.Combine(directory, "Commands")))
                Directory.CreateDirectory(Path.Combine(directory, "Commands"));

            string output = Path.Combine(directory, "Commands", "commands.txt");

            foreach (var file in Directory.GetFiles(freqDir))
            {
                string name = Path.GetFileNameWithoutExtension(file).Split('_')[0];
                string[] lines = File.ReadAllLines(file);

                int count = 0;
                foreach (var line in lines.Skip(1))
                {
                    string[] splits = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                    string last = splits.Last();

                    if (last.EndsWith("B"))
                    {
                        double val = Convert.ToDouble(last.Remove(last.Length - 1));

                        foreach (double cl in classes)
                        {
                            if (Math.Abs(cl - val) < 0.5)
                            {
                                count++;
                                string[] timeformats = { @"m\:ss", @"mm\:ss", @"h\:mm\:ss" };
                                int toatal = Convert.ToInt32(TimeSpan.ParseExact(splits.First().Split('.')[0], timeformats, CultureInfo.InvariantCulture).TotalSeconds);

                                string destPath = Path.Combine(destFolder, string.Format("{0}_{1}_To_{2}_{3}.csv", name, toatal, toatal + 5, cl));
                                string command = string.Format("rdsamp -r mssvepdb/{0} -c -H -f {1} -t {2} -v -pd -s 46 36 35 1 100 125 68 95 201 20 182 169 149 223 17 115 >'{3}'", name, toatal, toatal + 5, destPath);
                                outputlines.Add(new OutputLine(command, destPath));
                            }
                        }
                    }

                    if (count == 5) break;
                }
            }

            using (StreamWriter file = new StreamWriter(output))
            {
                foreach (OutputLine line in outputlines)
                {
                    file.WriteLine(line.Command);
                }
            }
        }

        private void GenerateNeucomFiles(string classFileLocation, List<OutputLine> outputlines, string destination)
        {
            using (StreamWriter classFile = new StreamWriter(classFileLocation))
            {
                foreach (var output in outputlines)
                {
                    string file = output.Path;

                    string destFilePath = Path.Combine(destination, string.Format("sam{0}_eeg.csv", output.Count));
                    using (StreamWriter outputFile = new StreamWriter(destFilePath))
                    {
                        foreach (var line in File.ReadAllLines(file).Skip(2))
                        {
                            outputFile.WriteLine(line.Remove(0, line.IndexOf(',') + 1));
                        }
                    }

                    int cat = GetClass(file);
                    classFile.WriteLine(cat);
                }
            }
        }

        private void GenerateNeucomFile(string classFileLocation, OutputLine physionetFile, string destination)
        {
            if (!File.Exists(classFileLocation))
                File.Create(classFileLocation);

            using (StreamWriter classFile = new StreamWriter(classFileLocation, true))
            {
                string filePath = Path.Combine(destination, string.Format("sam{0}_eeg.csv", physionetFile.Count));
                using (StreamWriter outputFile = new StreamWriter(filePath))
                {
                    foreach (var line in File.ReadAllLines(physionetFile.Path).Skip(2))
                    {
                        outputFile.WriteLine(line.Remove(0, line.IndexOf(',') + 1));
                    }
                }

                int cat = GetClass(physionetFile.Path);
                classFile.WriteLine(cat);
                classFile.Close();
            }
        }

        private int GetClass(string file)
        {
            double cat = Convert.ToDouble(Path.GetFileNameWithoutExtension(file).Split('_').Last());

            switch ((int)cat)
            {
                case 6:
                    return 1;
                case 7:
                    return 2;
                case 8:
                    return 3;
                case 10:
                    return 4;
                case 12:
                    return 5;
            }

            return 0;
        }

        public void ExecuteCommand(string cygwinLocation, List<OutputLine> lines, Action<OutputLine> callBack)
        {
            Process proc = new Process();
            string stOut = "";

            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.FileName = cygwinLocation;
            proc.StartInfo.Arguments = "-i -l";

            proc.Start();

            StreamWriter sw = proc.StandardInput;
            StreamReader sr = proc.StandardOutput;
            StreamReader se = proc.StandardError;

            sw.AutoFlush = true;

            string cmd = "ls -lh";
            sw.WriteLine(cmd);

            //while (true)
            //{
            //    //if(sr.Peek() >= 0)
            //    {
            //        Console.WriteLine("sr.Peek = " + sr.Peek());
            //        Console.WriteLine("sr = " + sr.ReadLine());
            //    }

            //    if (se.Peek() >= 0)
            //    {
            //        Console.WriteLine("se.Peek = " + se.Peek());
            //        Console.WriteLine("se = " + se.ReadLine());
            //    }
            //}

            for (int i = 1; i < lines.Count; i++)
            {
                var currentLine = lines[i];
                sw.WriteLine(currentLine.Command);
            }

            sw.Close();
            sr.Close();

            proc.WaitForExit();
            proc.Close();
        }
    }

    public class OutputLine
    {
        static int count = 0;

        public OutputLine(string command, string path)
        {
            count++;
            Command = command;
            Path = path;
            Count = count;
        }

        public string Command { set; get; }
        public int Count { get; internal set; }
        public string Path { set; get; }
    }
}
