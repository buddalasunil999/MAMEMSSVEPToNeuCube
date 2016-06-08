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
            string destination = Path.Combine(directory, "output");
            if (!Directory.Exists(destination))
                Directory.CreateDirectory(destination);

            string classFileLocation = Path.Combine(destination, "tar_class_labels.csv");

            string freqDir = Path.Combine(directory, "freq");
            if (!Directory.Exists(freqDir))
                Directory.CreateDirectory(freqDir);

            string csvFilesDirectory = Path.Combine(directory, "csv");
            if (!Directory.Exists(csvFilesDirectory))
                Directory.CreateDirectory(csvFilesDirectory);

            //LoadFrequencyFilesFromPhysioNet(freqDir, cygwinLocation);
            PopulateSubjects(freqDir);
        }

        private void PopulateSubjects(string freqDir)
        {
            foreach (var file in Directory.GetFiles(freqDir).Select(path => Path.GetFileName(path).Split('_')[0]).Distinct())
            {
                checkedListBox1.Items.Add(file, true);
            }
        }

        private void LoadFrequencyFilesFromPhysioNet(string freqDir, string cygwinLocation)
        {
            List<OutputLine> lines = new List<OutputLine>();
            for (int i = 1; i < 12; i++)
            {
                string num = i.ToString();
                if (i < 10)
                    num = "0" + num;
                string name = string.Format("S0{0}a_freq.txt", num);
                string path = Path.Combine(freqDir, name);
                string command = string.Format("rdann -r mssvepdb/dataset1/S001a -a freq -v >'{0}'", path);
                lines.Add(new OutputLine(command, path, name.Split('_')[0]));
            }

            foreach (var line in lines)
            {
                ExecuteCommand(cygwinLocation, line, (li) => { });
            }
        }

        private void LoadFromPhysioNet(string directory, string freqDir, string destFolder, List<OutputLine> outputlines)
        {
            bool period = false;

            List<double> classes = new List<double> { 6.66, 7.5, 8.57, 10, 12 };
            if (!Directory.Exists(Path.Combine(directory, "Commands")))
                Directory.CreateDirectory(Path.Combine(directory, "Commands"));

            string output = Path.Combine(directory, "Commands", "commands.txt");

            foreach (var file in Directory.GetFiles(freqDir))
            {
                string name = Path.GetFileNameWithoutExtension(file).Split('_')[0];
                var lines = File.ReadAllLines(file).Skip(1);

                int count = 0;
                List<string> covered = new List<string>();
                string begin = string.Empty;
                string end = string.Empty;

                for (int i = 0; i < lines.Count(); i++)
                {
                    var line = lines.ElementAt(i);

                    string[] splits = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                    string last = splits.Last();

                    if (last.EndsWith("B"))
                    {
                        begin = splits.First();
                    }
                    else
                    {
                        end = splits.First();

                        double val = Convert.ToDouble(last.Remove(last.Length - 1));

                        foreach (double cl in classes)
                        {
                            if (Math.Abs(cl - val) < 0.5 && !covered.Contains(cl.ToString()))
                            {
                                count++;
                                string[] timeformats = { @"m\:ss", @"mm\:ss", @"h\:mm\:ss" };
                                int total = Convert.ToInt32(TimeSpan.ParseExact(splits.First().Split('.')[0], timeformats, CultureInfo.InvariantCulture).TotalSeconds);

                                string beginTime = begin;
                                string endTime = end;

                                if (period)
                                {
                                    beginTime = (total - 5).ToString();
                                    endTime = (total + 5).ToString();
                                }

                                string destPath = Path.Combine(destFolder, string.Format("{0}_{1}_To_{2}_{3}.csv", name, beginTime.Replace(":", "."), endTime.Replace(":", "."), cl));
                                string command = string.Format("rdsamp -r mssvepdb/dataset1/{0} -c -H -f {1} -t {2} -v -pd -s 46 36 35 1 100 125 68 95 201 20 182 169 149 223 17 115 >'{3}'", name, beginTime, endTime, destPath);
                                outputlines.Add(new OutputLine(command, destPath, name));
                                covered.Add(cl.ToString());
                                break;
                            }
                        }
                    }

                    if (count == classes.Count) break;
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
            if (!File.Exists(classFileLocation))
                File.Create(classFileLocation).Close();

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
                File.Create(classFileLocation).Close();

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

        public void ExecuteCommand(string cygwinLocation, OutputLine line, Action<OutputLine> callBack)
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


            if (checkBox1.Checked || !File.Exists(line.Path.Replace(":", ".")))
                sw.WriteLine(line.Command);

            sw.Close();
            sr.Close();

            proc.WaitForExit();
            proc.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            bool isDistinct = !checkBox2.Checked;

            string path = Path.Combine(textBox1.Text, "output");

            var allFiles = Directory.GetFiles(path, "sam*");

            foreach (var file in allFiles)
            {
                string[] allLines = File.ReadAllLines(file);

                int length = allLines[0].Split(',').Count();

                int count = 0;
                int take = allLines.Length / Convert.ToInt32(textBox3.Text);
                var selectedLines = new List<string>();

                do
                {
                    var filteredLines = allLines.Skip(count).Take(take).Select(x => x.Split(',')).ToList();
                    List<string> values = new List<string>();

                    if (isDistinct)
                    {
                        values = filteredLines[0].ToList();
                    }
                    else
                    {
                        for (int i = 0; i < length; i++)
                        {
                            double sum = 0;
                            foreach (var item in filteredLines)
                            {
                                sum += (item[i] == "-" ? 0 : Convert.ToDouble(item[i]));
                            }

                            values.Add((sum / filteredLines.Count()).ToString());
                        }
                    }

                    selectedLines.Add(string.Join(",", values));

                    count += take;
                } while (count < allLines.Length);

                File.WriteAllLines(file, selectedLines);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string path = Path.Combine(textBox1.Text, "output");
            DirectoryInfo info = new DirectoryInfo(path);
            string destination = Path.Combine(info.Parent.FullName, info.Name + "_others");

            if (!Directory.Exists(destination))
                Directory.CreateDirectory(destination);

            var allFiles = Directory.GetFiles(path, "sam*");

            string[] allClasses = File.ReadAllLines(Path.Combine(path, "tar_class_labels.csv"));

            List<string> combined1 = new List<string>();
            List<string> combined2 = new List<string>();
            combined1.Add("F7,FP1,F3,F8,Pz,Oz,T7,P7,T8,Fz,C4,P8,O2,F4,FP2,O1,Class");

            for (int i = 0; i < allFiles.Length; i++)
            {
                string file = allFiles[i];
                string cl = allClasses[i];

                var lines = File.ReadAllLines(file);
                foreach (var line in lines)
                {
                    combined1.Add(line + "," + cl);
                    combined2.Add(string.Join(" ", line.Split(',')) + " " + cl);
                }
            }

            string dest1 = Path.Combine(destination, "data.csv");
            string dest2 = Path.Combine(destination, "data.txt");
            if (!File.Exists(dest1))
                File.Create(dest1).Close();

            if (!File.Exists(dest2))
                File.Create(dest2).Close();

            File.WriteAllLines(dest1, combined1);
            File.WriteAllLines(dest2, combined2);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            string directory = textBox1.Text;
            string cygwinLocation = textBox2.Text;
            string destination = Path.Combine(directory, "output");
            string classFileLocation = Path.Combine(destination, "tar_class_labels.csv");
            string freqDir = Path.Combine(directory, "freq");
            string csvFilesDirectory = Path.Combine(directory, "csv");

            List<OutputLine> outputlines = new List<OutputLine>();
            LoadFromPhysioNet(directory, freqDir, csvFilesDirectory, outputlines);

            if (checkedListBox1.CheckedItems.Count > 0)
            {
                foreach (var line in outputlines.Where(x => checkedListBox1.CheckedItems.Cast<string>().Contains(x.Name)))
                {
                    ExecuteCommand(cygwinLocation, line, (li) =>
                  {
                      //File.WriteAllLines(Path.Combine(directory, "Commands", "commands.txt"), outputlines.Except(new[] { li }).Select(x => x.Command));
                  });
                }
            }

            GenerateNeucomFiles(classFileLocation, outputlines, destination);
        }
    }

    public class OutputLine
    {
        static int count = 0;

        public OutputLine(string command, string path, string name)
        {
            count++;
            Command = command;
            Path = path;
            Count = count;
            Name = name;
        }

        public string Command { set; get; }
        public int Count { get; internal set; }
        public string Path { set; get; }
        public string Name { set; get; }
    }
}
