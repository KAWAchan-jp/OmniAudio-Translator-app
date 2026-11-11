using System.Runtime.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.CoreAudio;
using static Vanara.PInvoke.Ole32;

namespace OmniAudioTranslator.Core.Interop;

/// <summary>
/// AUDIOCLIENT_ACTIVATION_PARAMS(+union内のAUDIOCLIENT_PROCESS_LOOPBACK_PARAMS)と同一メモリレイアウトの構造体。
/// Vanara.PInvoke.CoreAudioのAUDIOCLIENT_ACTIVATION_PARAMSはProcessLoopbackParamsフィールドがprivateで
/// 外部から値を設定できないため(Vanara側の制約)、代わりにこの構造体をBLOBとしてPROPVARIANTへ詰めて渡す。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ActivationParamsBlob
{
    public AUDIOCLIENT_ACTIVATION_TYPE ActivationType;
    public uint TargetProcessId;
    public PROCESS_LOOPBACK_MODE ProcessLoopbackMode;
}

internal static class ProcessLoopbackDevice
{
    // mmdeviceapi.h: #define VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK L"VAD\\Process_Loopback"
    public const string VirtualAudioDeviceProcessLoopback = @"VAD\Process_Loopback";
}

/// <summary>
/// ActivateAudioInterfaceAsyncの完了コールバック。Windowsは別スレッド(MTA)から呼び出すため、
/// デッドロック回避のためIAgileObjectも実装する(Microsoft公式サンプルの注記に基づく)。
/// </summary>
internal sealed class ActivateAudioInterfaceCompletionHandler
    : IActivateAudioInterfaceCompletionHandler
{
    private readonly TaskCompletionSource<IActivateAudioInterfaceAsyncOperation> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<IActivateAudioInterfaceAsyncOperation> Completion => _tcs.Task;

    public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation)
        => _tcs.TrySetResult(activateOperation);
}

/// <summary>
/// Vanara.PInvoke.CoreAudio.ActivateAudioInterfaceAsync()は、completionHandler引数を
/// [MarshalAs(UnmanagedType.IUnknown)]でインターフェース型として自動マーシャリングしようとすると
/// .NET 8のこの環境で"Cannot marshal 'parameter #4': Invalid managed/unmanaged type combination
/// (Interfaces must be paired with Interface)"という例外、または(タイミングによっては)ヒープ破損による
/// 即死クラッシュを起こすことを実機検証で確認した(docs/feature-process-loopback-capture.md参照)。
/// そのため、この関数だけは独自にDllImportし、completionHandler/activationOperationをIntPtrとして扱い、
/// Marshal.GetComInterfaceForObject/GetObjectForIUnknownで手動変換することで自動マーシャリングを回避する。
/// </summary>
internal static class ActivateAudioInterfaceAsyncNative
{
    [DllImport("mmdevapi.dll", ExactSpelling = true)]
    private static extern int ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
        in Guid riid,
        PROPVARIANT? activationParams,
        IntPtr completionHandler,
        out IntPtr activationOperation);

    public static HRESULT Activate(
        string deviceInterfacePath,
        Guid riid,
        PROPVARIANT? activationParams,
        IActivateAudioInterfaceCompletionHandler completionHandler,
        out IActivateAudioInterfaceAsyncOperation? activationOperation)
    {
        IntPtr handlerPtr = Marshal.GetComInterfaceForObject(
            completionHandler, typeof(IActivateAudioInterfaceCompletionHandler));
        try
        {
            int hr = ActivateAudioInterfaceAsync(deviceInterfacePath, riid, activationParams, handlerPtr, out IntPtr opPtr);

            if (hr < 0 || opPtr == IntPtr.Zero)
            {
                activationOperation = null;
            }
            else
            {
                try
                {
                    activationOperation = (IActivateAudioInterfaceAsyncOperation)Marshal.GetObjectForIUnknown(opPtr);
                }
                finally
                {
                    Marshal.Release(opPtr);
                }
            }

            return new HRESULT(hr);
        }
        finally
        {
            Marshal.Release(handlerPtr);
        }
    }
}
