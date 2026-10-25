using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using WslcDesktop.Formatting;

namespace WslcDesktop.Tests;

/// <summary>
///     Keeps the logic layer usable by a UI other than WinUI, and free of the processes, files and registry its adapters own.
/// </summary>
public sealed class LayeringTests
{
    private static readonly Assembly Core = typeof(LogParser).Assembly;

    private static readonly string[] References = [.. Core.GetReferencedAssemblies().Select(name => name.Name!)];

    private static readonly string[] AdapterTypes =
    [
        "System.Diagnostics.Process",
        "System.Diagnostics.ProcessStartInfo",
        "System.IO.File",
        "System.IO.FileInfo",
        "System.IO.FileStream",
        "System.IO.Directory",
        "System.IO.DirectoryInfo",
        "Microsoft.Win32.Registry",
        "Microsoft.Win32.RegistryKey"
    ];

    [Theory]
    [InlineData("Microsoft.UI")]
    [InlineData("Microsoft.WindowsAppSDK")]
    [InlineData("WinRT")]
    [InlineData("CommunityToolkit")]
    [InlineData("WslcDesktop.Infrastructure")]
    [InlineData("WslcDesktop.Interop")]
    public void Core_ReferencesNoUiOrAdapterAssembly(string prefix) =>
        Assert.DoesNotContain(References, name => name.StartsWith(prefix, StringComparison.Ordinal));

    [Fact]
    public void Core_UsesNoProcessFileOrRegistryTypes() =>
        Assert.Empty(ReferencedTypeNames().Intersect(AdapterTypes));

    private static HashSet<string> ReferencedTypeNames()
    {
        using FileStream stream = File.OpenRead(Core.Location);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();

        return
        [
            .. metadata.TypeReferences
                .Select(metadata.GetTypeReference)
                .Select(type => $"{metadata.GetString(type.Namespace)}.{metadata.GetString(type.Name)}")
        ];
    }
}
