using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Runtime.InteropServices;
using System.IO;
using System;
using System.Threading.Tasks;
using AvaloniaUI.MVVM.Navigation;
using AvaloniaUI.MVVM.ViewModel;
using AvaloniaUI.MVVM.ViewModel.Base;
using ExpPlot.ViewModels.Validation;
using ExpPlot.ViewModels.ResultArgs;
using ExpPlot.Services.Settings;
using ExpPlot.Helpers;
using ExpPlot.Models;

namespace ExpPlot.ViewModels.Dialog
{
    public class SelectSourceViewModel : WindowViewModelBase<SelectSourceDialogResult>
    {
        private readonly ISettingsService _settings;
        private readonly string _channelsFileName = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()!.Location)!, "Analogs.txt");
        private readonly string _defaultAistIp = "1.1.1.1";
        private readonly string _defaultRdaPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:\\" : "/home";

        public SelectSourceViewModel(
            ISettingsService settings,
            INavigationService navigationService) :
            base(navigationService)
        {
            _settings = settings;

            GetDataFromSettings();

            this.WhenAnyValue(x => x.UseAist, x => x.AistAddress.HasErrors,
                              x => x.RdaFilePath, x => x.ChannelsFilePath,
                              x => x.UseLocalTimeZone, x => x.TimeOffset.HasErrors)
                .Subscribe(_ => IsValid = CheckValidity());

            ConfirmCommand = ReactiveCommand.CreateFromTask(() => ExceptionCatchWrapAsync(Confirm),
                this.WhenAnyValue(x => x.IsValid));
            CancelCommand = ReactiveCommand.Create(Cancel);
            SelectRdaFolderCommand = ReactiveCommand.CreateFromTask(SelectRdaFolder);
            SelectChannelsFileCommand = ReactiveCommand.CreateFromTask(SelectChannelsFile);
        }

        public ReactiveCommand<Unit, bool> ConfirmCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }
        public ReactiveCommand<Unit, Unit> SelectRdaFolderCommand { get; }
        public ReactiveCommand<Unit, Unit> SelectChannelsFileCommand { get; }

        [Reactive] public bool UseAist { get; set; }
        [Reactive] public bool UseFile { get; set; }
        [Reactive] public IpViewModel AistAddress { get; set; }
        [Reactive] public TimeZoneViewModel TimeOffset { get; set; }
        [Reactive] public bool UseLocalTimeZone { get; set; } = true;
        [Reactive] public string RdaFilePath { get; set; }
        [Reactive] public string ChannelsFilePath { get; set; }
        [Reactive] public bool IsValid { get; set; }

        public async Task SelectRdaFolder()
        {
            var folder = await NavigationService.ShowOpenFolderDialog(
                string.IsNullOrEmpty(RdaFilePath) ? _defaultRdaPath : RdaFilePath,
                ApplicationResources.GetString("SelectSource.OpenFolder.Title"));

            if (folder != null && folder.Count != 0 && !string.IsNullOrEmpty(folder[0].Path.LocalPath))
                RdaFilePath = folder[0].Path.LocalPath;
        }

        public async Task SelectChannelsFile()
        {
            var resultLines = await NavigationService.ShowOpenFileDialog(
                string.IsNullOrEmpty(ChannelsFilePath) ? _defaultRdaPath : ChannelsFilePath, null,
                ApplicationResources.GetString("SelectSource.OpenFile.Title"));

            if (resultLines != null && resultLines.Count != 0 && !string.IsNullOrEmpty(resultLines[0].Path.LocalPath))
                ChannelsFilePath = resultLines[0].Path.LocalPath;
        }

        public async Task Confirm()
        {
            bool sourceChanged = false;

            if (UseAist && !AistAddress.HasErrors && Int32.TryParse(AistAddress.Port, out var port) &&
                (_settings.AistServer != AistAddress.Ip || _settings.AistPort != port || _settings.DataSource != DataSource.Aist))
            {
                sourceChanged = true;
                _settings.DataSource = DataSource.Aist;
                _settings.AistServer = AistAddress.Ip!;
                _settings.AistPort = port;

                Close(new SelectSourceDialogResult(sourceChanged));
            }
            else if (UseFile && !string.IsNullOrEmpty(RdaFilePath) && !string.IsNullOrEmpty(ChannelsFilePath))
            {
                if (!Directory.Exists(RdaFilePath))
                {
                    await NavigationService.ShowDialogAsync<MessageViewModel, AvaloniaUI.MVVM.ViewModel.MessageNavigationParameter>(new MessageViewModel(), new AvaloniaUI.MVVM.ViewModel.MessageNavigationParameter(
                            string.Format(ApplicationResources.GetString("SelectSource.FolderError"), RdaFilePath),
                            ApplicationResources.GetString("Message")));
                    return;
                }

                if (!File.Exists(ChannelsFilePath))
                {
                    await NavigationService.ShowDialogAsync<AvaloniaUI.MVVM.ViewModel.MessageViewModel, AvaloniaUI.MVVM.ViewModel.MessageNavigationParameter>(new MessageViewModel(), new AvaloniaUI.MVVM.ViewModel.MessageNavigationParameter(
                        string.Format(ApplicationResources.GetString("SelectSource.FileError"), ChannelsFilePath),
                        ApplicationResources.GetString("Message")));
                    return;
                }

                _settings.FilePath = RdaFilePath;
                if (!ChannelsFilePath.Equals(_channelsFileName))
                {
                    File.Copy(ChannelsFilePath, _channelsFileName, true);
                    sourceChanged = true;
                }

                if (_settings.DataSource != DataSource.File)
                {
                    _settings.DataSource = DataSource.File;
                    sourceChanged = true;
                }

                _settings.TimeZoneOffset = UseLocalTimeZone ? "" : TimeOffset.Value!;

                Close(new SelectSourceDialogResult(sourceChanged));
            }
            Close(new SelectSourceDialogResult(sourceChanged));
        }

        public void Cancel()
        {
            Close(new SelectSourceDialogResult(false));
        }

        private void GetDataFromSettings()
        {
            UseAist = _settings.DataSource == DataSource.Aist;
            UseFile = !UseAist;

            AistAddress = new IpViewModel(string.IsNullOrEmpty(_settings.AistServer) ? _defaultAistIp : _settings.AistServer,
                                        _settings.AistPort.ToString());

            RdaFilePath = string.IsNullOrEmpty(_settings.FilePath) ? _defaultRdaPath : _settings.FilePath;
            if (File.Exists(_channelsFileName))
                ChannelsFilePath = _channelsFileName;

            if (!string.IsNullOrEmpty(_settings.TimeZoneOffset))
            {
                UseLocalTimeZone = false;
                TimeOffset = new TimeZoneViewModel(_settings.TimeZoneOffset);
            }
            else TimeOffset = new TimeZoneViewModel();
        }

        private bool CheckValidity()
        {
            return UseAist
                ? AistAddress is { HasErrors: false }
                : !string.IsNullOrEmpty(RdaFilePath) && !string.IsNullOrEmpty(ChannelsFilePath) && (UseLocalTimeZone || (!UseLocalTimeZone && !TimeOffset.HasErrors));
        }
    }
}

