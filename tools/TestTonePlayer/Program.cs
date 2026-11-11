using System.Media;

namespace TestTonePlayer;

/// <summary>
/// OmniAudio Translatorの動作検証用に、指定したWAVファイルをループ再生し続ける自前ウィンドウ。
/// PowerShellのアドホックスクリプトより安定して動作する(Dispatcher.Run系の不安定さを避けるためWinForms採用)。
/// 音声もウィンドウも同一プロセスが直接所有するため、プロセスツリーに依存せずキャプチャ対象にできる。
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 0 || !File.Exists(args[0]))
        {
            MessageBox.Show("使い方: TestTonePlayer <wavファイルパス>");
            return;
        }

        string wavPath = args[0];

        ApplicationConfiguration.Initialize();

        var form = new Form
        {
            Text = "OmniAudioTestSource",
            Width = 320,
            Height = 120,
            TopMost = true,
        };
        var label = new Label
        {
            Text = "テスト音声を再生中...",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        form.Controls.Add(label);

        var cts = new CancellationTokenSource();
        var playThread = new Thread(() => PlayLoop(wavPath, cts.Token)) { IsBackground = true };
        playThread.Start();

        form.FormClosed += (_, _) => cts.Cancel();

        Application.Run(form);
    }

    private static void PlayLoop(string wavPath, CancellationToken cancellationToken)
    {
        using var player = new SoundPlayer(wavPath);
        while (!cancellationToken.IsCancellationRequested)
        {
            player.PlaySync();
            Thread.Sleep(300);
        }
    }
}
