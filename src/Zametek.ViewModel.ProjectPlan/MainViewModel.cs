using Dock.Model.Controls;
using Dock.Model.Core;
using ReactiveUI;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class MainViewModel
        : ViewModelBase, IMainViewModel
    {
        #region Fields

        private readonly object m_Lock;

        private static readonly IList<IFileFilter> s_ProjectPlanFileFilters =
            new List<IFileFilter>
            {
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ProjectPlanFileType,
                    Extensions = new List<string>
                    {
                        Resource.ProjectPlan.Filters.Filter_ProjectPlanFileExtension
                    }
                }
            };

        private static readonly IList<IFileFilter> s_ImportFileFilters =
            new List<IFileFilter>
            {
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_MicrosoftProjectFileType,
                    Extensions = new List<string>
                    {
                        Resource.ProjectPlan.Filters.Filter_MicrosoftProjectMppFileExtension,
                        Resource.ProjectPlan.Filters.Filter_MicrosoftProjectXmlFileExtension
                    }
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ProjectXlsxFileType,
                    Extensions = new List<string>
                    {
                        Resource.ProjectPlan.Filters.Filter_ProjectXlsxFileExtension
                    }
                }
            };

        private static readonly IList<IFileFilter> s_ExportFileFilters =
            new List<IFileFilter>
            {
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ExcelFileType,
                    Extensions = new List<string>
                    {
                        Resource.ProjectPlan.Filters.Filter_ExcelXlsxFileExtension
                    }
                }
            };

        private readonly IFactory m_DockFactory;
        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IProjectFileImport m_ProjectFileImport;
        private readonly IProjectFileExport m_ProjectFileExport;
        private readonly IProjectFileOpen m_ProjectFileOpen;
        private readonly IProjectFileSave m_ProjectFileSave;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;

        #endregion

        #region Ctors

        public MainViewModel(
            IFactory dockFactory,//!!,
            ICoreViewModel coreViewModel,//!!,
            IProjectFileImport projectFileImport,//!!,
            IProjectFileExport projectFileExport,//!!,
            IProjectFileOpen projectFileOpen,//!!,
            IProjectFileSave projectFileSave,//!!,
            ISettingService settingService,//!!,
            IDialogService dialogService)//!!)
        {
            m_Lock = new object();
            m_DockFactory = dockFactory;
            m_CoreViewModel = coreViewModel;
            m_ProjectFileImport = projectFileImport;
            m_ProjectFileExport = projectFileExport;
            m_ProjectFileOpen = projectFileOpen;
            m_ProjectFileSave = projectFileSave;
            m_SettingService = settingService;
            m_DialogService = dialogService;

            {
                ReactiveCommand<Unit, Unit> openProjectPlanFileCommand = ReactiveCommand.CreateFromTask(OpenProjectPlanFileAsync);
                openProjectPlanFileCommand.IsExecuting.ToProperty(this, main => main.IsOpening, out m_IsOpening);
                OpenProjectPlanFileCommand = openProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> saveProjectPlanFileCommand = ReactiveCommand.CreateFromTask(SaveProjectPlanFileAsync);
                saveProjectPlanFileCommand.IsExecuting.ToProperty(this, main => main.IsSaving, out m_IsSaving);
                SaveProjectPlanFileCommand = saveProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> saveAsProjectPlanFileCommand = ReactiveCommand.CreateFromTask(SaveAsProjectPlanFileAsync);
                saveAsProjectPlanFileCommand.IsExecuting.ToProperty(this, main => main.IsSavingAs, out m_IsSavingAs);
                SaveAsProjectPlanFileCommand = saveAsProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> importProjectFileCommand = ReactiveCommand.CreateFromTask(ImportProjectFileAsync);
                importProjectFileCommand.IsExecuting.ToProperty(this, main => main.IsImporting, out m_IsImporting);
                ImportProjectFileCommand = importProjectFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> exportProjectFileCommand = ReactiveCommand.CreateFromTask(ExportProjectFileAsync);
                exportProjectFileCommand.IsExecuting.ToProperty(this, main => main.IsExporting, out m_IsExporting);
                ExportProjectFileCommand = exportProjectFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> closeProjectPlanCommand = ReactiveCommand.CreateFromTask(CloseProjectPlanAsync);
                closeProjectPlanCommand.IsExecuting.ToProperty(this, main => main.IsClosing, out m_IsClosing);
                CloseProjectPlanCommand = closeProjectPlanCommand;
            }

            ToggleShowDatesCommand = ReactiveCommand.Create(ToggleShowDates);
            ToggleUseBusinessDaysCommand = ReactiveCommand.Create(ToggleUseBusinessDays);

            CompileCommand = ReactiveCommand.CreateFromTask(RunCompileAsync);
            ToggleAutoCompileCommand = ReactiveCommand.Create(ToggleAutoCompile);
            TransitiveReductionCommand = ReactiveCommand.Create(RunTransitiveReductionAsync);

            OpenHyperLinkCommand = ReactiveCommand.Create<string, Task>(OpenHyperLinkAsync);
            OpenAboutCommand = ReactiveCommand.Create(OpenAboutAsync);

            m_ProjectTitle = this
                .WhenAnyValue(
                    main => main.m_CoreViewModel.ProjectTitle,
                    main => main.m_CoreViewModel.IsProjectUpdated,
                    (title, isProjectUpdate) => $@"{(isProjectUpdate ? "*" : "")}{(string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title)} - {Resource.ProjectPlan.Titles.Title_ProjectPlan}")
                .ToProperty(this, main => main.ProjectTitle);

            m_IsBusy = this
                .WhenAnyValue(
                    main => main.IsOpening,
                    main => main.IsSaving,
                    main => main.IsSavingAs,
                    main => main.IsImporting,
                    main => main.IsExporting,
                    main => main.IsClosing,
                    main => main.m_CoreViewModel.IsBusy,
                    (isOpening, isSaving, isSavingAs, isImporting, isExporting, isClosing, isBusy) =>
                        isOpening || isSaving || isSavingAs || isImporting || isExporting || isClosing || isBusy)
                .ToProperty(this, x => x.IsBusy);

            m_IsProjectUpdated = this
                .WhenAnyValue(main => main.m_CoreViewModel.IsProjectUpdated)
                .ToProperty(this, main => main.IsProjectUpdated);

            m_ProjectStart = this
                .WhenAnyValue(main => main.m_CoreViewModel.ProjectStart)
                .ToProperty(this, main => main.ProjectStart);

            m_ProjectStartDateTime = this
                .WhenAnyValue(main => main.m_CoreViewModel.ProjectStartDateTime)
                .ToProperty(this, main => main.ProjectStartDateTime);

            m_ShowDates = this
                .WhenAnyValue(main => main.m_CoreViewModel.ShowDates)
                .ToProperty(this, main => main.ShowDates);

            m_UseBusinessDays = this
                .WhenAnyValue(main => main.m_CoreViewModel.UseBusinessDays)
                .ToProperty(this, main => main.UseBusinessDays);

            m_AutoCompile = this
                .WhenAnyValue(main => main.m_CoreViewModel.AutoCompile)
                .ToProperty(this, main => main.AutoCompile);

            m_CoreViewModel.UseBusinessDays = true;
            m_CoreViewModel.IsProjectUpdated = false;
            m_CoreViewModel.AutoCompile = true;

#if DEBUG
            DebugFactoryEvents(m_DockFactory);
#endif

            m_Layout = m_DockFactory.CreateLayout();
            if (m_Layout is not null)
            {
                m_DockFactory.InitLayout(m_Layout);
            }
        }

        #endregion

        #region Properties

        private IRootDock? m_Layout;
        public IRootDock? Layout
        {
            get => m_Layout;
            set => this.RaiseAndSetIfChanged(ref m_Layout, value);
        }

        private readonly ObservableAsPropertyHelper<bool> m_IsOpening;
        public bool IsOpening => m_IsOpening.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsSaving;
        public bool IsSaving => m_IsSaving.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsSavingAs;
        public bool IsSavingAs => m_IsSavingAs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsImporting;
        public bool IsImporting => m_IsImporting.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsExporting;
        public bool IsExporting => m_IsExporting.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsClosing;
        public bool IsClosing => m_IsClosing.Value;

        #endregion

        #region Private Methods

        private static void DebugFactoryEvents(IFactory factory)
        {
            factory.ActiveDockableChanged += (_, args) =>
            {
                Debug.WriteLine($"[ActiveDockableChanged] Title='{args.Dockable?.Title}'");
            };

            factory.FocusedDockableChanged += (_, args) =>
            {
                Debug.WriteLine($"[FocusedDockableChanged] Title='{args.Dockable?.Title}'");
            };

            factory.DockableAdded += (_, args) =>
            {
                Debug.WriteLine($"[DockableAdded] Title='{args.Dockable?.Title}'");
            };

            factory.DockableRemoved += (_, args) =>
            {
                Debug.WriteLine($"[DockableRemoved] Title='{args.Dockable?.Title}'");
            };

            factory.DockableClosed += (_, args) =>
            {
                Debug.WriteLine($"[DockableClosed] Title='{args.Dockable?.Title}'");
            };

            factory.DockableMoved += (_, args) =>
            {
                Debug.WriteLine($"[DockableMoved] Title='{args.Dockable?.Title}'");
            };

            factory.DockableSwapped += (_, args) =>
            {
                Debug.WriteLine($"[DockableSwapped] Title='{args.Dockable?.Title}'");
            };

            factory.DockablePinned += (_, args) =>
            {
                Debug.WriteLine($"[DockablePinned] Title='{args.Dockable?.Title}'");
            };

            factory.DockableUnpinned += (_, args) =>
            {
                Debug.WriteLine($"[DockableUnpinned] Title='{args.Dockable?.Title}'");
            };

            factory.WindowOpened += (_, args) =>
            {
                Debug.WriteLine($"[WindowOpened] Title='{args.Window?.Title}'");
            };

            factory.WindowClosed += (_, args) =>
            {
                Debug.WriteLine($"[WindowClosed] Title='{args.Window?.Title}'");
            };

            factory.WindowClosing += (_, args) =>
            {
                // NOTE: Set to True to cancel window closing.
#if false
                args.Cancel = true;
#endif
                Debug.WriteLine($"[WindowClosing] Title='{args.Window?.Title}', Cancel={args.Cancel}");
            };

            factory.WindowAdded += (_, args) =>
            {
                Debug.WriteLine($"[WindowAdded] Title='{args.Window?.Title}'");
            };

            factory.WindowRemoved += (_, args) =>
            {
                Debug.WriteLine($"[WindowRemoved] Title='{args.Window?.Title}'");
            };

            factory.WindowMoveDragBegin += (_, args) =>
            {
                // NOTE: Set to True to cancel window dragging.
#if false
                args.Cancel = true;
#endif
                Debug.WriteLine($"[WindowMoveDragBegin] Title='{args.Window?.Title}', Cancel={args.Cancel}, X='{args.Window?.X}', Y='{args.Window?.Y}'");
            };

            factory.WindowMoveDrag += (_, args) =>
            {
                Debug.WriteLine($"[WindowMoveDrag] Title='{args.Window?.Title}', X='{args.Window?.X}', Y='{args.Window?.Y}");
            };

            factory.WindowMoveDragEnd += (_, args) =>
            {
                Debug.WriteLine($"[WindowMoveDragEnd] Title='{args.Window?.Title}', X='{args.Window?.X}', Y='{args.Window?.Y}");
            };
        }

        private void ToggleShowDates() => ShowDates = !ShowDates;

        private void ToggleUseBusinessDays() => UseBusinessDays = !UseBusinessDays;

        private void ToggleAutoCompile() => AutoCompile = !AutoCompile;

        private void ProcessProjectImport(ProjectImportModel importModel) => m_CoreViewModel.ProcessProjectImport(importModel);

        private void ProcessProjectPlan(ProjectPlanModel planModel) => m_CoreViewModel.ProcessProjectPlan(planModel);

        private async Task<ProjectPlanModel> BuildProjectPlanAsync() => await Task.Run(() => m_CoreViewModel.BuildProjectPlan());

        private async Task RunCompileAsync() => await Task.Run(() => m_CoreViewModel.RunCompile());

        private async Task RunAutoCompileAsync() => await Task.Run(() => m_CoreViewModel.RunAutoCompile());

        private async Task RunTransitiveReductionAsync() => await Task.Run(() => m_CoreViewModel.RunTransitiveReduction());

        private void ResetProject() => m_CoreViewModel.ResetProject();

        private async Task SaveProjectPlanFileInternalAsync(string? filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    Resource.ProjectPlan.Messages.Message_EmptyFilename);
            }
            else
            {
                ProjectPlanModel projectPlan = await BuildProjectPlanAsync();
                await m_ProjectFileSave.SaveProjectPlanFileAsync(projectPlan, filename);
                m_CoreViewModel.IsProjectUpdated = false;
                m_SettingService.SetFilePath(filename);
            }
        }

        #endregion

        #region IMainViewModel Members

        private readonly ObservableAsPropertyHelper<string> m_ProjectTitle;
        public string ProjectTitle
        {
            get => m_ProjectTitle.Value;
            private set
            {
                lock (m_Lock) m_CoreViewModel.ProjectTitle = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsProjectUpdated;
        public bool IsProjectUpdated => m_IsProjectUpdated.Value;

        private readonly ObservableAsPropertyHelper<DateTimeOffset> m_ProjectStart;
        public DateTimeOffset ProjectStart
        {
            get => m_ProjectStart.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.ProjectStart = value;
            }
        }

        private readonly ObservableAsPropertyHelper<DateTime> m_ProjectStartDateTime;
        public DateTime ProjectStartDateTime
        {
            get => m_ProjectStartDateTime.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.ProjectStartDateTime = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowDates;
        public bool ShowDates
        {
            get => m_ShowDates.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.ShowDates = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_UseBusinessDays;
        public bool UseBusinessDays
        {
            get => m_UseBusinessDays.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.UseBusinessDays = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_AutoCompile;
        public bool AutoCompile
        {
            get => m_AutoCompile.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.AutoCompile = value;
            }
        }

        public ICommand OpenProjectPlanFileCommand { get; }

        public ICommand SaveProjectPlanFileCommand { get; }

        public ICommand SaveAsProjectPlanFileCommand { get; }

        public ICommand ImportProjectFileCommand { get; }

        public ICommand ExportProjectFileCommand { get; }

        public ICommand CloseProjectPlanCommand { get; }

        public ICommand ToggleShowDatesCommand { get; }

        public ICommand ToggleUseBusinessDaysCommand { get; }

        public ICommand CompileCommand { get; }

        public ICommand ToggleAutoCompileCommand { get; }

        public ICommand TransitiveReductionCommand { get; }

        public ICommand OpenHyperLinkCommand { get; }

        public ICommand OpenAboutCommand { get; }

        public void CloseLayout()
        {
            if (Layout is IDock dock)
            {
                if (dock.Close.CanExecute(null))
                {
                    dock.Close.Execute(null);
                }
            }
        }

        public void ResetLayout()
        {
            if (Layout is not null)
            {
                if (Layout.Close.CanExecute(null))
                {
                    Layout.Close.Execute(null);
                }
            }

            IRootDock? layout = m_DockFactory.CreateLayout();
            if (layout is not null)
            {
                Layout = layout;
                m_DockFactory.InitLayout(layout);
            }
        }

        public async Task OpenProjectPlanFileAsync()
        {
            try
            {
                if (IsProjectUpdated)
                {
                    bool confirmation = await m_DialogService.ShowConfirmationAsync(
                        Resource.ProjectPlan.Titles.Title_UnsavedChanges,
                        Resource.ProjectPlan.Messages.Message_UnsavedChanges);

                    if (!confirmation)
                    {
                        return;
                    }
                }
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowOpenFileDialogAsync(string.Empty, directory, s_ProjectPlanFileFilters);

                if (!string.IsNullOrWhiteSpace(filename))
                {
                    ProjectPlanModel planModel = await m_ProjectFileOpen.OpenProjectPlanFileAsync(filename);
                    ProcessProjectPlan(planModel);
                    m_SettingService.SetFilePath(filename);
                    await RunAutoCompileAsync();
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    ex.Message);
                ResetProject();
            }
        }

        public async Task SaveProjectPlanFileAsync()
        {
            try
            {
                string projectTitle = m_SettingService.ProjectTitle;

                if (string.IsNullOrWhiteSpace(projectTitle))
                {
                    await SaveAsProjectPlanFileAsync();
                    return;
                }

                string directory = m_SettingService.ProjectDirectory;
                string? filename = Path.Combine(directory, projectTitle);
                filename = Path.ChangeExtension(filename, Resource.ProjectPlan.Filters.Filter_ProjectPlanFileExtension);
                await SaveProjectPlanFileInternalAsync(filename);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    ex.Message);
            }
        }

        public async Task SaveAsProjectPlanFileAsync()
        {
            try
            {
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(string.Empty, directory, s_ProjectPlanFileFilters);

                if (string.IsNullOrWhiteSpace(filename))
                {
                    return;
                }

                await SaveProjectPlanFileInternalAsync(filename);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    ex.Message);
            }
        }

        public async Task ImportProjectFileAsync()
        {
            try
            {
                if (IsProjectUpdated)
                {
                    bool confirmation = await m_DialogService.ShowConfirmationAsync(
                        Resource.ProjectPlan.Titles.Title_UnsavedChanges,
                        Resource.ProjectPlan.Messages.Message_UnsavedChanges);

                    if (!confirmation)
                    {
                        return;
                    }
                }
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowOpenFileDialogAsync(string.Empty, directory, s_ImportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename))
                {
                    ProjectImportModel importModel = await m_ProjectFileImport.ImportProjectFileAsync(filename);
                    ProcessProjectImport(importModel);
                    m_SettingService.SetFilePath(filename);
                    await RunAutoCompileAsync();
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    ex.Message);
                ResetProject();
            }
        }

        public async Task ExportProjectFileAsync()
        {
            try
            {
                string projectTitle = m_SettingService.ProjectTitle;
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(projectTitle, directory, s_ExportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename))
                {
                    ProjectPlanModel projectPlan = await BuildProjectPlanAsync();
                    await m_ProjectFileExport.ExportProjectPlanFileAsync(
                        projectPlan,
                        m_CoreViewModel.ResourceSeriesSet,
                        m_CoreViewModel.TrackingSeriesSet,
                        ShowDates,
                        filename);
                    m_SettingService.SetFilePath(filename);
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    ex.Message);
            }
        }

        public async Task CloseProjectPlanAsync()
        {
            try
            {
                if (IsProjectUpdated)
                {
                    bool confirmation = await m_DialogService.ShowConfirmationAsync(
                        Resource.ProjectPlan.Titles.Title_UnsavedChanges,
                        Resource.ProjectPlan.Messages.Message_UnsavedChanges);

                    if (!confirmation)
                    {
                        return;
                    }
                }
                ResetProject();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    ex.Message);
                ResetProject();
            }
        }

        public async Task OpenHyperLinkAsync(string hyperlink)
        {
            try
            {
                var uri = new Uri(hyperlink);
                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    ex.Message);
            }
        }

        public async Task OpenAboutAsync()
        {
            try
            {
                var about = new StringBuilder();
                about.AppendLine($"## {Resource.ProjectPlan.Labels.Label_AppName}");
                about.AppendLine();
                about.AppendLine(Resource.ProjectPlan.Labels.Label_AppVersion);
                about.AppendLine();
                about.AppendLine($"{Resource.ProjectPlan.Labels.Label_Copyright}, {Resource.ProjectPlan.Labels.Label_Author}");
                about.AppendLine();

                await m_DialogService.ShowInfoAsync(
                    Resource.ProjectPlan.Titles.Title_ProjectPlan,
                    about.ToString(), height: 180, width: 400,
                    markdown: true);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    ex.Message);
            }
        }

        #endregion
    }
}