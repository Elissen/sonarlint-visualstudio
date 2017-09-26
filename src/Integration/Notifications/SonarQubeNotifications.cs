/*
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
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using SonarLint.VisualStudio.Integration.Service;
using SonarLint.VisualStudio.Integration.State;
using CancellationTokenSource = System.Threading.CancellationTokenSource;

namespace SonarLint.VisualStudio.Integration.Notifications
{
    [Export(typeof(ISonarQubeNotifications))]
    internal class SonarQubeNotifications : ISonarQubeNotifications
    {
        private readonly ITimer timer;
        private readonly IStateManager stateManager;
        private readonly ISonarQubeServiceWrapper sonarQubeService;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        public INotificationIndicatorViewModel Model { get; private set; }

        private DateTimeOffset lastCheckDate;

        public NotificationData GetNotificationData() =>
            new NotificationData
                {
                    IsEnabled = Model.AreNotificationsEnabled,
                    LastNotificationDate = lastCheckDate
                };

        [ImportingConstructor]
        [ExcludeFromCodeCoverage] // Do not unit test MEF constructor
        internal SonarQubeNotifications(IHost host)
            : this(host.SonarQubeService, host.VisualStateManager,
                  new NotificationIndicatorViewModel(),
                  new TimerWrapper { Interval = 10000 /* TODO: UNHACK MAKE SURE IT'S 60sec */ })
        {
        }

        internal SonarQubeNotifications(ISonarQubeServiceWrapper sonarQubeService,
            IStateManager stateManager, INotificationIndicatorViewModel model,
            ITimer timer)
        {
            this.timer = timer;
            this.sonarQubeService = sonarQubeService;
            this.stateManager = stateManager;
            Model = model;

            timer.Elapsed += OnTimerElapsed;
        }

        public void Start(NotificationData notificationData)
        {
            Model.AreNotificationsEnabled = notificationData?.IsEnabled ?? true;

            var oneDayAgo = DateTimeOffset.Now.AddDays(-1);
            if (notificationData == null ||
                notificationData.LastNotificationDate < oneDayAgo)
            {
                lastCheckDate = oneDayAgo;
            }
            else
            {
                lastCheckDate = notificationData.LastNotificationDate;
            }

            UpdateEvents();
            timer.Start();
        }

        public void Stop()
        {
            cancellation.Cancel();

            timer.Stop();
            Model.IsIconVisible = false;
        }

        private void UpdateEvents()
        {
            var events = GetNotificationEvents();
            if (events == null)
            {
                Stop();
                return;
            }
            Model.IsIconVisible = true;
            Model.SetNotificationEvents(events);
        }

        private void OnTimerElapsed(object sender, EventArgs e)
        {
            UpdateEvents();
        }

        private NotificationEvent[] GetNotificationEvents()
        {
            //TODO: UNHACK
            return new NotificationEvent[]
            {
                new NotificationEvent
                {
                    Category = "Category",
                    Message = "Quality gate is Red (was Green)",
                    Link = new Uri("http://www.com"),
                    Date = DateTimeOffset.Now,
                    Project = "Project"
                },

                    new NotificationEvent
                {
                    Category = "Category",
                    Message = "You have 17 new issues assigned to you",
                    Link = new Uri("http://www.com"),
                    Date = DateTimeOffset.Now.AddMinutes(5),
                    Project = "Project"
                }
            };

            //var connection = ThreadHelper.Generic.Invoke(() => stateManager?.GetConnectedServers().FirstOrDefault());
            //var projectKey = stateManager?.BoundProjectKey;

            //if (connection != null && projectKey != null)
            //{
            //    return new NotificationEvent[0];
            //}

            //NotificationEvent[] events;
            //if (sonarQubeService.TryGetNotificationEvents(connection, cancellation.Token, projectKey,
            //    lastCheckDate, out events))
            //{
            //    if (events.Length > 0)
            //    {
            //        lastCheckDate = events.Max(ev => ev.Date);
            //    }
            //}
            //else
            //{
            //    return new NotificationEvent[0];
            //}

            //return events;
        }

        public void Dispose()
        {
            timer.Elapsed -= OnTimerElapsed;
            Stop();
            timer.Dispose();
        }
    }
}
