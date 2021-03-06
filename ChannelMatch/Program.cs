﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChannelMatch
{
    class Program
    {
        static void Main(string[] args)
        {
            List<string> outputlines = new List<string>();
            LoadFromPhysioNet(outputlines);
            ExecuteCommand(outputlines);

            GenerateNeucomFiles();

            Console.ReadKey();
        }

        private static void GenerateNeucomFiles()
        {
            int count = 1;
            using (StreamWriter classFile = new StreamWriter(@"C:\Users\DarkBlue\Google Drive\AUT\Neuro Informatics\Assign2\Neucube Data\tar_class_labels.csv"))
            {
                foreach (var file in Directory.GetFiles(@"C:\cygwin64\home\DarkBlue", "*.csv"))
                {
                    string filePath = Path.Combine(@"C:\Users\DarkBlue\Google Drive\AUT\Neuro Informatics\Assign2\Neucube Data", string.Format("sam{0}_eeg.csv", count++));
                    using (StreamWriter outputFile = new StreamWriter(filePath))
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

        private static int GetClass(string file)
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

        private static void LoadFromPhysioNet(List<string> outputlines)
        {
            string directory = @"C:\Users\DarkBlue\Google Drive\AUT\Neuro Informatics\Assign2\Original data\Files";
            List<double> classes = new List<double> { 8.57 };
            //List<double> classes = new List<double> { 6.66, 7.5, 8.57, 10, 12 };
            string output = Path.Combine(directory, "Commands", "commands.txt");

            foreach (var file in Directory.GetFiles(directory))
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
                                outputlines.Add(string.Format("rdsamp -r mssvepdb/{0} -c -H -f {1} -t {2} -v -pd -s 46 36 35 1 100 125 68 95 201 20 182 169 149 223 17 115 >{0}_{1}_To_{2}_{3}.csv", name, toatal, toatal + 5, cl));
                            }
                        }
                    }

                    if (count == 1) break;
                    //if (count == 5) break;
                }
            }

            using (StreamWriter file = new StreamWriter(output))
            {
                foreach (string line in outputlines)
                {
                    file.WriteLine(line);
                }
            }
        }

        public static void ExecuteCommand(List<string> lines)
        {
            Process proc = new Process();
            string stOut = "";

            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.FileName = @"C:\cygwin64\bin\bash.exe";
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

            foreach (string line in lines)
            {
                sw.WriteLine(line);
            }

            sw.Close();
            sr.Close();

            proc.WaitForExit();
            proc.Close();
        }

        private static void ChannelMatch()
        {
            string text = File.ReadAllText(@"C:\Users\DarkBlue\Google Drive\AUT\Neuro Informatics\Assign2\NC coordinates_65 channels\65channel_name.txt");
            List<string> channels = new List<string>();
            foreach (Match match in Regex.Matches(text, @"\w*"))
            {
                if (!string.IsNullOrEmpty(match.Value))
                    channels.Add(match.Value);
            }

            string[] headerLines = File.ReadAllLines(@"C:\Users\DarkBlue\Google Drive\AUT\Neuro Informatics\Assign2\Data\CORR.txt");
            List<string> sourceChannels = new List<string>();
            foreach (string line in headerLines)
            {
                sourceChannels.Add(line.Split(' ').First());
            }

            File.WriteAllLines(@"C:\Users\DarkBlue\Google Drive\AUT\Neuro Informatics\Assign2\Neucube\ChannelName.txt", sourceChannels.Where(x => channels.Contains(x)));

            Console.ReadKey();
        }
    }
}
