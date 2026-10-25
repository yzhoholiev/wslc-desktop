using System.Collections.ObjectModel;
using WinRT;
using WslcDesktop.Models;
using WslcDesktop.ViewModels;

// x:Bind hands ItemsSource collections to WinUI as object, so CsWinRT cannot discover these
// generic instantiations itself; without dynamic code, an undeclared one fails with E_INVALIDARG.
[assembly: GeneratedWinRTExposedExternalType(typeof(ObservableCollection<KeyValueEntry>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(ObservableCollection<ContainerUsage>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(ObservableCollection<ContainerItem>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(ObservableCollection<ImageItem>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(ObservableCollection<VolumeItem>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(ObservableCollection<NetworkItem>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(ObservableCollection<WslDistro>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(ObservableCollection<WslcSessionItem>))]
