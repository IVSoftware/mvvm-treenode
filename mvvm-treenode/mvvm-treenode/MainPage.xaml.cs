// MIT License
// Copyright (c) 2021 IVSoftware, LLC and Thomas C. Gregor

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Telerik.XamarinForms.DataControls.TreeView;
using Xamarin.Forms;
using PropertyChangingEventArgs = Xamarin.Forms.PropertyChangingEventArgs;

namespace mvvm_treenode
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
            BindingContext.Tree.Expand = treeView.Expand;
            BindingContext.Tree.Collapse = treeView.Collapse;
            TreeNodeModel.PropertyChanging += TreeNodeModel_PropertyChanging;

            var testData = new string[]
            {
                @"Emerald\LevelA1\Level2\Leaf1",
                @"Emerald\LevelB1\Level2\Leaf1",
                @"Sapphire\Level1\Level2\Leaf1",
                @"Sapphire\Level1\Level2\Level3\Leaf1",
                @"Sapphire\Level1\Level2\Level3\Leaf2",
            };
            foreach (var path in testData)
            {
                BindingContext.Tree.Add(path);
            }
        }

        private void TreeNodeModel_PropertyChanging(object sender, PropertyChangingCancelEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(TreeNodeModel.IsViewNodeExpanded):
                    var node = (TreeNodeModel)sender;
                    if (node.Tree != null)
                    {
                        if (node.Tree.IsEventTest && !node.IsViewNodeExpanded)
                        {
                            e.PromptTask = App.Current.MainPage.DisplayAlert("Before Expand", $"Do you want to open {node.Text}?", "Yes", "No");
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        internal new MainPageViewModel BindingContext
        {
            get => (MainPageViewModel)base.BindingContext;
            set => base.BindingContext = value;
        }


        private void IndicatorPropertyChangedProxy(object sender, PropertyChangingEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(TreeViewDataItem.IsExpanded):
                    var meta = (TreeViewDataItem)((ExpandCollapseIndicator)sender).BindingContext;
                    var node = (TreeNodeModel)meta.Item;
                    node.IsViewNodeExpanded = meta.IsExpanded;
                    break;
                default:
                    break;
            }
        }
    }

    class MainPageViewModel
    {
        public TreeModel Tree { get; } = new TreeModel { ExpandOnAdd = false };
    }

    class TreeNodeCollection : ObservableCollection<TreeNodeModel> { }

    class TreeModel : TreeNodeCollection
    {
        public TreeModel()
        {
            TimedExpandAllCommand = new Command(OnTimedExpandAll);
            TimedCollapseAllCommand = new Command(OnTimedCollapseAll);
            TestEventCommand = new Command(OnTestEvent);
        }

        public bool ExpandOnAdd = false;
        internal Action<TreeNodeModel> Expand { get; set; }
        internal Action<TreeNodeModel> Collapse { get; set; }
        public void Add(string fullPath)
        {
            var node = new TreeNodeModel(fullPath);
            var parse = node.FullPath.Split('\\');

            TreeNodeCollection nodes = this;
            TreeNodeModel parent = null;

            List<string> builder = new List<string>();
            for (int level = 0; level < parse.Length; level++)
            {
                var text = parse[level];
                builder.Add(text);
                var path = string.Join(@"\", builder);

                var found = nodes.FirstOrDefault(match => match.Text == text);
                if (found == null)
                {
                    if (path == node.FullPath)
                    {
                        // Assign the Tree properties
                        node.Tree = this;
                        node.Parent = parent;
                        nodes.Add(node);
                        nodes = node.Nodes;
                        if (!ExpandOnAdd)
                        {
                            node.Parent?.Collapse();
                        }
                        return;
                    }
                    else
                    {
                        // Add a passing node using default or custom node factory
                        found = new TreeNodeModel(path);
                        found.Tree = this;
                        found.Parent = parent;
                        nodes.Add(found);
                        nodes = found.Nodes;
                        if(!ExpandOnAdd)
                        {
                            found.Parent?.Collapse();
                        }
                    }
                }
                parent = found;
                nodes = found.Nodes;
            }
        }
        internal IEnumerable<TreeNodeModel> GetDescendants()
        {
            var descendants = new List<TreeNodeModel>();
            foreach (var child in this)
            {
                child.GetDescendants(descendants);
            } 
            return descendants;
        }
        public ICommand TimedExpandAllCommand { get; }
        private async void OnTimedExpandAll(object o)
        {
            foreach (var node in GetDescendants())
            {
                node.IsViewNodeExpanded = true;
                await Task.Delay(400);
            }
        }
        public ICommand TimedCollapseAllCommand { get; }
        private async void OnTimedCollapseAll(object o)
        {
            var propertyInfo = 
                typeof(TreeNodeModel)
                .GetProperty(nameof(TreeNodeModel.IsViewNodeExpanded));

            foreach (var node in GetDescendants().Reverse())
            {
                node.IsViewNodeExpanded = false;
                await Task.Delay(400);
            }
        }
        public ICommand TestEventCommand { get; }
        private void OnTestEvent(object o)
        {
            bool b4 = IsEventTest;
            try
            {
                IsEventTest = true;
                foreach (var root in this)
                {
                    root.IsViewNodeExpanded = true;
                }
            }
            finally
            {
                IsEventTest = b4;
            }
        }
        public bool IsEventTest { get; set; }
    }

    class TreeNodeModel : INotifyPropertyChanged
    {
        public TreeNodeModel(string fullPath)
        {
            Text = fullPath.Split('\\').Last();
            FullPath = fullPath;
        }
        public TreeModel Tree { get; set; }
        public TreeNodeModel Parent { get; set; }
        public TreeNodeCollection Nodes { get; } = new TreeNodeCollection();

        #region M E T H O D S
        internal void Expand() => Tree?.Expand(this);
        internal void Collapse() => Tree?.Collapse(this);
        #endregion

        #region P R O P E R T I E S
        string _FullPath = string.Empty;
        public string FullPath
        {
            get => _FullPath;
            set => SetProperty(ref _FullPath, value);
        }
        string _Text = string.Empty;
        public string Text
        {
            get => _Text;
            set => SetProperty(ref _Text, value);
        }

        bool _isCheckBoxVisible = false;
        public bool IsCheckBoxVisible
        {
            get { return _isCheckBoxVisible; }
            set { SetProperty(ref _isCheckBoxVisible, value); }
        }

        bool _IsViewNodeExpanded = false;
        public bool IsViewNodeExpanded
        {
            get => _IsViewNodeExpanded;
            set
            {
                if( SetProperty(ref _IsViewNodeExpanded, value))
                {
                    if (_IsViewNodeExpanded) Expand();
                    else Collapse();
                }
            }
        }
        internal void GetDescendants(List<TreeNodeModel> descendants)
        {
            descendants.Add(this);
            foreach (var child in Nodes)
            {
                child.GetDescendants(descendants);
            }
        }
        #endregion

        #region INotifyPropertyChanged Implementer
        internal bool SetProperty<T>(ref T backingStore, T value,  [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;
            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        Queue<object> PropertySetterQueue = new Queue<object>();
        bool BeforeSetProperty<T>(FieldInfo backingField, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals((T)backingField.GetValue(this), value))
                return false;
            var e = new PropertyChangingCancelEventArgs(backingField, value, propertyName);
            OnPropertyChanging(e);
            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static event PropertyChangingCancelEventHandler PropertyChanging;
        protected virtual void OnPropertyChanging(PropertyChangingCancelEventArgs e)
        {
            PropertyChanging?.Invoke(this, e);
        }

        #endregion
    }

    delegate void PropertyChangingCancelEventHandler(object sender, PropertyChangingCancelEventArgs e);

    class PropertyChangingCancelEventArgs : PropertyChangingEventArgs
    {
        public PropertyChangingCancelEventArgs (FieldInfo backingField, object value, string propertyName) : base(propertyName)
        {
            BackingField = backingField;
            Value = value;
        }

        public FieldInfo BackingField { get; }
        object Value { get; }

        public bool Cancel { get; set; }
        public Task<bool> PromptTask { get; internal set; }
    }

    class LevelToIndentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (uint)value * 20;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}