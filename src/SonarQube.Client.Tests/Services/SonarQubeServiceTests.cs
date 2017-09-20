using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarQube.Client.Services.Tests
{
    [TestClass]
    public class SonarQubeServiceTests
    {
        [TestMethod]
        public void Ctor_WithNullClietn_ThrowsArgumentNullException()
        {
            // Arrange & Act
            Action action = () => new SonarQubeService(null);

            // Assert
            action.ShouldThrow<ArgumentNullException>();
        }
    }
}
