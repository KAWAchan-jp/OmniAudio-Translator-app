using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OmniAudioTranslator.Core.Capture;
using OmniAudioTranslator.Core.Localization;
using OmniAudioTranslator.Core.Settings;

namespace OmniAudioTranslator.App.Views;

public sealed class WindowPickerItem : INotifyPropertyChanged
{
    private bool _isHighlighted;

    public WindowPickerItem(CapturableWindow window)
    {
        Window = window;
        DisplayText = $"[{window.ProcessName}.exe]: {window.Title}";
    }

    public CapturableWindow Window { get; }

    public string DisplayText { get; }

    /// <summary>登録キーワードのいずれかに一致した場合に強調表示する。</summary>
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            if (_isHighlighted == value)
            {
                return;
            }

            _isHighlighted = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHighlighted)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // ListBoxItemのUI Automation名はDisplayMemberPathではなくToString()から取られるため、
    // スクリーンリーダー等の支援技術がタイトルを正しく読み上げられるようオーバーライドする。
    public override string ToString() => DisplayText;
}

public partial class WindowPickerDialog : Window
{
    private static readonly Brush KeywordChipBackground = new SolidColorBrush(Color.FromRgb(0xFF, 0xF1, 0xA8));
    private static readonly Brush KeywordChipBorder = new SolidColorBrush(Color.FromRgb(0xE0, 0xD0, 0x80));

    private readonly SettingsStore _settingsStore;
    private List<string> _keywords;

    public WindowPickerDialog(SettingsStore? settingsStore = null)
    {
        _settingsStore = settingsStore ?? new SettingsStore();
        _keywords = _settingsStore.LoadWindowKeywords().ToList();

        InitializeComponent();
        ApplyLocalizedText();
        LoadWindows();

        Strings.LanguageChanged += ApplyLocalizedText;
        Closed += (_, _) => Strings.LanguageChanged -= ApplyLocalizedText;
    }

    public CapturableWindow? SelectedWindow { get; private set; }

    private void ApplyLocalizedText()
    {
        Title = Strings.Get("window_picker.title");
        DescriptionText.Text = Strings.Get("window_picker.description");
        KeywordGroup.Header = Strings.Get("window_picker.keyword_group");
        KeywordLabel.Text = Strings.Get("window_picker.keyword_input_label");
        AddKeywordButton.Content = Strings.Get("window_picker.keyword_add");
        KeywordHintText.Text = Strings.Get("window_picker.keyword_hint");
        RefreshButton.Content = Strings.Get("window_picker.refresh");
        SelectButton.Content = Strings.Get("window_picker.select");
        CancelButton.Content = Strings.Get("window_picker.cancel");
        RenderKeywords();
    }

    private void LoadWindows()
    {
        WindowListBox.ItemsSource = WindowCatalog.GetCapturableWindows()
            .Select(w => new WindowPickerItem(w))
            .ToList();
        ApplyKeywordHighlights();
    }

    private void ApplyKeywordHighlights()
    {
        foreach (WindowPickerItem item in WindowListBox.Items)
        {
            item.IsHighlighted = WindowKeywordMatcher.Matches(item.DisplayText, _keywords);
        }
    }

    private void RenderKeywords()
    {
        KeywordItemsControl.Items.Clear();

        foreach (string keyword in _keywords)
        {
            KeywordItemsControl.Items.Add(CreateKeywordChip(keyword));
        }
    }

    private UIElement CreateKeywordChip(string keyword)
    {
        var removeButton = new Button
        {
            Content = "×",
            Width = 18,
            Height = 18,
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Focusable = false,
            Tag = keyword,
            ToolTip = Strings.Get("window_picker.keyword_remove"),
        };
        removeButton.Click += RemoveKeywordButton_Click;

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(6, 1, 2, 1),
        };
        panel.Children.Add(new TextBlock
        {
            Text = keyword,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.Black,
        });
        panel.Children.Add(removeButton);

        return new Border
        {
            Background = KeywordChipBackground,
            BorderBrush = KeywordChipBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(0, 0, 6, 6),
            Child = panel,
        };
    }

    private void AddKeyword()
    {
        IReadOnlyList<string> normalized = WindowKeywordMatcher.Normalize(_keywords.Append(KeywordTextBox.Text));
        if (normalized.Count == _keywords.Count)
        {
            // 空文字・重複は追加しない(入力内容は残し、利用者が直せるようにする)。
            return;
        }

        _keywords = normalized.ToList();
        KeywordTextBox.Clear();
        SaveKeywords();
        RenderKeywords();
        ApplyKeywordHighlights();
    }

    private void RemoveKeyword(string keyword)
    {
        _keywords = WindowKeywordMatcher
            .Normalize(_keywords.Where(existing => !string.Equals(existing, keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        SaveKeywords();
        RenderKeywords();
        ApplyKeywordHighlights();
    }

    private void SaveKeywords()
    {
        try
        {
            _settingsStore.SaveWindowKeywords(_keywords);
            KeywordHintText.Text = Strings.Get("window_picker.keyword_hint");
        }
        catch (IOException ex)
        {
            KeywordHintText.Text = Strings.Format("window_picker.keyword_save_failed", ex.Message);
        }
    }

    private void KeywordTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        // Enterは既定ボタン(選択)に吸われてダイアログを閉じてしまうため、キーワード追加として処理する。
        if (e.Key == Key.Enter)
        {
            AddKeyword();
            e.Handled = true;
        }
    }

    private void AddKeywordButton_Click(object sender, RoutedEventArgs e) => AddKeyword();

    private void RemoveKeywordButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string keyword })
        {
            RemoveKeyword(keyword);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadWindows();

    private void OkButton_Click(object sender, RoutedEventArgs e) => Accept();

    private void WindowListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Accept();

    private void Accept()
    {
        if (WindowListBox.SelectedItem is WindowPickerItem item)
        {
            SelectedWindow = item.Window;
            DialogResult = true;
        }
    }
}
