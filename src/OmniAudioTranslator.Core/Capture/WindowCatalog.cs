using System.Runtime.InteropServices;
using System.Text;

namespace OmniAudioTranslator.Core.Capture;

public sealed record CapturableWindow(int ProcessId, string ProcessName, string Title);

/// <summary>
/// キャプチャ対象として選択可能なウィンドウを列挙する。UIはウィンドウ単位([exe.exe]: タイトル形式)で
/// 選ばせるが、実際の音声キャプチャは<see cref="ProcessLoopbackCapture"/>によりプロセス単位で行われる
/// (同一exeが複数ウィンドウを持つ場合、選択ウィンドウ以外の音声も混ざりうるという既知の制約がある)。
/// </summary>
public static class WindowCatalog
{
    public static IReadOnlyList<CapturableWindow> GetCapturableWindows()
    {
        var results = new List<CapturableWindow>();

        NativeMethods.EnumWindows(
            (hWnd, _) =>
            {
                if (!NativeMethods.IsWindowVisible(hWnd))
                {
                    return true;
                }

                int length = NativeMethods.GetWindowTextLength(hWnd);
                if (length == 0)
                {
                    return true;
                }

                var builder = new StringBuilder(length + 1);
                NativeMethods.GetWindowText(hWnd, builder, builder.Capacity);
                string title = builder.ToString();
                if (string.IsNullOrWhiteSpace(title))
                {
                    return true;
                }

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0)
                {
                    return true;
                }

                try
                {
                    using var process = System.Diagnostics.Process.GetProcessById((int)pid);
                    results.Add(new CapturableWindow((int)pid, process.ProcessName, title));
                }
                catch (ArgumentException)
                {
                    // 列挙からプロセス取得までの間にプロセスが終了した場合は無視する
                }

                return true;
            },
            IntPtr.Zero);

        return results;
    }

    private static class NativeMethods
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}
