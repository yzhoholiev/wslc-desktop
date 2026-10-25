using Windows.Win32;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace WslcDesktop.Interop;

/// <summary>
///     An entry in the jump list's Tasks section.
/// </summary>
public abstract unsafe record JumpListItem
{
    /// <summary>
    ///     Creates the shell link that shows this entry in the jump list; the caller releases it.
    /// </summary>
    internal abstract IShellLinkW* CreateLink();

    private protected static IShellLinkW* NewLink()
    {
        ShellLink.CreateInstance(out IShellLinkW* link).ThrowOnFailure();
        return link;
    }

    private protected static void UpdateProperties(IShellLinkW* link, Action<nint> update)
    {
        Guid iid = typeof(IPropertyStore).GUID;
        void* store;
        link->QueryInterface(&iid, &store).ThrowOnFailure();
        try
        {
            update((nint)store);
            ((IPropertyStore*)store)->Commit();
        }
        finally
        {
            ((IPropertyStore*)store)->Release();
        }
    }
}

/// <summary>
///     A jump list entry that starts a program with arguments.
/// </summary>
public sealed unsafe record JumpListTask(string Title, string ProgramPath, string Arguments, string IconPath) : JumpListItem
{
    internal override IShellLinkW* CreateLink()
    {
        IShellLinkW* link = NewLink();
        fixed (char* path = ProgramPath)
        {
            link->SetPath(path);
        }

        fixed (char* iconPath = IconPath)
        {
            link->SetIconLocation(iconPath, 0);
        }

        fixed (char* arguments = Arguments)
        {
            link->SetArguments(arguments);
        }

        UpdateProperties(link, store => ShellProperties.SetText(store, PInvoke.PKEY_Title, Title));
        return link;
    }
}

/// <summary>
///     A line between groups of jump list tasks.
/// </summary>
public sealed unsafe record JumpListSeparator : JumpListItem
{
    internal override IShellLinkW* CreateLink()
    {
        IShellLinkW* link = NewLink();
        UpdateProperties(link, store => ShellProperties.SetFlag(store, PInvoke.PKEY_AppUserModel_IsDestListSeparator));
        return link;
    }
}
