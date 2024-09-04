using System.Collections.ObjectModel;
using Avalonia.Controls;
using CrossPlatformDownloadManager.Data.Models;
using CrossPlatformDownloadManager.Data.UnitOfWork;
using CrossPlatformDownloadManager.Data.ViewModels;
using CrossPlatformDownloadManager.Data.ViewModels.CustomEventArgs;
using CrossPlatformDownloadManager.Utils;
using Downloader;
using PropertyChanged;
using DownloadStatus = CrossPlatformDownloadManager.Utils.Enums.DownloadStatus;

namespace CrossPlatformDownloadManager.Data.Services.DownloadFileService;

[AddINotifyPropertyChangedInterface]
public class DownloadFileService : IDownloadFileService
{
    #region Private Fields

    private readonly IUnitOfWork _unitOfWork;
    private readonly Dictionary<int, DownloadService> _downloadServices;
    private readonly Dictionary<int, DownloadConfiguration> _downloadConfigurations;
    private readonly Dictionary<int, Window> _downloadFileWindows;
    private readonly Dictionary<int, bool> _windowClosingStates;

    #endregion

    #region Events

    public event EventHandler<DownloadFileServiceEventArgs>? DataChanged;

    #endregion

    #region Properties

    public ObservableCollection<DownloadFileViewModel> DownloadFiles { get; set; } = [];

    #endregion

    public DownloadFileService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _downloadServices = [];
        _downloadConfigurations = [];
        _downloadFileWindows = [];
        _windowClosingStates = [];
    }

    public async Task LoadFilesAsync()
    {
        var primaryKeys = DownloadFiles.Select(df => df.Id).ToList();
        var downloadFiles = await _unitOfWork.DownloadFileRepository
            .GetAllAsync(includeProperties: ["Category.FileExtensions", "DownloadQueue"]);

        foreach (var downloadFile in downloadFiles)
        {
            if (primaryKeys.Contains(downloadFile.Id))
                continue;

            var vm = ConvertToDownloadFileViewModel(downloadFile);
            DownloadFiles.Add(vm);
        }
    }

    public async Task AddFileAsync(DownloadFile downloadFile)
    {
        await _unitOfWork.DownloadFileRepository.AddAsync(downloadFile);
        await _unitOfWork.SaveAsync();
        await LoadFilesAsync();
    }

    public async Task UpdateFileAsync(DownloadFile downloadFile)
    {
        await _unitOfWork.DownloadFileRepository.UpdateAsync(downloadFile);
        await _unitOfWork.SaveAsync();
        await LoadFilesAsync();
    }

    public async Task UpdateFileAsync(DownloadFileViewModel viewModel)
    {
        var downloadFile = await _unitOfWork.DownloadFileRepository.GetAsync(where: df => df.Id == viewModel.Id);
        if (downloadFile == null)
            return;

        DownloadStatus downloadStatus = DownloadStatus.None;
        if (viewModel.IsCompleted)
            downloadStatus = DownloadStatus.Completed;
        else if (viewModel.IsDownloading)
            downloadStatus = DownloadStatus.Downloading;
        else if (viewModel.IsPaused)
            downloadStatus = DownloadStatus.Pause;
        else if (viewModel.IsError)
            downloadStatus = DownloadStatus.Error;

        downloadFile.Status = downloadStatus;
        downloadFile.LastTryDate = viewModel.LastTryDate;
        downloadFile.IsPaused = viewModel.IsPaused;
        downloadFile.IsError = viewModel.IsError;
        downloadFile.DownloadProgress = viewModel.DownloadProgress ?? 0;
        downloadFile.ElapsedTime = viewModel.ElapsedTime;
        downloadFile.TimeLeft = viewModel.TimeLeft;
        downloadFile.TransferRate = viewModel.TransferRate;
        downloadFile.DownloadPackage = viewModel.DownloadPackage;

        await UpdateFileAsync(downloadFile);
    }

    public async Task UpdateFilesAsync(List<DownloadFile> downloadFiles)
    {
        await _unitOfWork.DownloadFileRepository.UpdateAllAsync(downloadFiles);
        await _unitOfWork.SaveAsync();
        await LoadFilesAsync();
    }

    public async Task StartDownloadFileAsync(DownloadFileViewModel? downloadFile, Window? window,
        bool minimizeDownloadWindow, bool hideDownloadWindow)
    {
        if (downloadFile == null || window == null)
            return;

        if (minimizeDownloadWindow)
            window.WindowState = WindowState.Minimized;

        switch (hideDownloadWindow)
        {
            case true:
            {
                window.Hide();
                break;
            }

            case false:
            {
                window.Show();
                break;
            }
        }

        window.Tag = downloadFile.Id;
        window.Closing += WindowOnClosing;

        var downloadConfiguration = new DownloadConfiguration
        {
            ChunkCount = 8,
            MaximumBytesPerSecond = 64 * 1024,
            ParallelDownload = true,
        };

        var downloadService = new DownloadService(downloadConfiguration);

        _downloadServices.Add(downloadFile.Id, downloadService);
        _downloadConfigurations.Add(downloadFile.Id, downloadConfiguration);
        _downloadFileWindows.Add(downloadFile.Id, window);
        _windowClosingStates.Add(downloadFile.Id, false);

        await downloadFile.StartDownloadFileAsync(downloadService, downloadConfiguration, _unitOfWork);
    }

    private async void WindowOnClosing(object? sender, WindowClosingEventArgs e)
    {
        // TODO: Show message box
        try
        {
            var window = sender as Window;
            if (window == null)
                return;

            var tag = window.Tag?.ToString();
            if (tag.IsNullOrEmpty() || !int.TryParse(tag, out var id))
                return;

            var downloadFile = DownloadFiles.FirstOrDefault(df => df.Id == id);
            if (downloadFile == null)
                return;

            var state = _windowClosingStates.FirstOrDefault(wcs => wcs.Key == downloadFile.Id).Value;
            if (state)
                return;

            _windowClosingStates.Remove(downloadFile.Id);
            _windowClosingStates.Add(downloadFile.Id, true);

            await StopDownloadFileAsync(downloadFile);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    public async Task StopDownloadFileAsync(DownloadFileViewModel? downloadFile)
    {
        if (downloadFile == null)
            return;

        var downloadService = _downloadServices.FirstOrDefault(ds => ds.Key == downloadFile.Id).Value;
        var window = _downloadFileWindows.FirstOrDefault(dw => dw.Key == downloadFile.Id).Value;
        if (downloadService == null || window == null)
            return;

        await downloadFile.StopDownloadFileAsync(downloadService);

        _downloadConfigurations.Remove(downloadFile.Id);
        _downloadServices.Remove(downloadFile.Id);
        _downloadFileWindows.Remove(downloadFile.Id);
        _windowClosingStates.Remove(downloadFile.Id);

        await UpdateFileAsync(downloadFile);
        
        window.Closing -= WindowOnClosing;
    }

    public void ResumeDownloadFile(DownloadFileViewModel? downloadFile)
    {
        if (downloadFile == null)
            return;

        var downloadService = _downloadServices.FirstOrDefault(ds => ds.Key == downloadFile.Id).Value;
        if (downloadService == null)
            return;

        downloadFile.ResumeDownloadFile(downloadService);
    }

    public void PauseDownloadFile(DownloadFileViewModel? downloadFile)
    {
        if (downloadFile == null)
            return;

        var downloadService = _downloadServices.FirstOrDefault(ds => ds.Key == downloadFile.Id).Value;
        if (downloadService == null)
            return;

        downloadFile.PauseDownloadFile(downloadService);
    }

    public void LimitDownloadFileSpeed(DownloadFileViewModel? downloadFile, long speed)
    {
        if (downloadFile == null)
            return;

        var downloadConfiguration = _downloadConfigurations.FirstOrDefault(dc => dc.Key == downloadFile.Id).Value;
        if (downloadConfiguration == null)
            return;

        downloadConfiguration.MaximumBytesPerSecond = speed;
    }

    #region Helpers

    private DownloadFileViewModel ConvertToDownloadFileViewModel(DownloadFile downloadFile)
    {
        var ext = Path.GetExtension(downloadFile.FileName);
        var fileType =
            downloadFile.Category?.FileExtensions
                ?.FirstOrDefault(fe => fe.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase))?.Alias ??
            Constants.UnknownFileType;

        var vm = new DownloadFileViewModel
        {
            Id = downloadFile.Id,
            FileName = downloadFile.FileName,
            FileType = fileType,
            QueueId = downloadFile.DownloadQueue?.Id,
            QueueName = downloadFile.DownloadQueue?.Title ?? string.Empty,
            Size = downloadFile.Size == 0 ? null : downloadFile.Size,
            IsCompleted = Math.Abs(Math.Floor(downloadFile.DownloadProgress) - 100) == 0,
            IsDownloading = false,
            IsPaused = downloadFile.IsPaused,
            IsError = downloadFile.IsError,
            DownloadProgress = downloadFile.DownloadProgress == 0 ? null : downloadFile.DownloadProgress,
            ElapsedTime = downloadFile.ElapsedTime,
            TimeLeft = downloadFile.TimeLeft,
            TransferRate = downloadFile.TransferRate,
            LastTryDate = downloadFile.LastTryDate,
            DateAdded = downloadFile.DateAdded,
            Url = downloadFile.Url,
            SaveLocation = downloadFile.SaveLocation,
            CategoryId = downloadFile.CategoryId,
            DownloadPackage = downloadFile.DownloadPackage,
        };

        return vm;
    }

    #endregion
}