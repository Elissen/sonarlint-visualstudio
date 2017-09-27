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
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarLint.VisualStudio.Integration.State;
using SonarLint.VisualStudio.Integration.UnitTests;
using SonarQube.Client.Messages;
using SonarQube.Client.Models;
using SonarQube.Client.Services;

namespace SonarLint.VisualStudio.Integration.Notifications.UnitTests
{
    [TestClass]
    public class SonarQubeNotificationsTests
    {
        private IStateManager stateManager;
        private Mock<ITimer> timerMock;
        private Mock<INotificationIndicatorViewModel> modelMock;

        private async Task<ISonarQubeService> GetConnectedService()
        {
            var expectedEvent = new NotificationsResponse
            {
                Category = "QUALITY_GATE",
                Link = new Uri("http://foo.com"),
                Date = new DateTimeOffset(2010, 1, 1, 14, 59, 59, TimeSpan.FromHours(2)),
                Message = "foo",
                Project = "test"
            };

            var successResponse = new HttpResponseMessage(HttpStatusCode.OK);
            var client = new Mock<ISonarQubeClient>();
            client.Setup(x => x.ValidateCredentialsAsync(It.IsAny<ConnectionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new Result<CredentialResponse>(successResponse, new CredentialResponse { IsValid = true }));

            client.Setup(x => x.GetVersionAsync(It.IsAny<ConnectionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new Result<VersionResponse>(successResponse, new VersionResponse { Version = "6.6" }));

            client.Setup(x => x.GetNotificationEventsAsync(It.IsAny<ConnectionRequest>(),
                It.IsAny<NotificationsRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new Result<NotificationsResponse[]>(successResponse, new[] { expectedEvent }));

            var service = new SonarQubeService(client.Object);
            await service.ConnectAsync(new ConnectionInformation(new Uri("http://mysq.com")), CancellationToken.None);
            return service;
        }

        [TestInitialize]
        public void TestInitialize()
        {
            timerMock = new Mock<ITimer>();
            modelMock = new Mock<INotificationIndicatorViewModel>();
            modelMock.SetupAllProperties();

            stateManager = new ConfigurableStateManager();
            var connection = new ConnectionInformation(new Uri("http://127.0.0.1"));
            var projects = new SonarQubeProject[] { new SonarQubeProject("test", "test") };
            stateManager.SetProjects(connection, projects);
            (stateManager as ConfigurableStateManager).ConnectedServers.Add(connection);

        }

        [TestMethod]
        public async Task Start_Sets_IsVisible()
        {
            // Arrange
            timerMock.Setup(mock => mock.Start());
            var sqService = await GetConnectedService();

            var model = modelMock.Object;
            var notifications = new SonarQubeNotifications(sqService, stateManager,
               model, timerMock.Object);

            // Act
            await notifications.StartAsync(null);

            // Assert
            model.IsIconVisible.Should().BeTrue();
        }

        [TestMethod]
        public async Task Test_DefaultNotificationDate_IsOneDayAgo()
        {
            // Arrange
            var sqService = await GetConnectedService();
            using (var notifications = new SonarQubeNotifications(sqService, stateManager,
                modelMock.Object, timerMock.Object))
            {
                await notifications.StartAsync(null);

                // Assert
                AreDatesEqual(notifications.GetNotificationData().LastNotificationDate,
                    DateTimeOffset.Now.AddDays(-1), TimeSpan.FromMinutes(1)).Should().BeTrue();
            }
        }

        [TestMethod]
        public async Task Test_OldNotificationDate_IsSetToOneDayAgo()
        {
            // Arrange
            var sqService = await GetConnectedService();
            using (var notifications = new SonarQubeNotifications(sqService, stateManager,
                modelMock.Object, timerMock.Object))
            {
                var date = new NotificationData
                {
                    IsEnabled = true,
                    LastNotificationDate =
                       new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(1))
                };

                await notifications.StartAsync(date);

                // Assert
                AreDatesEqual(notifications.GetNotificationData().LastNotificationDate,
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
