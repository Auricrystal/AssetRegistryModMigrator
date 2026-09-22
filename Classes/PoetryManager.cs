using IronPython.Hosting;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace AssetRegistryModMigrator.Classes
{
    internal class PoetryManager
    {
        public enum VersionCheck { NotInstalled, OutOfDate, Newer, Current }
        //private Process Prompt { get; set; }
        //private StreamWriter SW { get; set; }
        //private StreamReader SR { get; set; }
        //public Queue<string> CMD_Output { get; set; } = new Queue<string>();

        string Directory { get; set; } = "";
        public PoetryManager(string Directory)
        {
            //var project=OpenDialog("Poetry Project Directory", ".toml", true);
            if (!File.Exists($"{Directory}/pyproject.toml"))
                throw new Exception("Can't Find pyproject.toml");
            var temp = READ_VALUE("build-system", "build-backend", $"{Directory}/pyproject.toml").Trim('"');
            if (!temp.Equals("poetry.core.masonry.api"))
                throw new Exception("pyproject.toml does not use poetry");
            this.Directory = Directory;
            var ver = CheckPythonInstall();
            if (ver == VersionCheck.NotInstalled)
                if (MessageBox.Show("Python not installed, would you like to download?", "Python Required", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = @"https://www.python.org/downloads/",
                        UseShellExecute = true
                    });
            ver = CheckPoetryInstall();
            if (ver == VersionCheck.NotInstalled)
                if (MessageBox.Show("Poetry not installed, would you like to download?", "Poetry Required", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = @"https://github.com/python-poetry/install.python-poetry.org",
                        UseShellExecute = true
                    });

            //MessageBox.Show($"Version is {ver.ToString()}", "Python Installation", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public VersionCheck CheckPythonInstall()
        {
            var PyIniValue = READ_VALUE("tool.poetry.dependencies", "python", $"{Directory}/pyproject.toml").Trim('"');
            Regex VersionCheckRegex = new Regex(@"(?:\d+\.?)+");
            var PythonToml = VersionCheckRegex.Match(PyIniValue).Value;
            var PythonInstalled = VersionCheckRegex.Match(CallCommand("python --version", true)).Value;
            if (string.IsNullOrEmpty(PythonInstalled))
                return VersionCheck.NotInstalled;

            var installed = new Version(PythonInstalled);
            var poetry = new Version(PythonToml);
            switch (installed.CompareTo(poetry))
            {
                case -1: Debug.WriteLine("OLDER"); return VersionCheck.OutOfDate;
                case 0: Debug.WriteLine("CURRENT"); return VersionCheck.Current;
                case 1: Debug.WriteLine("NEWER"); return VersionCheck.Newer;
                default: return VersionCheck.NotInstalled;
            }
        }

        public VersionCheck CheckPoetryInstall()
        {
            Regex VersionCheckRegex = new Regex(@"(?:\d+\.?)+");
            var PythonInstalled = VersionCheckRegex.Match(CallCommand("poetry --version", true)).Value;
            if (string.IsNullOrEmpty(PythonInstalled))
                return VersionCheck.NotInstalled;
            else
                return VersionCheck.Current;
        }
        public string CallCommand(string args, bool single) { return CallCommand(args).FirstOrDefault() ?? ""; }
        public List<string> CallCommand(string args)
        {
            // Start the child process.
            Process p = new()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    WorkingDirectory = Directory,
                    CreateNoWindow = true,
                    Arguments = args
                }
            };
            p.Start();
            string? o = "";
            //Eat cmd initial lines
            while (!string.IsNullOrEmpty(o = p.StandardOutput.ReadLine())) ;

            //input command
            p.StandardInput.WriteLine(args);

            //Grab each line
            List<string> lines = new List<string>();
            var dir = new DirectoryInfo(Directory).FullName;
            while (!string.IsNullOrEmpty(o = p.StandardOutput.ReadLine()))
                if (o != $"{dir}>{args}")//ignore input line getting caught
                    lines.Add(o);
                else continue;

            //close process
            p.Close();
            return lines;//discard early results as they may be input lines
        }
        public void BinToJson()
        {
            var bin = OpenDialog("Bin", ".bin");
            if (bin == null) return;
            var json = OpenDialog("Json", ".json", false);
            BinToJson(bin, json);
        }

        public void BinToJson(string binInput, string jsonOutput) => CallCommand($"poetry run asset_reg bin_to_json {binInput} -o {jsonOutput}");
        public void JsonToBin()
        {
            var json = OpenDialog("Json", ".json");
            if (json == null) return;
            var bin = OpenDialog("Bin", ".bin", false);
            
            JsonToBin(json, bin);
        }

        public void JsonToBin(string jsonOutput, string binInput) => CallCommand($"poetry run asset_reg json_to_bin {jsonOutput} -o {binInput}");

        private static string? OpenDialog(string Title, string Ext, bool CheckExists = true)
        {
            //filename = "";
            //if (registry == null) return;
            FileDialog dialog = new OpenFileDialog() { DefaultExt = $"{Ext}", Title = Title, Filter = $"{Ext.Substring(1)} Files ({Ext})|*{Ext}", CheckFileExists = CheckExists };
            Nullable<bool> result = dialog.ShowDialog();
            if (!result.Value) return null;

            return dialog.FileName;
        }

        public string READ_VALUE(string SECTION, string KEY, string PATH, string DEFAULT_VALUE = "")
        {
            // read all lines from the file
            string[] READER_LINES = File.ReadAllLines(PATH);

            // we are going to capture the value of a "section" line here, so we can 
            // test to see if subsequent lines belong to the section we are
            // looking for
            string CURRENT_SECTION = "";

            // iterate all the lines until we find the section and key we want
            foreach (string READER_LINE in READER_LINES)
            {
                // detect if the line is a [SECTION_NAME] and capture it as the current section
                if (READER_LINE.StartsWith("[") && READER_LINE.EndsWith("]"))
                {
                    CURRENT_SECTION = READER_LINE;
                }
                else if (CURRENT_SECTION.Equals($"[{SECTION}]"))
                {
                    // The current line is not a section header

                    // The current section is the section we are looking for, so lets process 
                    // the lines within it

                    // now lets split the current line into a key/value pair using = as the delimitor
                    string[] lineParts = READER_LINE.Split(new[] { '=' }, 2).Select(x => x.Trim()).ToArray();

                    // test if part 1 of the line PythonToml the KEY we are looking for
                    if (lineParts.Length >= 1 && lineParts[0] == KEY)
                    {
                        // we have found the key.
                        // now return part 2 of the line as the value, or DEFAULT_VALUE if the split 
                        // operation above could not find a part 2 to add to the list
                        return lineParts.Length >= 2
                            ? lineParts[1]
                            : DEFAULT_VALUE;
                    }
                }
            }

            // we have not found a match, so return the default value instead
            return DEFAULT_VALUE;
        }
    }
}
