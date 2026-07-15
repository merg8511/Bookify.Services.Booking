using Bookify.Services.Booking.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace Bookify.Services.Booking.Domain.Tests.Shared
{
    public sealed class ResultTests
    {
        [Fact]
        public void Success_ShouldCreateSuccessfulResult()
        {
            // ACT
            var result = Result.Success();

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
            Assert.Equal(Error.None, result.Error);
        }

        [Fact]
        public void Failure_ShouldCreateFailedResult()
        {
            //ARRANGE
            var error = Error.Validation(
                "Test.InvalidValue",
                "The value is invalid.");

            //ACT
            var result = Result.Failure(error);

            //ASSERT
            Assert.True(result.IsFailure);
            Assert.False(result.IsSuccess);
            Assert.Equal(error, result.Error);
        }

        [Fact]
        public void GenericSuccess_ShouldContainValue()
        {
            // ARRANGE
            const int expectedValue = 10;

            // ACT
            var result = Result<int>.Success(expectedValue);

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.Equal(expectedValue, result.Value);
        }

        [Fact]
        public void AccessingValueOfFailedResult_ShouldThrow()
        {
            // Arrange
            var error = Error.Validation(
                "Test.InvalidValue",
                "The value is invalid.");

            var result = Result<int>.Failure(error);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => result.Value);
        }
    }
}
