using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using CrossPlatformDownloadManager.Data.Services.DownloadFileService;
using CrossPlatformDownloadManager.Data.UnitOfWork;
using CrossPlatformDownloadManager.Data.ViewModels;
using CrossPlatformDownloadManager.Data.ViewModels.CustomEventArgs;
using CrossPlatformDownloadManager.DesktopApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using RolandK.AvaloniaExtensions.DependencyInjection;

namespace CrossPlatformDownloadManager.DesktopApp.Views.UserControls.AddNewQueueWindowControls;

public partial class FilesView : UserControl
{
    #region Private Fields

    private List<int>? _previousSelectedItems;

    #endregion

    #region Events

    public event EventHandler<DownloadQueueListPriorityChangedEventArgs>? DownloadQueueListPriorityChanged;

    #endregion

    #region Properties

    public static readonly StyledProperty<IEnumerable<DownloadFileViewModel>> FilesItemsSourceProperty =
        AvaloniaProperty.Register<FilesView, IEnumerable<DownloadFileViewModel>>(
            "FilesItemsSource", defaultValue: new List<DownloadFileViewModel>(),
            defaultBindingMode: BindingMode.TwoWay);

    public IEnumerable<DownloadFileViewModel> FilesItemsSource
    {
        get => GetValue(FilesItemsSourceProperty);
        set => SetValue(FilesItemsSourceProperty, value);
    }

    public static readonly StyledProperty<int> DownloadFilesCountAtTheSameTimeProperty =
        AvaloniaProperty.Register<FilesView, int>(
            "DownloadFilesCountAtTheSameTime", defaultValue: 1, defaultBindingMode: BindingMode.TwoWay);

    public int DownloadFilesCountAtTheSameTime
    {
        get => GetValue(DownloadFilesCountAtTheSameTimeProperty);
        set => SetValue(DownloadFilesCountAtTheSameTimeProperty, value);
    }

    public static readonly StyledProperty<int?> DownloadQueueIdProperty = AvaloniaProperty.Register<FilesView, int?>(
        "DownloadQueueId");

    public int? DownloadQueueId
    {
        get => GetValue(DownloadQueueIdProperty);
        set => SetValue(DownloadQueueIdProperty, value);
    }

    #endregion

    public FilesView()
    {
        InitializeComponent();
    }

    private void ChangePriorityToHigherLevelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ChangeItemsPriority(true);
    }

    private void ChangePriorityToLowerLevelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ChangeItemsPriority(false);
    }

    private void FilesDataGrid_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        var propName = e.Property.Name;
        if (!propName.Equals("ItemsSource"))
            return;

        if (_previousSelectedItems == null || !_previousSelectedItems.Any())
            return;

        var items = GetValue(FilesItemsSourceProperty)
            .Where(df => _previousSelectedItems.Contains(df.Id))
            .ToList();

        foreach (var item in items)
            FilesDataGrid.SelectedItems.Add(item);

        _previousSelectedItems = null;
    }

    private void DeleteItemFromDataGridButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (FilesDataGrid.SelectedItems == null || FilesDataGrid.SelectedItems.Count == 0)
            return;

        var list = GetValue(FilesItemsSourceProperty).ToList();
        _previousSelectedItems = null;

        foreach (var item in FilesDataGrid.SelectedItems)
        {
            var downloadFile = item as DownloadFileViewModel;
            if (downloadFile == null)
                continue;

            list.Remove(downloadFile);
        }
        
        var eventArgs = new DownloadQueueListPriorityChangedEventArgs
        {
            NewList = list,
        };

        DownloadQueueListPriorityChanged?.Invoke(this, eventArgs);
    }

    private async void AddItemToDataGridButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // TODO: Show message box if error occurred
        try
        {
            var serviceProvider = this.GetServiceProvider();
            var unitOfWork = serviceProvider.GetService<IUnitOfWork>();
            var downloadFileService = serviceProvider.GetService<IDownloadFileService>();
            var mapper = serviceProvider.GetService<IMapper>();
            if (unitOfWork == null || downloadFileService == null || mapper == null)
                return;
            
            var vm = new AddFilesToQueueWindowViewModel(unitOfWork, downloadFileService, mapper)
            {
                DownloadQueueId = GetValue(DownloadQueueIdProperty),
            };

            var window = new AddFilesToQueueWindow { DataContext = vm };
            var parent = this.Parent;
            if (parent == null)
                return;

            while (parent.GetType() != typeof(Views.AddNewQueueWindow))
            {
                parent = parent.Parent;
                if (parent == null)
                    return;
            }

            var owner = parent as Window;
            if (owner == null)
                return;
            
            var result = await window.ShowDialog<List<DownloadFileViewModel>>(owner);
            if (!result.Any())
                return;
            
            var list = GetValue(FilesItemsSourceProperty).ToList();
            var primaryKeys = result.Select(df => df.Id).Distinct().ToList();
            list = list.Where(df => !primaryKeys.Contains(df.Id)).ToList();
            list.AddRange(result);
            
            var eventArgs = new DownloadQueueListPriorityChangedEventArgs
            {
                NewList = list,
            };

            DownloadQueueListPriorityChanged?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    #region Helpers

    private void ChangeItemsPriority(bool isHighPriority)
    {
        if (FilesDataGrid.SelectedItems == null || FilesDataGrid.SelectedItems.Count == 0)
            return;

        var list = GetValue(FilesItemsSourceProperty).ToList();
        _previousSelectedItems = new List<int>();

        for (int i = isHighPriority ? 0 : FilesDataGrid.SelectedItems.Count - 1;
             isHighPriority ? i < FilesDataGrid.SelectedItems.Count : i >= 0;
             i = isHighPriority ? i + 1 : i - 1)
        {
            var downloadFile = FilesDataGrid.SelectedItems[i] as DownloadFileViewModel;
            if (downloadFile == null)
                continue;

            _previousSelectedItems.Add(downloadFile.Id);

            var index = list.IndexOf(downloadFile);
            if (index == (isHighPriority ? 0 : list.Count - 1))
                continue;

            if (FilesDataGrid.SelectedItems.Contains(list[isHighPriority ? index - 1 : index + 1]))
                continue;

            list.RemoveAt(index);
            list.Insert(isHighPriority ? index - 1 : index + 1, downloadFile);
        }

        var eventArgs = new DownloadQueueListPriorityChangedEventArgs
        {
            NewList = list,
        };

        DownloadQueueListPriorityChanged?.Invoke(this, eventArgs);
    }

    #endregion
}