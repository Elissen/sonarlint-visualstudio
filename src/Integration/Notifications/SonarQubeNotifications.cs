﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Timers;
using Microsoft.VisualStudio.Shell;
using SonarLint.VisualStudio.Integration.Service;
using SonarLint.VisualStudio.Integration.State;
using SonarLint.VisualStudio.Integration.WPF;

using CancellationTokenSource = System.Threading.CancellationTokenSource;

namespace SonarLint.VisualStudio.Integration.Notifications
{
    [Export(typeof(ISonarQubeNotifications))]
    internal class SonarQubeNotifications : ViewModelBase, ISonarQubeNotifications
    {
        private string text = "You have no events.";
        private bool hasUnreadEvents;
        private bool isVisible;
        private bool isBalloonTooltipVisible;

        private readonly ITimer timer;
        private const string iconPath = "pack://application:,,,/SonarLint;component/Resources/sonarqube_green.ico";
        private const string tooltipTitle = "SonarQube notification";
        private readonly IStateManager stateManager;
        private readonly ISonarQubeServiceWrapper sonarQubeService;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        public ObservableCollection<NotificationEvent> NotificationEvents { get; }
        public NotificationData NotificationData { get; private set; }

        [ImportingConstructor]
        [ExcludeFromCodeCoverage] // Do not unit test MEF constructor
        internal SonarQubeNotifications(IHost host)
            : this(host.SonarQubeService, host.VisualStateManager,
                  new TimerWrapper { Interval = 10000 /* 60sec */ })
        {
        }

        internal SonarQubeNotifications(ISonarQubeServiceWrapper sonarQubeService,
            IStateManager stateManager, ITimer timer)
        {
            this.timer = timer;
            this.sonarQubeService = sonarQubeService;
            this.stateManager = stateManager;

            timer.Elapsed += OnTimerElapsed;
            NotificationEvents = new ObservableCollection<NotificationEvent>();
        }

        public string Text
        {
            get
            {
                return text;
            }
            set
            {
                SetAndRaisePropertyChanged(ref text, value);
            }
        }

        public bool HasUnreadEvents
        {
            get
            {
                return hasUnreadEvents;
            }

            set
            {
                SetAndRaisePropertyChanged(ref hasUnreadEvents, value);

                UpdateTooltipText();
            }
        }

        public bool IsVisible
        {
            get
            {
                return isVisible;
            }
            set
            {
                SetAndRaisePropertyChanged(ref isVisible, value);
            }
        }

        public bool IsEnabled
        {
            get
            {
                return NotificationData.IsEnabled;
            }
            set
            {
                var isEnabled = NotificationData.IsEnabled;
                SetAndRaisePropertyChanged(ref isEnabled, value);
                NotificationData.IsEnabled = IsEnabled;
            }
        }

        public bool IsBalloonTooltipVisible
        {
            get
            {
                return isBalloonTooltipVisible;
            }
            set
            {
                SetAndRaisePropertyChanged(ref isBalloonTooltipVisible, value);
            }
        }

        public void Start(NotificationData notificationData)
        {
            NotificationData = notificationData ??
                new NotificationData
                {
                    IsEnabled = true,
                    LastNotificationDate = DateTimeOffset.Now.AddDays(-1)
                };

            var oneDayAgo = DateTimeOffset.Now.AddDays(-1);
            if (NotificationData.LastNotificationDate < oneDayAgo)
            {
                NotificationData.LastNotificationDate = oneDayAgo;
            }

            var serverConnection = ThreadHelper.Generic.Invoke(() => stateManager?.GetConnectedServers().FirstOrDefault());
            timer.Start();
            OnTimerElapsed(this, null);
        }

        public void Stop()
        {
            cancellation.Cancel();

            timer.Stop();
            IsVisible = false;
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            var events = GetNotificationEvents();
            if (events == null)
            {
                Stop();
                return;
            }

            if (!IsVisible && events != null)
            {
                IsVisible = true;
            }


            if (IsEnabled)
            {
                ThreadHelper.Generic.Invoke(() => SetNotificationEvents(events));
            }
        }

        private async void AnimateBalloonTooltip()
        {
            IsBalloonTooltipVisible = true;
            await System.Threading.Tasks.Task.Delay(4000);
            IsBalloonTooltipVisible = false;
        }

        private void SetNotificationEvents(NotificationEvent[] events)
        {
            if (events.Length > 0)
            {
                NotificationEvents.Clear();
                Array.ForEach(events, NotificationEvents.Add);

                HasUnreadEvents = true;
            }
        }

        private void UpdateTooltipText()
        {
            Text = NotificationEvents.Count > 0 && HasUnreadEvents
                ? $"You have {NotificationEvents.Count} unread events."
                : "You have no unread events.";
        }

        private NotificationEvent[] GetNotificationEvents()
        {
            var connection = ThreadHelper.Generic.Invoke(() => stateManager?.GetConnectedServers().FirstOrDefault());
            var projectKey = stateManager?.BoundProjectKey;

            if (connection != null && projectKey != null)
            {
                return new NotificationEvent[0];
            }

            NotificationEvent[] events = null;
            if (sonarQubeService.TryGetNotificationEvents(connection, cancellation.Token, projectKey,
                NotificationData.LastNotificationDate, out events))
            {
                if (events?.Length > 0)
                {
                    NotificationData.LastNotificationDate = events.Max(ev => ev.Date);
                }
            }
            else
            {
                events = new NotificationEvent[0];
            }

            return events;
        }

        public void Dispose()
        {
            timer.Elapsed -= OnTimerElapsed;

            Stop();

            timer.Dispose();
        }
    }
}
