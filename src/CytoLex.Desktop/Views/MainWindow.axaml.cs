using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CytoLex.Core.Text;
using CytoLex.Desktop.Highlighting;
using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Views;

/// <summary>
/// The single-string check window (T-036). The AvaloniaEdit text is not bindable, so this code-behind syncs it with
/// <see cref="MainWindowViewModel.Input"/>, hands the highlights of the selected edition tab (T-037) to the editor's
/// renderers, and selects <see cref="MainWindowViewModel.SelectedSpan"/> (a picked diagnostic, card or part) in the editor.
/// The Batch tab (T-038) uses it only for the file pickers and the row double-click; the work is in <see cref="BatchViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private readonly DiagnosticColorizer _colorizer = new();
    private readonly InsertionElementGenerator _inserts = new();
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Input.TextArea.TextView.LineTransformers.Add(_colorizer);
        Input.TextArea.TextView.ElementGenerators.Add(_inserts);
        Input.TextChanged += OnEditorTextChanged;

        // One-line input: Enter does nothing (tunnel, before the editor's own key handling).
        Input.AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            SyncEditorText();
            ApplyHighlights();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.Input):
                SyncEditorText();
                break;
            case nameof(MainWindowViewModel.Highlights):
                ApplyHighlights();
                break;
            case nameof(MainWindowViewModel.SelectedSpan):
                if (_viewModel?.SelectedSpan is { } span)
                {
                    SelectSpan(span);
                }

                break;
            case nameof(MainWindowViewModel.HoverSpan):
                _colorizer.HoverSpan = _viewModel?.HoverSpan;
                Input.TextArea.TextView.Redraw();
                break;
            default:
                break;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var text = Input.Text;
        if (text.Contains('\r', StringComparison.Ordinal) || text.Contains('\n', StringComparison.Ordinal))
        {
            // Pasted line breaks become spaces (same length, so the caret stays put).
            var caret = Input.CaretOffset;
            Input.Text = text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');
            Input.CaretOffset = Math.Min(caret, Input.Text.Length);
            return;
        }

        _viewModel.Input = text;
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            e.Handled = true;
        }
    }

    private void SyncEditorText()
    {
        if (_viewModel is not null && Input.Text != _viewModel.Input)
        {
            Input.Text = _viewModel.Input;
            Input.CaretOffset = Input.Text.Length;
        }
    }

    private void ApplyHighlights()
    {
        var highlights = _viewModel?.Highlights ?? [];
        _colorizer.Highlights = highlights;
        _inserts.Highlights = highlights;
        Input.TextArea.TextView.Redraw();
    }

    private void SelectSpan(TextSpan span)
    {
        var length = Input.Text.Length;
        var start = Math.Clamp(span.Start, 0, length);
        var end = Math.Clamp(span.End, start, length);
        Input.Select(start, end - start); // also moves the caret (to the insertion point for an empty span)
    }

    // Abnormalities panel (T-069): clicking a card or a part selects its span; hovering a card marks it in the editor.
    private void OnSpanItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: ISpanItem item })
        {
            _viewModel?.SelectSpan(item.Span);
            e.Handled = true; // a part inside a card selects only the part
        }
    }

    private void OnCardPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_viewModel is not null && sender is Control { DataContext: AbnormalityItemViewModel item })
        {
            _viewModel.HoverSpan = item.Span;
        }
    }

    private void OnCardPointerExited(object? sender, PointerEventArgs e)
    {
        if (_viewModel is not null && sender is Control { DataContext: AbnormalityItemViewModel item } && _viewModel.HoverSpan == item.Span)
        {
            _viewModel.HoverSpan = null;
        }
    }
    // Batch tab (T-038): file pickers hand local paths to the view model.
    private static readonly FilePickerFileType _batchInput = new("Batch input (*.txt, *.csv, *.tsv)") { Patterns = ["*.txt", "*.csv", "*.tsv"] };
    private static readonly FilePickerFileType _csv = new("CSV summary (*.csv)") { Patterns = ["*.csv"] };
    private static readonly FilePickerFileType _json = new("JSON, full reports (*.json)") { Patterns = ["*.json"] };

    private async void OnOpenBatchFile(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a batch of ISCN strings",
            AllowMultiple = false,
            FileTypeFilter = [_batchInput, FilePickerFileTypes.All],
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            await _viewModel.Batch.LoadAsync(path);
        }
    }

    private void OnExportBatchCsv(object? sender, RoutedEventArgs e) => ExportBatch(_csv, "csv");

    private void OnExportBatchJson(object? sender, RoutedEventArgs e) => ExportBatch(_json, "json");

    private async void ExportBatch(FilePickerFileType type, string extension)
    {
        if (_viewModel is null)
        {
            return;
        }

        var name = Path.GetFileNameWithoutExtension(_viewModel.Batch.FilePath) is { Length: > 0 } stem ? stem + "-cytolex" : "cytolex-batch";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Export the batch result ({extension.ToUpperInvariant()})",
            SuggestedFileName = $"{name}.{extension}",
            DefaultExtension = extension,
            FileTypeChoices = [type],
            ShowOverwritePrompt = true,
        });
        if (file?.TryGetLocalPath() is { } path)
        {
            await _viewModel.Batch.ExportAsync(path);
        }
    }

    private void OnBatchRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel?.Batch is { SelectedRow: { } row } batch)
        {
            batch.Open(row);
        }
    }
}
