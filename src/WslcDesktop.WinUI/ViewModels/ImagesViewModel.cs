using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

public sealed partial class ImagesViewModel : ResourceListViewModel<ImageItem>
{
    private static readonly Dictionary<string, SortColumn<ImageItem>> Columns = new()
    {
        ["image"] = new SortColumn<ImageItem>(item => item.Reference),
        ["size"] = new SortColumn<ImageItem>(item => item.SizeBytes, true),
        ["created"] = new SortColumn<ImageItem>(item => item.CreatedAt, true),
        ["usage"] = new SortColumn<ImageItem>(item => item.ContainerCount, true)
    };

    public ImagesViewModel(AppServices services)
        : base(services, "images", "image", Columns)
    {
        PullReference = "";
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PullCommand))]
    public partial string PullReference { get; set; }

    protected override string ItemsName => "images";

    protected override EmptyText NoItems { get; } =
        new(EmptyReason.NoItems, Glyphs.Images, "No images yet", "Pull one with the box above, for example nginx:alpine.");

    public Task RemoveAsync(ImageItem item, bool force) =>
        RunAndReloadAsync($"Deleting {item.Reference}", () => Services.Commands.RemoveImageAsync(ImageTarget(item), force), $"Deleted {item.Reference}.");

    /// <summary>
    ///     Reads the image's defaults and the networks a new container can join.
    /// </summary>
    public async Task<RunImageDefaults?> LoadRunDefaultsAsync(ImageItem item)
    {
        RunImageDefaults? defaults = null;
        string image = ImageTarget(item);
        await RunOperationAsync($"Reading {item.Reference}", async () =>
        {
            ImageConfigDto config = await Services.Reader.GetImageConfigAsync(image, CancellationToken.None);
            List<NetworkDto> networks = await Services.Reader.ListNetworksAsync(CancellationToken.None);
            defaults = new RunImageDefaults(image, item.Reference, config, [.. networks.Select(network => network.Name)]);
        });

        return defaults;
    }

    public Task ReportCreatedAsync(RunImageDefaults defaults, bool started)
    {
        Services.Notifier.Success(started ? $"Started a container from {defaults.Reference}." : $"Created a container from {defaults.Reference}.");
        return ReloadAsync();
    }

    public Task PruneAsync(bool all) =>
        RunAndReloadAsync("Pruning images", () => Services.Commands.PruneImagesAsync(all), all ? "Removed all unused images." : "Removed dangling images.");

    protected override async Task<List<ImageItem>> FetchAsync()
    {
        List<ImageDto> images = await Services.Reader.ListImagesAsync(CancellationToken.None);
        long largest = Math.Max(1, images.Select(image => ByteSize.Parse(image.Size)).DefaultIfEmpty().Max());
        return [.. images.Select(image => ToItem(image, largest))];
    }

    private ImageItem ToItem(ImageDto image, long largestSize)
    {
        string reference = image.Tag is "" or "<none>" ? image.Repository : $"{image.Repository}:{image.Tag}";
        string shortId = image.Id.Replace("sha256:", "", StringComparison.Ordinal);
        long size = ByteSize.Parse(image.Size);
        // Matching names misses containers created from a digest, so it only stands in when wslc reports no count.
        int containerCount = int.TryParse(image.Containers, out int reported)
            ? reported
            : ResourceUsage.ContainersUsingImage(image.Id, reference, Services.Monitor.Latest.Containers).Length;

        return new ImageItem(
            image.Id,
            reference,
            image.Repository,
            image.Tag,
            Humanize.ShortId(shortId),
            ByteSize.Normalize(image.Size),
            size,
            image.CreatedSince,
            image.CreatedAt,
            containerCount)
        {
            SizeShare = (double)size / largestSize
        };
    }

    protected override int CountIn(Inventory inventory) => inventory.Images;

    protected override ContainerDto[] ContainersUsing(ImageItem item) =>
        ResourceUsage.ContainersUsingImage(item.Id, item.Reference, Services.Monitor.Latest.Containers);

    protected override string NameOf(ImageItem item) => item.Reference;

    protected override bool Matches(ImageItem item, string search) =>
        item.Reference.Contains(search, StringComparison.OrdinalIgnoreCase)
        || item.ShortId.StartsWith(search, StringComparison.OrdinalIgnoreCase);

    protected override string Summarize(IReadOnlyList<ImageItem> all)
    {
        var images = all.DistinctBy(item => item.Id).ToList();
        return $"{Humanize.Count(images.Count, "image")} · {ByteSize.Format(images.Sum(item => item.SizeBytes))} · {images.Count(item => item.IsUnused)} unused";
    }

    protected override string KeyOf(ImageItem item) => $"{item.Reference}@{item.ShortId}";

    protected override (string Title, string Subtitle) Describe(ImageItem item) =>
        (item.Reference, $"{item.ShortId} · {item.Size} · created {item.Created} · {Labels.UsageLabel(item.ContainerCount)}");

    protected override Task<string> InspectAsync(ImageItem item, CancellationToken ct) =>
        Services.Details.InspectImageAsync(ImageTarget(item), ct);

    [RelayCommand(CanExecute = nameof(CanPull))]
    private Task PullAsync()
    {
        string reference = PullReference.Trim();
        if (!ImageReference.IsValid(reference))
        {
            Services.Notifier.Error($"Cannot pull '{reference}'",
                "An image reference uses letters, digits and . _ - : / @, such as nginx:alpine or ghcr.io/owner/app:1.0.");

            return Task.CompletedTask;
        }

        return RunAndReloadAsync($"Pulling {reference}", async () =>
            {
                await Services.Commands.PullImageAsync(reference);
                PullReference = "";
            }, $"Pulled {reference}.");
    }

    private bool CanPull() => !string.IsNullOrWhiteSpace(PullReference);

    /// <summary>
    ///     The name wslc commands take for the image: its ID when it has no tag, since a bare repository name means repo:latest.
    /// </summary>
    internal static string ImageTarget(ImageItem item) => item.Repository == "<none>" || !item.HasTag ? item.ShortId : item.Reference;
}
