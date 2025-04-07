using System.Runtime.InteropServices;

namespace PubgOverlay.uiaccess;

public partial class UiAccessHelper
{
    // 动态加载嵌入资源中的DLL
    public static int PrepareForUiAccess()
    {
        var tempPath = ExtractEmbeddedDll("uiaccess.dll");
        var dllHandle = LoadLibrary(tempPath);

        if (dllHandle == IntPtr.Zero)
        {
            return Marshal.GetLastWin32Error();
        }

        var procAddress = GetProcAddress(dllHandle, "PrepareForUIAccess");
        if (procAddress == IntPtr.Zero)
        {
            FreeLibrary(dllHandle);
            return Marshal.GetLastWin32Error();
        }

        var prepareFunc = Marshal.GetDelegateForFunctionPointer<PrepareForUiAccessDelegate>(procAddress);
        return prepareFunc();
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int PrepareForUiAccessDelegate();

    [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr LoadLibrary(string dllToLoad);

    [LibraryImport("kernel32.dll", EntryPoint = "GetProcAddress", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr GetProcAddress(IntPtr hModule, string procedureName);

    [LibraryImport("kernel32.dll", EntryPoint = "FreeLibrary")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FreeLibrary(IntPtr hModule);

    public static string ExtractEmbeddedDll(string dllName)
    {
        // 由于没有显式定义Program类，我们需要获取当前程序集
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(dllName, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(resourceName))
        {
            throw new FileNotFoundException($"未找到嵌入资源: {dllName}");
        }

        var tempPath = Path.Combine(Path.GetTempPath(), dllName);

        using var resourceStream = assembly.GetManifestResourceStream(resourceName);
        using var fileStream = File.Create(tempPath);
        resourceStream?.CopyTo(fileStream);

        return tempPath;
    }
}