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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarLint.VisualStudio.Integration.Service;
using SonarLint.VisualStudio.Integration.State;
using SonarLint.VisualStudio.Integration.UnitTests;

namespace SonarLint.VisualStudio.Integration.Notifications.UnitTests
{
    [TestClass]
    public class SonarQubeNotifications_Start
    {
        private ISonarQubeServiceWrapper sqService;
        private IStateManager stateManager;
        private Mock<ITimer> timerMock;

        [TestInitialize]
        public void TestInitialize()
        {
            sqService = new ConfigurableSonarQubeServiceWrapper();
            stateManager = new ConfigurableStateManager();
            var connection = new ConnectionInformation(new Uri("http://127.0.0.1"));
            var projects = new ProjectInformation[] { new ProjectInformation(), new ProjectInformation() };
            stateManager.SetProjects(connection, projects);
            (stateManager as ConfigurableStateManager).ConnectedServers.Add(connection);

            timerMock = new Mock<ITimer>();
        }

        [TestMethod]
        public void Start_Sets_IsVisible_Timer_Start()
        {
            // Arrange
            timerMock.Setup(mock => mock.Start());

            var notifications = new SonarQubeNotifications(sqService, stateManager, timerMock.Object);
            notifications.IsVisible.Should().BeFalse(); // check default value

            // Act
            notifications.Start(null);

            // Assert
            timerMock.VerifyAll();
            notifications.IsVisible.Should().BeTrue();
        }


        [TestMethod]
        public void Test_DefaultNotificationDate_IsOneDayAgo()
        {
            // Arrange
            using (var notifications = new SonarQubeNotifications(sqService, stateManager,
                timerMock.Object))
            {
                notifications.Start(null);

                // Assert
                AreDatesEqual(notifications.NotificationData.LastNotificationDate,
                    DateTimeOffset.Now.AddDays(-1), TimeSpan.FromMinutes(1)).Should().BeTrue();
            }
        }

        [TestMethod]
        public void Test_OldNotificationDate_IsSetToOneDayAgo()
        {
            // Arrange
            using (var notifications = new SonarQubeNotifications(sqService, stateManager,
                timerMock.Object))
            {
                var date = new NotificationData
                {
                    IsEnabled = true,
                    LastNotificationDate =
                       new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(1))
                };

                notifications.Start(date);

                // Assert
                AreDatesEqual(notifications.NotificationData.LastNotificationDate,
                    DateTimeOffset.Now.AddDays(-1), TimeSpan.FromMinutes(1)).Should().BeTrue();
            }
        }

        private static bool AreDatesEqual(DateTimeOffset date1, DateTimeOffset date2,
            TimeSpan allowedDifference)
        {
            var diff = date1 - date2;
            return diff.Duration() < allowedDifference;
        }
    }
}
