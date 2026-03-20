using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace SABepInExManager.ViewModels;

public class BackupPageViewModel : ViewModelBase
{
    private readonly HomePageViewModel _homePage;
    private bool _hasSnapshots;

    public BackupPageViewModel(HomePageViewModel homePage)
    {
        HomePage = homePage;
        _homePage = homePage;

        RefreshSnapshotsCommand = new RelayCommand(RefreshSnapshots);

        HomePage.Logs.CollectionChanged += (_, _) => RefreshSnapshots();
        HomePage.PropertyChanged += OnHomePagePropertyChanged;
        RefreshSnapshots();
    }

    public HomePageViewModel HomePage { get; }
    public ObservableCollection<BackupSnapshotItemViewModel> Snapshots { get; } = new();
    public bool HasSnapshots
    {
        get => _hasSnapshots;
        private set
        {
            if (!SetProperty(ref _hasSnapshots, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public bool ShowEmptyState => !HasSnapshots;

    public IRelayCommand RefreshSnapshotsCommand { get; }

    public async Task RestoreSnapshotAsync(string folderName)
    {
        await _homePage.RestoreBaselineSnapshotAsync(folderName);
        RefreshSnapshots();
    }

    public async Task DeleteSnapshotAsync(string folderName)
    {
        await _homePage.DeleteBaselineSnapshotAsync(folderName);
        RefreshSnapshots();
    }

    private void OnHomePagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(HomePageViewModel.GameRootPath), StringComparison.Ordinal))
        {
            RefreshSnapshots();
        }
    }

    public void RefreshSnapshots()
    {
        Snapshots.Clear();
        HasSnapshots = false;

        if (string.IsNullOrWhiteSpace(HomePage.GameRootPath) || !Directory.Exists(HomePage.GameRootPath))
        {
            return;
        }

        var baselineRoot = Path.Combine(HomePage.GameRootPath, Services.PathConstants.StateRootFolder, Services.PathConstants.BaselineFolder);
        if (!Directory.Exists(baselineRoot))
        {
            return;
        }

        var snapshotDirs = Directory
            .GetDirectories(baselineRoot)
            .Select(path => new
            {
                Path = path,
                Name = Path.GetFileName(path),
                IsTimestamp = long.TryParse(Path.GetFileName(path), out _),
            })
            .Where(x => x.IsTimestamp)
            .OrderByDescending(x => x.Name)
            .ToList();

        foreach (var snapshotDir in snapshotDirs)
        {
            var folderName = snapshotDir.Name;
            var createdAt = ParseUnixTimestamp(folderName);
            var (fileCount, totalBytes) = CalculateDirectoryStats(snapshotDir.Path);

            Snapshots.Add(new BackupSnapshotItemViewModel
            {
                FolderName = folderName,
                CreatedAtText = createdAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "未知",
                FileCount = fileCount,
                SizeText = FormatSize(totalBytes),
            });
        }

        HasSnapshots = Snapshots.Count > 0;
    }

    private static DateTimeOffset? ParseUnixTimestamp(string folderName)
    {
        if (!long.TryParse(folderName, out var unixSeconds))
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch
        {
            return null;
        }
    }

    private static (int FileCount, long TotalBytes) CalculateDirectoryStats(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return (0, 0);
        }

        var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        long totalBytes = 0;
        foreach (var file in files)
        {
            totalBytes += new FileInfo(file).Length;
        }

        return (files.Length, totalBytes);
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }
}

