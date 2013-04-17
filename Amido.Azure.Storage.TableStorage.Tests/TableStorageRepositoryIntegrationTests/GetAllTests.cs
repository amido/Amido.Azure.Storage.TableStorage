﻿using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Amido.Azure.Storage.TableStorage.Tests.TableStorageRepositoryIntegrationTests
{
    [TestClass]
    public class GetAllTests : TableStorageRepositoryTestBase
    {
        [TestMethod]
        public void Should_Return_All_Entities_Accross_Partitions_When_Item_Count_Greater_Than_1000()
        {
            // Arrange
            for (var i = 0; i < 22; i++)
            {
                for (var j = 0; j < 100; j++)
                {
                    Repository.BatchWriter.Insert(new TestEntity("PartitionKey" + i, "RowKey" + j));
                }
            }
            Repository.BatchWriter.Execute();

            // Act
            var results = Repository.GetAll().ToList();

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(2200, results.Count());
            Assert.AreEqual(100, results.Count(x => x.PartitionKey == "PartitionKey0"));
            Assert.AreEqual(100, results.Count(x => x.PartitionKey == "PartitionKey21"));
        }
    }
}