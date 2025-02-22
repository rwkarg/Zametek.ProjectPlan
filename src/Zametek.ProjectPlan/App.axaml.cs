using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using Splat;
using System;
using System.ComponentModel;
using Zametek.Contract.ProjectPlan;
using Zametek.View.ProjectPlan;

namespace Zametek.ProjectPlan
{
    public partial class App
        : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            IMainViewModel mainViewModel = GetRequiredService<IMainViewModel>();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                MainView mainView = new()
                {
                    DataContext = mainViewModel
                };

                IDialogService dialogService = GetRequiredService<IDialogService>();
                dialogService.Parent = mainView;

                // Cancelling the window closing does not work when using an async handler,
                // and trying to force Wait on the return dialog freezes the UI thread.
                // This solution is the hack below, where CancelClose automatically cancels
                // the closing event first, then CheckClose checks to see if the project
                // has updates.
                // If there are no updates, CheckClose removes all handlers and forces a new close.
                // If there are updates, then the dialog requests permission to proceed.
                // If yes, then it continues as before. If no, then CheckClose removes itself
                // and then adds back all the handlers in the correct order (i.e. the same
                // initial state) and then immediately returns.
                void CancelClose(object? sender, CancelEventArgs args)
                {
                    args.Cancel = true;
                }

                async void CheckClose(object? sender, CancelEventArgs args)
                {
                    mainView.Closing -= CancelClose;

                    if (mainViewModel.IsProjectUpdated)
                    {
                        bool wishToClose = await dialogService.ShowConfirmationAsync(
                            Resource.ProjectPlan.Titles.Title_UnsavedChanges,
                            Resource.ProjectPlan.Messages.Message_UnsavedChanges);

                        if (!wishToClose)
                        {
                            // Clearing the rest of the handlers and then adding
                            // them back in the correct order.
                            mainView.Closing -= CheckClose;
                            mainView.Closing += CancelClose;
                            mainView.Closing += CheckClose;
                            return;
                        }
                    }

                    mainView.Closing -= CheckClose;
                    mainViewModel.CloseLayout();
                    mainView.Close();
                }

                mainView.Closing += CancelClose;
                mainView.Closing += CheckClose;

                desktopLifetime.MainWindow = mainView;

                desktopLifetime.Exit += (a, b) =>
                {
                    mainViewModel.CloseLayout();
                };
            }
            //else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
            //{
            //    var mainView = new MainView()
            //    {
            //        DataContext = mainViewModel
            //    };

            //    singleViewLifetime.MainView = mainView;
            //}
            base.OnFrameworkInitializationCompleted();
        }

        private static T GetRequiredService<T>() =>
            Locator.Current.GetService<T>() ?? throw new NullReferenceException($"{Resource.ProjectPlan.Messages.Message_UnableToResolveType} {typeof(T).FullName}");
    }
}
