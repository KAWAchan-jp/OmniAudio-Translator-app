using System.Windows;
using System.Windows.Input;
using OmniAudioTranslator.Core.Capture;
using OmniAudioTranslator.Core.Localization;

namespace OmniAudioTranslator.App.Views;

public sealed class WindowPickerItem(CapturableWindow window)
{
    public CapturableWindow Window { get; } = window;

    public string DisplayText { get; } = $"[{window.ProcessName}.exe]: {window.Title}";

    // ListBoxItemのUI Automation名はDisplayMemberPathではなくToString()から取られるため、
    // スクリーンリーダー等の支援技術がタイトルを正しく読み上げられるようオーバーライドする。
    public override string ToString() => DisplayText;
}

public partial class WindowPickerDialog : Window
{
    public WindowPickerDialog()
    {
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
        RefreshButton.Content = Strings.Get("window_picker.refresh");
        SelectButton.Content = Strings.Get("window_picker.select");
        CancelButton.Content = Strings.Get("window_picker.cancel");
    }

    private void LoadWindows()
    {
        WindowListBox.ItemsSource = WindowCatalog.GetCapturableWindows()
            .Select(w => new WindowPickerItem(w))
            .ToList();
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
