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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using SonarLint.VisualStudio.Integration.State;
using SonarQube.Client.Models;
using SonarQube.Client.Services;
using CancellationTokenSource = System.Threading.CancellationTokenSource;

namespace SonarLint.VisualStudio.Integration.Notifications
{
    [Export(typeof(ISonarQubeNotifications))]
    internal class SonarQubeNotifications : ISonarQubeNotifications
    {
        private readonly ITimer timer;
        private readonly IStateManager stateManager;
        private readonly ISonarQubeService sonarQubeService;
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

        internal SonarQubeNotifications(ISonarQubeService sonarQubeService,
            IStateManager stateManager, INotificationIndicatorViewModel model,
            ITimer timer)
        {
            this.timer = timer;
            this.sonarQubeService = sonarQubeService;
            this.stateManager = stateManager;
            Model = model;

            timer.Elapsed += OnTimerElapsed;
        }

        public async Task StartAsync(NotificationData notificationData)
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

            await UpdateEvents();
            timer.Start();
        }

        public void Stop()
        {
            cancellation.Cancel();

            timer.Stop();
            Model.IsIconVisible = false;
        }

        private async Task UpdateEvents()
        {
            var events = await GetNotificationEvents();
            if (events == null)
            {
                Stop();
                return;
            }
            Model.IsIconVisible = true;
            Model.SetNotificationEvents(events);
        }

        private async void OnTimerElapsed(object sender, EventArgs e)
        {
            await UpdateEvents();
        }

        private async Task<IList<SonarQubeNotification>> GetNotificationEvents()
        {
            //TODO: UNHACK
            //return new SonarQubeNotification[]
            //{
            //    new SonarQubeNotification
            //    {
            //        Category = "Category",
            //        Message = "Quality gate is Red (was Green)",
            //        Link = new Uri("http://www.com"),
            //        Date = DateTimeOffset.Now
            //    },

            //        new SonarQubeNotification
            //    {
            //        Category = "Category",
            //        Message = "You have 17 new issues assigned to you",
            //        Link = new Uri("http://www.com"),
            //        Date = DateTimeOffset.Now.AddMinutes(5)
            //    }
            //};

            var projectKey = stateManager.IsConnected
                ? stateManager?.BoundProjectKey : null;

            if (projectKey == null)
            {
                return new SonarQubeNotification[0];
            }

            var events = await sonarQubeService.GetNotificationEventsAsync(projectKey,
                lastCheckDate, cancellation.Token);

            if (events != null && events.Count > 0)
            {
                lastCheckDate = events.Max(ev => ev.Date);
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
