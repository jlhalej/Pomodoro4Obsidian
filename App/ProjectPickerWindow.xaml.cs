using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.Json;

namespace PomodoroForObsidian
{
    public partial class TagPickerWindow : Window
    {
        public string? SelectedTag { get; private set; }
        public bool CreateNew { get; private set; } = false;
        private List<string> _allTags;

        public TagPickerWindow()
        {
            InitializeComponent();
            // Load tags from VaultTagsList.json
            var tagFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VaultTagsList.json");
            if (System.IO.File.Exists(tagFile))
            {
                try
                {
                    var json = System.IO.File.ReadAllText(tagFile);
                    _allTags = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
                catch
                {
                    _allTags = new List<string>();
                }
            }
            else
            {
                _allTags = new List<string>();
            }
            TagList.ItemsSource = _allTags;
            SearchBox.TextChanged += SearchBox_TextChanged;
            TagList.MouseDoubleClick += TagList_MouseDoubleClick;
            TagList.KeyDown += TagList_KeyDown;
            SearchBox.KeyDown += SearchBox_KeyDown;
            SearchBox.PreviewKeyDown += SearchBox_PreviewKeyDown;
            SearchBox.Focus();
            UpdateList();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateList();
        }

        private void UpdateList()
        {
            string filter = SearchBox.Text.Trim().ToLower();
            var filtered = _allTags.Where(t => t.ToLower().Contains(filter)).ToList();
            TagList.ItemsSource = filtered;
            if (filtered.Count > 0)
            {
                TagList.SelectedIndex = 0;
                CreateHint.Visibility = Visibility.Collapsed;
            }
            else
            {
                CreateHint.Visibility = Visibility.Visible;
            }
        }

        private void TagList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TagList.SelectedItem != null)
            {
                SelectedTag = TagList.SelectedItem.ToString();
                DialogResult = true;
            }
        }

        private void TagList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && TagList.SelectedItem != null)
            {
                SelectedTag = TagList.SelectedItem.ToString();
                DialogResult = true;
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && TagList.Items.Count > 0)
            {
                SelectedTag = TagList.Items[0].ToString();
                DialogResult = true;
            }
            else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                // Create new tag
                SelectedTag = SearchBox.Text.Trim();
                CreateNew = true;
                DialogResult = true;
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Down || e.Key == Key.Up) && TagList.Items.Count > 0)
            {
                TagList.Focus();
                if (TagList.SelectedIndex < 0)
                    TagList.SelectedIndex = 0;
                var item = TagList.ItemContainerGenerator.ContainerFromIndex(TagList.SelectedIndex) as ListBoxItem;
                item?.Focus();
                e.Handled = true;
            }
        }
    }
}
