using System.Security.Cryptography;
using System.Text;

namespace OmniAudioTranslator.Core.Settings;

/// <summary>
/// APIキー等の機密文字列を、設定ファイルへ平文で残さないよう保護する。
/// WindowsではDPAPI(<see cref="ProtectedData"/>, <see cref="DataProtectionScope.CurrentUser"/>)を使うため、
/// 保護された値は同じWindowsユーザーでしか復号できない。
/// </summary>
/// <remarks>
/// DPAPIはWindows専用APIで、Linux上では<see cref="PlatformNotSupportedException"/>になる。
/// 本アプリはWindows専用だが、Core層はLinuxのDev Containerで<c>dotnet test</c>を回せる必要があるため、
/// 非Windowsでは保護せずそのまま往復させる(この経路は開発時のテストでしか通らない)。
/// </remarks>
public static class SecretProtector
{
    // 平文と区別し、保護済みの値だけを復号対象にするための目印。
    private const string ProtectedPrefix = "DPAPI:";

    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        if (!OperatingSystem.IsWindows())
        {
            return plainText;
        }

        byte[] encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plainText),
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return ProtectedPrefix + Convert.ToBase64String(encrypted);
    }

    public static string Unprotect(string storedValue)
    {
        if (string.IsNullOrEmpty(storedValue))
        {
            return string.Empty;
        }

        if (!storedValue.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            // 保護前に保存された値や、非Windowsで保存された値はそのまま返す。
            return storedValue;
        }

        if (!OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        try
        {
            byte[] decrypted = ProtectedData.Unprotect(
                Convert.FromBase64String(storedValue[ProtectedPrefix.Length..]),
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // 別ユーザー・別マシンへ設定ファイルを持ち込んだ場合は復号できない。
            // 起動不能にはせず、未設定として扱う。
            return string.Empty;
        }
    }
}
