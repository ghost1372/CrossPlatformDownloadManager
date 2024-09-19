﻿using System.Collections.Generic;
using AutoMapper;
using CrossPlatformDownloadManager.Data.Services.DownloadFileService;
using CrossPlatformDownloadManager.Data.UnitOfWork;
using CrossPlatformDownloadManager.Data.ViewModels;
using CrossPlatformDownloadManager.Data.ViewModels.CustomEventArgs;
using ReactiveUI;

namespace CrossPlatformDownloadManager.DesktopApp.ViewModels;

public class ViewModelBase : ReactiveObject
{
    #region Properties

    protected IUnitOfWork UnitOfWork { get; }

    protected IDownloadFileService DownloadFileService { get; }
    
    protected IMapper Mapper { get; }

    #endregion

    public ViewModelBase(IUnitOfWork unitOfWork, IDownloadFileService downloadFileService, IMapper mapper)
    {
        UnitOfWork = unitOfWork;
        DownloadFileService = downloadFileService;
        Mapper = mapper;
        DownloadFileService.DataChanged += DownloadFileServiceOnDataChanged;
    }

    protected virtual void DownloadFileServiceDataChanged(DownloadFileServiceEventArgs eventArgs)
    {
    }

    private void DownloadFileServiceOnDataChanged(object? sender, DownloadFileServiceEventArgs e)
    {
        DownloadFileServiceDataChanged(e);
    }
}