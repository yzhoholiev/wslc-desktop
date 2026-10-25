using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;
using WslcDesktop.Services;

namespace WslcDesktop.Interop;

/// <summary>
///     The app's taskbar jump list, written through ICustomDestinationList because Windows.UI.StartScreen.JumpList requires package identity.
/// </summary>
public static unsafe class JumpList
{
    /// <summary>
    ///     Replaces the tasks in the jump list of the app with this app ID.
    /// </summary>
    public static void SetTasks(string appId, IEnumerable<JumpListItem> items)
    {
        ICustomDestinationList* list = null;
        IObjectCollection* collection = null;
        try
        {
            DestinationList.CreateInstance(out list).ThrowOnFailure();
            fixed (char* id = appId)
            {
                list->SetAppID(id);
            }

            uint minimumSlots;
            Guid removedIid = typeof(IObjectArray).GUID;
            void* removed;
            list->BeginList(&minimumSlots, &removedIid, &removed);
            ((IObjectArray*)removed)->Release();

            EnumerableObjectCollection.CreateInstance(out collection).ThrowOnFailure();
            foreach (JumpListItem item in items)
            {
                IShellLinkW* link = item.CreateLink();
                try
                {
                    collection->AddObject((IUnknown*)link);
                }
                finally
                {
                    link->Release();
                }
            }

            list->AddUserTasks((IObjectArray*)collection);
            list->CommitList();
        }
        catch (Exception ex)
        {
            // The jump list is a convenience; a shell that refuses it must not stop the app from starting.
            CrashLog.Write(ex);
        }
        finally
        {
            if (collection is not null)
            {
                collection->Release();
            }

            if (list is not null)
            {
                list->Release();
            }
        }
    }
}
