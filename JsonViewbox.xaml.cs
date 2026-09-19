using AssetRegistryModMigrator.Classes;
using AssetRegistryModMigrator.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace AssetRegistryModMigrator
{
    /// <summary>
    /// Interaction logic for JsonViewbox.xaml
    /// </summary>
    public partial class JsonViewbox : Window
    {
        private FileNode _file;

        public FileNode File { get => _file; set => _file = value; }

        public JsonViewbox(FileNode file)
        {
            _file = file;
            InitializeComponent();
            JsonTextBox.Text = file.GetAllAssets().Select(x => JsonConvert.SerializeObject(x, Formatting.Indented, new JsonSerializerSettings() { })).Aggregate((first, second) => first + ",\n" + second); ;
        }
    }
}
