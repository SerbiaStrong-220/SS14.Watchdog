using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using SS14.Watchdog.Components.ServerManagement;
using SS14.Watchdog.Controllers;

namespace SS14.Watchdog.Tests.Controllers
{
    public class InstanceControllerTest
    {
        [Test]
        public void TestCommandAuthorizationSuccess()
        {
            // Arrange
            const string commandToken = "secure that console";

            var instanceMock = new Mock<IServerInstance>();
            instanceMock.SetupGet(p => p.CommandToken).Returns(commandToken);

            var controller = new InstanceController(Mock.Of<IServerManager>());

            // Act
            var success = controller.TryAuthorizeCommand(commandToken, instanceMock.Object, out var failure);

            // Assert
            Assert.That(success);
            Assert.That(failure, Is.Null);
        }

        [Test]
        public void TestCommandAuthorizationMissingConfiguredToken()
        {
            // Arrange
            var instanceMock = new Mock<IServerInstance>();
            var controller = new InstanceController(Mock.Of<IServerManager>());

            // Act
            var success = controller.TryAuthorizeCommand("anything", instanceMock.Object, out var failure);

            // Assert
            Assert.That(success, Is.False);
            Assert.That(failure, Is.TypeOf<ConflictObjectResult>());
        }

        [Test]
        public void TestCommandAuthorizationMissingHeader()
        {
            // Arrange
            var instanceMock = new Mock<IServerInstance>();
            instanceMock.SetupGet(p => p.CommandToken).Returns("secure that console");

            var controller = new InstanceController(Mock.Of<IServerManager>());

            // Act
            var success = controller.TryAuthorizeCommand(null, instanceMock.Object, out var failure);

            // Assert
            Assert.That(success, Is.False);
            Assert.That(failure, Is.TypeOf<UnauthorizedResult>());
        }

        [Test]
        public void TestCommandAuthorizationWrongLengthDoesNotThrow()
        {
            // Arrange
            var instanceMock = new Mock<IServerInstance>();
            instanceMock.SetupGet(p => p.CommandToken).Returns("secure that console");

            var controller = new InstanceController(Mock.Of<IServerManager>());

            // Act
            var success = controller.TryAuthorizeCommand("bad", instanceMock.Object, out var failure);

            // Assert
            Assert.That(success, Is.False);
            Assert.That(failure, Is.TypeOf<UnauthorizedResult>());
        }
    }
}
