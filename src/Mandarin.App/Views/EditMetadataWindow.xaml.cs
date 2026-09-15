using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Mandarin.Core.AdvancedTools;
using Mandarin.Core.AdvancedTools.Images;
using Button = System.Windows.Controls.Button;

namespace Mandarin.App.Views;

public partial class EditMetadataWindow : Window
{
    private readonly string _sourcePath;
    private readonly List<MetadataTag> _allTags = new();
    private readonly List<string> _removedTagNames = new();

    public MetadataEditOptions? Result { get; private set; }

    public EditMetadataWindow(string sourcePath)
    {
        InitializeComponent();
        DialogChrome.Apply(this, HeaderBar, CardRoot);
        OutputPreview.Show(OutputPreviewText, sourcePath, AdvancedToolKind.EditMetadata);
        _sourcePath = sourcePath;
        FileNameText.Text = Path.GetFileName(sourcePath);

        var tags = ImageMetadataTool.ReadTags(sourcePath);
        _allTags.AddRange(tags);

        UpdateTagList();
    }

    private void UpdateTagList()
    {
        string query = SearchBox.Text?.Trim() ?? "";
        var filtered = _allTags
            .Where(t => !_removedTagNames.Contains(t.RawKey))
            .Where(t => string.IsNullOrEmpty(query) ||
                        t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        t.Value.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        t.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        TagsItemsControl.ItemsSource = filtered;
        EmptyStateText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateTagList();
    }

    private void RemoveTagButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is MetadataTag tag)
        {
            _removedTagNames.Add(tag.RawKey);
            UpdateTagList();
        }
    }

    private void RemoveAllButton_Click(object sender, RoutedEventArgs e)
    {
        Result = new MetadataEditOptions(RemoveAll: true);
        DialogResult = true;
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Result = new MetadataEditOptions(RemoveAll: false, RemovedTagNames: _removedTagNames);
        DialogResult = true;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
