using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CytoLex.Core.Text;
using CytoLex.Desktop.Editing;
using CytoLex.Desktop.Highlighting;
using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Views;

/// <summary>
/// The single-string check window (T-036). The AvaloniaEdit text is not bindable, so this code-behind syncs it with
/// <see cref="MainWindowViewModel.Input"/>, hands the highlights of the selected edition tab (T-037) to the editor's
/// renderers, and selects <see cref="MainWindowViewModel.SelectedSpan"/> (a picked diagnostic, card or part) in the editor.
/// The Batch tab (T-038) uses it only for the file pickers and the row double-click; the work is in <see cref="BatchViewModel"/>.
/// The Rules tab (T-039) is bound to <see cref="RuleBrowserViewModel"/>; a diagnostic's rule-ID link opens it.
/// The translation (T-059): the mouse over the editor text sets <see cref="MainWindowViewModel.HoverOffset"/>; hovering a part
/// card marks it and its span.
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

        // Editor → translation part under the mouse (T-059).
        Input.TextArea.TextView.PointerMoved += OnEditorPointerMoved;
        Input.TextArea.TextView.PointerExited += OnEditorPointerExited;
    }

    /// <summary>Stops a running CytoGPS check (T-081) with the window.</summary>
    protected override void OnClosed(EventArgs e)
    {
        _viewModel?.CytoGps.Cancel();
        base.OnClosed(e);
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
            case nameof(MainWindowViewModel.EditorToolTip):
                // T-097: autocorrection notes and findings at the mouse position.
                var textView = Input.TextArea.TextView;
                ToolTip.SetTip(textView, _viewModel?.EditorToolTip);
                ToolTip.SetIsOpen(textView, _viewModel?.EditorToolTip is not null);
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
            // Pasted line breaks become spaces. Not here: this runs inside the paste's open undo group, where changing the
            // document (or setting Text, which clears the undo stack) throws. Replace them once the paste is done.
            Dispatcher.UIThread.Post(ReplaceLineBreaks);
            return;
        }

        _viewModel.Input = text;
    }

    // Each line break ("\r\n", "\r" or "\n") becomes one space, as one undoable document change; the caret moves with the text.
    private void ReplaceLineBreaks()
    {
        var document = Input.Document;
        var breaks = new List<(int Offset, int Length)>();
        var text = document.Text;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                breaks.Add((i++, 2));
            }
            else if (text[i] is '\r' or '\n')
            {
                breaks.Add((i, 1));
            }
        }

        if (breaks.Count == 0)
        {
            return;
        }

        using (document.RunUpdate())
        {
            for (var k = breaks.Count - 1; k >= 0; k--)
            {
                document.Replace(breaks[k].Offset, breaks[k].Length, " ");
            }
        }
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            e.Handled = true;
        }
        else if (e.Key == Key.U && e.KeyModifiers == KeyModifiers.Control)
        {
            ToggleUnderline();
            e.Handled = true;
        }
    }

    private void OnUnderlineClick(object? sender, RoutedEventArgs e) => ToggleUnderline();

    private void OnCutClick(object? sender, RoutedEventArgs e) => Input.Cut();

    private void OnCopyClick(object? sender, RoutedEventArgs e) => Input.Copy();

    private void OnPasteClick(object? sender, RoutedEventArgs e) => Input.Paste();

    /// <summary>
    /// Adds or removes U+0332 on the selection or the number at the caret (T-042), as one document replace so Ctrl+Z
    /// undoes it; the changed text stays selected.
    /// </summary>
    private void ToggleUnderline()
    {
        if (Underlining.Toggle(Input.Text ?? "", Input.SelectionStart, Input.SelectionLength) is not { } edit)
        {
            return;
        }

        Input.Document.Replace(edit.Start, edit.Length, edit.NewText);
        Input.Select(edit.Start, edit.NewText.Length);
        Input.TextArea.Focus();
    }

    private void SyncEditorText()
    {
        if (_viewModel is not null && Input.Text != _viewModel.Input)
        {
            // A document replace instead of setting Text keeps the undo stack: Ctrl+Z undoes an applied fix (T-040).
            Input.Document.Replace(0, Input.Document.TextLength, _viewModel.Input);
            Input.CaretOffset = Input.Document.TextLength;
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
        if (sender is Control { DataContext: ISpanItem item } && item is not TranslationItemViewModel { IsSummary: true })
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

    // Translation (T-059): hovering a part marks it and its span in the editor.
    private void OnTranslationPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_viewModel is not null && sender is Control { DataContext: TranslationItemViewModel item })
        {
            _viewModel.HoverTranslationItem(item, hovered: true);
        }
    }

    private void OnTranslationPointerExited(object? sender, PointerEventArgs e)
    {
        if (_viewModel is not null && sender is Control { DataContext: TranslationItemViewModel item })
        {
            _viewModel.HoverTranslationItem(item, hovered: false);
        }
    }

    // Translation (T-059): the mouse over the editor text marks the translation part at that offset.
    private void OnEditorPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var textView = Input.TextArea.TextView;
        var position = textView.GetPosition(e.GetPosition(textView) + textView.ScrollOffset);
        int? offset = position is { } p ? Input.Document.GetOffset(p.Location) : null;
        if (offset is { } o && o >= Input.Document.TextLength)
        {
            offset = null; // past the end of the text
        }

        if (_viewModel.HoverOffset != offset)
        {
            _viewModel.HoverOffset = offset;
        }
    }

    private void OnEditorPointerExited(object? sender, PointerEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.HoverOffset = null;
        }
    }

    // Corrected line (T-097): copies the selected edition's corrected string.
    private async void OnCopyCorrected(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedPanel?.Corrected is { } corrected && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(corrected);
        }
    }

    // Diagnostics list (T-039): the rule ID of a KB rule opens it in the rule browser.
    private void OnOpenRuleClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is Control { DataContext: DiagnosticItemViewModel item })
        {
            _viewModel.OpenRule(item);
            e.Handled = true;
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
