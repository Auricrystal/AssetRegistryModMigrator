using AssetRegistryModMigrator.Classes;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace AssetRegistryModMigrator.Model
{
    public abstract class TreeNode : ObservableObject
    {
        public TreeNode(string name)
        {
            Name = name;
        }
        public TreeNode(string name, TreeNode parent) : this(name)
        {
            Name = name;
            ParentNode = parent;
        }
        protected TreeNode(Asset asset)
        {
            Name = asset.PackageName.Split("/", StringSplitOptions.RemoveEmptyEntries).Last();
        }
        public int Depth { get => (IsRoot ? 1 : ParentNode.Depth + 1); }
        public int HashCode { get => GetHashCode(); }
        public string Name { get; set; }
        public string Path { get { return (ParentNode is not null) ? ParentNode?.ToString() + "/" + Name : "/" + Name; } }
        public string FullPath { get => Path + "/" + Name; }
        public TreeNode? ParentNode { get; set; }
        public bool IsRoot { get => ParentNode is null; }
        public bool IsLeaf => this is FileNode;
        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged("IsExpanded");
                }
            }
        }

        public bool? _selected = false;
        public bool? Selected
        {
            get { return _selected; }
            set
            {
                if (value != _selected)
                {
                    _selected = value;
                    OnPropertyChanged("Selected");
                }
            }
        }


        public override string? ToString()
        {
            return (ParentNode is not null) ? ParentNode?.ToString() + "/" + Name : "/" + Name;
        }

        public virtual List<Asset> GetAllAssets()
        {
            return new List<Asset>();
        }

        public bool TreeHasMatchingName(string search)
        {
            //Debug.WriteLine("Depth: "+Depth);
            //if (FullPath.Contains("Award", StringComparison.OrdinalIgnoreCase))
            // return false;

            if (string.IsNullOrWhiteSpace(search)) return true;
            if (IsLeaf)
            {
                var temp = this;
                bool found = FullPath.Contains(search, StringComparison.OrdinalIgnoreCase);
                //if (found)
                    //Debug.WriteLine($"Match Found:{FullPath} Contains:{search} {FullPath.Contains(search, StringComparison.OrdinalIgnoreCase)} at Depth:{Depth}".Replace(search, $"<Bold>{search}</Bold>"));
                return found;
            }

            if (this is FolderNode)
                foreach (TreeNode t in (this as FolderNode).Children)
                    if (t.TreeHasMatchingName(search))
                        return true;

            return false;

        }

        //public virtual void PropagateChecks(bool? check) { Checked = check; }
    }
    public class TreeTrunk
    {
        public IEnumerable<TreeNode>? FullTree { get; set; }

        public IEnumerable<FileNode>? Leaves { get; set; }

        //public FileNode? GetLeaf(Func<FileNode,bool> predicate) {  return Leaves.Where<FileNode>(predicate).Single(); }
    }

    public abstract class ObservableObject : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged Members

        /// <summary>
        /// Raised when a property on this object has a new value.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property that has a new value.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            this.VerifyPropertyName(propertyName);

            if (this.PropertyChanged != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                this.PropertyChanged(this, e);
            }
        }

        #endregion // INotifyPropertyChanged Members

        #region Debugging Aides

        /// <summary>
        /// Warns the developer if this object does not have
        /// a public property with the specified name. This
        /// method does not exist in a Release build.
        /// </summary>
        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        public virtual void VerifyPropertyName(string propertyName)
        {
            // Verify that the property name matches a real,
            // public, instance property on this object.
            if (TypeDescriptor.GetProperties(this)[propertyName] == null)
            {
                string msg = "Invalid property name: " + propertyName;

                if (this.ThrowOnInvalidPropertyName)
                    throw new Exception(msg);
                else
                    Debug.Fail(msg);
            }
        }

        /// <summary>
        /// Returns whether an exception is thrown, or if a Debug.Fail() is used
        /// when an invalid property name is passed to the VerifyPropertyName method.
        /// The default value is false, but subclasses used by unit tests might
        /// override this property's getter to return true.
        /// </summary>
        protected virtual bool ThrowOnInvalidPropertyName { get; private set; }

        #endregion // Debugging Aides
    }
}
