﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SystemInterface.Timers;

namespace SonarLint.VisualStudio.Integration.Vsix.Notifications.UnitTests
{
    [TestClass]
    public class SonarQubeNotifications_OnTimerElapsedTests
    {
        private Mock<INotifyIconFactory> notifyIconFactoryMock;
        private Mock<ITimerFactory> timerFactoryMock;

        private Mock<INotifyIcon> notifyIconMock;
        private Mock<ITimer> timerMock;

        static SonarQubeNotifications_OnTimerElapsedTests()
        {
            // https://stackoverflow.com/questions/6005398/uriformatexception-invalid-uri-invalid-port-specified
            if (!UriParser.IsKnownScheme("pack"))
            {
                new System.Windows.Application();
            }
        }

        [TestInitialize]
        public void TestInitialize()
        {
            timerMock = new Mock<ITimer>();
            notifyIconMock = new Mock<INotifyIcon>();

            timerFactoryMock = new Mock<ITimerFactory>();
            timerFactoryMock
                .Setup(mock => mock.Create())
                .Returns(timerMock.Object);

            notifyIconFactoryMock = new Mock<INotifyIconFactory>();
            notifyIconFactoryMock
               .Setup(mock => mock.Create())
               .Returns(notifyIconMock.Object);
        }

        [TestMethod]
        public void NotifyIcon_Click_ShowsBalloonTip()
        {
            // Arrange
            notifyIconMock
                .Setup(mock => mock.ShowBalloonTip(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()));

            var notifications = new SonarQubeNotifications(notifyIconFactoryMock.Object, timerFactoryMock.Object);
            notifications.Start();

            // Act
            notifyIconMock.Raise(mock => mock.Click += null, EventArgs.Empty);

            // Assert
            notifyIconMock.VerifyAll();
        }
    }
}