using System.Diagnostics;
using System.Text;
using OmniAudioTranslator.Core.Capture;

namespace LoopbackSpike;

/// <summary>
/// Phase 1検証用の使い捨てコンソールツール。指定PIDが再生する音声だけをキャプチャし、WAVファイルへダンプする。
/// 詳細: docs/feature-process-loopback-capture.md
/// </summary>
internal static class Program
{
    [MTAThread]
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash("UnhandledException", e.ExceptionObject as Exception, e.ExceptionObject?.ToString());

        try
        {
            return await RunAsync(args);
        }
        catch (Exception ex)
        {
            LogCrash("Main", ex, null);
            return 1;
        }
    }

    private static void LogCrash(string source, Exception? ex, string? raw)
    {
        string text = $"[{DateTime.Now:O}] source={source}\n{ex?.ToString() ?? raw ?? "(no detail)"}\n";
        try
        {
            Console.Error.WriteLine(text);
            Console.Error.Flush();
        }
        catch
        {
            // stderrが使えない場合は無視してファイルログのみ頼る
        }

        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), text);
        }
        catch
        {
            // ファイル書き込みにも失敗した場合はもう手立てがない
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out int processId))
        {
            PrintCandidateProcesses();
            Console.WriteLine();
            Console.WriteLine("使い方: LoopbackSpike <PID> [秒数=8] [出力WAVパス] [繰り返し回数=1]");
            return 1;
        }

        int durationSeconds = args.Length > 1 && int.TryParse(args[1], out int d) ? d : 8;
        string outputPath = args.Length > 2
            ? args[2]
            : Path.Combine(AppContext.BaseDirectory, $"loopback-{processId}-{DateTime.Now:yyyyMMdd-HHmmss}.wav");
        int repeat = args.Length > 3 && int.TryParse(args[3], out int r) && r > 0 ? r : 1;

        Process targetProcess;
        try
        {
            targetProcess = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            Console.WriteLine($"PID {processId} のプロセスが見つかりません。");
            return 1;
        }

        Console.WriteLine($"対象プロセス: PID={processId} ({targetProcess.ProcessName})");
        Console.WriteLine($"出力先: {outputPath}");
        Console.WriteLine($"1回あたり{durationSeconds}秒の開始・停止を{repeat}回繰り返します...");

        // 停止→再開始でIAudioCaptureClientがE_NOINTERFACEになる事象の再現試験。
        // アプリ本体と同じく、試行ごとに新しいProcessLoopbackCaptureを生成する。
        int failures = 0;
        for (int iteration = 1; iteration <= repeat; iteration++)
        {
            string iterationPath = repeat == 1
                ? outputPath
                : Path.Combine(Path.GetDirectoryName(outputPath)!, $"loopback-{processId}-run{iteration}.wav");

            Console.WriteLine($"--- 試行 {iteration}/{repeat} ---");

            try
            {
                long totalBytes = await CaptureOnceAsync(processId, durationSeconds, iterationPath);
                if (totalBytes <= 0)
                {
                    failures++;
                    Console.WriteLine($"  失敗: 音声データを受信できませんでした -> {iterationPath}");
                }
                else
                {
                    Console.WriteLine($"  成功: {totalBytes:N0} バイト -> {iterationPath}");
                }
            }
            catch (Exception ex)
            {
                failures++;
                LogCrash($"iteration {iteration}", ex, null);
                Console.WriteLine($"  失敗: 試行 {iteration} で例外 -> {ex.Message}");
            }
        }

        Console.WriteLine(failures == 0
            ? $"完了: {repeat}回すべて成功"
            : $"完了: {repeat}回中{failures}回失敗");
        return failures == 0 ? 0 : 2;
    }

    private static async Task<long> CaptureOnceAsync(int processId, int durationSeconds, string outputPath)
    {
        using var capture = new ProcessLoopbackCapture();
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fileStream);

        WriteWavHeaderPlaceholder(writer, capture.SampleRate, capture.Channels, capture.BitsPerSample);

        long totalBytes = 0;
        var syncRoot = new object();
        capture.DataAvailable += (buffer, count) =>
        {
            lock (syncRoot)
            {
                writer.Write(buffer, 0, count);
                totalBytes += count;
            }
        };

        Exception? captureFailure = null;
        capture.CaptureFailed += ex => captureFailure = ex;

        await capture.StartAsync(processId);

        for (int i = 0; i < durationSeconds; i++)
        {
            await Task.Delay(1000);

            if (captureFailure is not null)
            {
                throw new InvalidOperationException("キャプチャが失敗しました。", captureFailure);
            }

            Console.WriteLine($"  経過 {i + 1}/{durationSeconds}秒 (受信バイト数: {totalBytes:N0})");
        }

        capture.Stop();

        if (captureFailure is not null)
        {
            throw new InvalidOperationException("キャプチャが失敗しました。", captureFailure);
        }

        lock (syncRoot)
        {
            FixWavHeader(writer, fileStream, totalBytes);
        }

        return totalBytes;
    }


    private static void PrintCandidateProcesses()
    {
        Console.WriteLine("ウィンドウを持つプロセス一覧 (PID: プロセス名 - ウィンドウタイトル):");
        foreach (Process p in Process.GetProcesses())
        {
            string title;
            try
            {
                title = p.MainWindowTitle;
            }
            catch
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                Console.WriteLine($"  {p.Id}: {p.ProcessName} - {title}");
            }
        }
    }

    private static void WriteWavHeaderPlaceholder(BinaryWriter writer, int sampleRate, int channels, int bitsPerSample)
    {
        int blockAlign = channels * bitsPerSample / 8;
        int byteRate = sampleRate * blockAlign;

        writer.Write("RIFF"u8);
        writer.Write(0); // ChunkSizeは後で書き戻す
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1); // WAVE_FORMAT_PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);
        writer.Write("data"u8);
        writer.Write(0); // Subchunk2Sizeは後で書き戻す
    }

    private static void FixWavHeader(BinaryWriter writer, FileStream fileStream, long dataSize)
    {
        writer.Flush();
        fileStream.Seek(4, SeekOrigin.Begin);
        writer.Write((int)(36 + dataSize));
        fileStream.Seek(40, SeekOrigin.Begin);
        writer.Write((int)dataSize);
        writer.Flush();
    }
}
