﻿using ReactiveUI;
using System.Reactive.Linq;
using System.Text;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class OutputManagerViewModel
        : ToolViewModelBase, IOutputManagerViewModel, IDisposable
    {
        #region Fields

        private readonly object m_Lock;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IDialogService m_DialogService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;

        private readonly IDisposable? m_BuildCompilationOutputSub;

        #endregion

        #region Ctors

        public OutputManagerViewModel(
            ICoreViewModel coreViewModel,//!!,
            IDialogService dialogService,//!!,
            IDateTimeCalculator dateTimeCalculator)//!!)
        {
            m_Lock = new object();
            m_CoreViewModel = coreViewModel;
            m_DialogService = dialogService;
            m_DateTimeCalculator = dateTimeCalculator;
            m_CompilationOutput = string.Empty;

            m_IsBusy = this
                .WhenAnyValue(om => om.m_CoreViewModel.IsBusy)
                .ToProperty(this, om => om.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(om => om.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, om => om.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(om => om.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, om => om.HasCompilationErrors);

            m_ShowDates = this
                .WhenAnyValue(om => om.m_CoreViewModel.ShowDates)
                .ToProperty(this, om => om.ShowDates);

            m_UseBusinessDays = this
                .WhenAnyValue(om => om.m_CoreViewModel.UseBusinessDays)
                .ToProperty(this, om => om.UseBusinessDays);

            m_ProjectStart = this
                .WhenAnyValue(om => om.m_CoreViewModel.ProjectStart)
                .ToProperty(this, om => om.ProjectStart);

            m_BuildCompilationOutputSub = this
                .WhenAnyValue(
                    om => om.m_CoreViewModel.GraphCompilation,
                    om => om.m_CoreViewModel.ResourceSeriesSet,
                    om => om.ShowDates,
                    om => om.UseBusinessDays,
                    om => om.ProjectStart)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async result => await BuildCompilationOutputAsync(result.Item1, result.Item2));

            Id = Resource.ProjectPlan.Titles.Title_Output;
            Title = Resource.ProjectPlan.Titles.Title_Output;
        }

        #endregion

        #region Properties

        private readonly ObservableAsPropertyHelper<bool> m_ShowDates;
        public bool ShowDates => m_ShowDates.Value;

        private readonly ObservableAsPropertyHelper<bool> m_UseBusinessDays;
        public bool UseBusinessDays => m_UseBusinessDays.Value;

        private readonly ObservableAsPropertyHelper<DateTimeOffset> m_ProjectStart;
        public DateTimeOffset ProjectStart => m_ProjectStart.Value;

        #endregion

        #region Private Methods

        private static string CalculateActivitySchedules(
            bool showDates,
            DateTimeOffset projectStart,
            IDateTimeCalculator dateTimeCalculator,//!!,
            IEnumerable<ResourceSeriesModel> resourceSeriesCollection)//!!)
        {
            var output = new StringBuilder();

            foreach (ResourceSeriesModel resourceSeries in resourceSeriesCollection)
            {
                IEnumerable<ScheduledActivityModel> scheduledActivities = resourceSeries.ResourceSchedule.ScheduledActivities;
                if (!scheduledActivities.Any())
                {
                    continue;
                }
                output.AppendLine($@">{resourceSeries.Title}");
                int previousFinishTime = 0;
                foreach (ScheduledActivityModel scheduledActivity in scheduledActivities)
                {
                    int startTime = scheduledActivity.StartTime;
                    int finishTime = scheduledActivity.FinishTime;
                    if (startTime > previousFinishTime)
                    {
                        string from = ChartHelper.FormatScheduleOutput(previousFinishTime, showDates, projectStart, dateTimeCalculator);
                        string to = ChartHelper.FormatScheduleOutput(startTime, showDates, projectStart, dateTimeCalculator);
                        output.AppendLine($@"*** {from} -> {to} ***");
                    }
                    string start = ChartHelper.FormatScheduleOutput(startTime, showDates, projectStart, dateTimeCalculator);
                    string finish = ChartHelper.FormatScheduleOutput(finishTime, showDates, projectStart, dateTimeCalculator);
                    output.AppendLine($@"{Resource.ProjectPlan.Labels.Label_Activity} {scheduledActivity.Id}: {start} -> {finish}");
                    previousFinishTime = finishTime;
                }
                output.AppendLine();
            }
            return output.ToString();
        }

        private void BuildCompilationOutput(
            IGraphCompilation<int, int, IDependentActivity<int, int>> graphCompilation,//!!,
            ResourceSeriesSetModel resourceSeriesSet)//!!)
        {
            lock (m_Lock)
            {
                CompilationOutput = string.Empty;

                IEnumerable<IGraphCompilationError> errors = graphCompilation.CompilationErrors;
                IEnumerable<ResourceSeriesModel> scheduled = resourceSeriesSet.Scheduled;

                var output = new StringBuilder();

                if (HasCompilationErrors)
                {
                    output.AppendLine($@">{Resource.ProjectPlan.Messages.Message_CompilationErrors}");
                    output.AppendLine();

                    foreach (IGraphCompilationError error in errors)
                    {
                        output.AppendLine($@">{Resource.ProjectPlan.Messages.Message_Error}: {error.ErrorCode}");
                        output.AppendLine($@">{error.ErrorMessage}");
                        output.AppendLine();
                    }
                }
                else if (scheduled.Any())
                {
                    output.Append(CalculateActivitySchedules(ShowDates, ProjectStart, m_DateTimeCalculator, scheduled));
                }

                CompilationOutput = output.ToString();
            }
        }

        private async Task BuildCompilationOutputAsync(
            IGraphCompilation<int, int, IDependentActivity<int, int>> graphCompilation,
            ResourceSeriesSetModel resourceSeriesSet)
        {
            try
            {
                lock (m_Lock)
                {
                    BuildCompilationOutput(graphCompilation, resourceSeriesSet);
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    ex.Message);
            }
        }

        #endregion

        #region IOutputManagerViewModel

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private string m_CompilationOutput;
        public string CompilationOutput
        {
            get => m_CompilationOutput;
            set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_CompilationOutput, value);
            }
        }

        #endregion

        #region IDisposable Members

        private bool m_Disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
                m_BuildCompilationOutputSub?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            m_Disposed = true;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
