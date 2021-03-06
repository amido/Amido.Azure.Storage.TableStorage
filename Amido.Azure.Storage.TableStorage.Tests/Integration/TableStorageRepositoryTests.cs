﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Amido.Azure.Storage.TableStorage.Account;
using Amido.Azure.Storage.TableStorage.Dbc;
using Amido.Azure.Storage.TableStorage.Queries;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Amido.Azure.Storage.TableStorage.Tests.Integration
{
    [TestClass]
    public class TableStorageRepositoryTests
    {
        public ITableStorageRepository<TestEntity> Repository;

        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext testContext) {
            var process = Process.Start(@"C:\Program Files\Microsoft SDKs\Windows Azure\Emulator\csrun", "/devstore");
            if(process != null) {
                process.WaitForExit();
            }
            else {
                throw new ApplicationException("Unable to start storage emulator.");
            }
        }

        [TestInitialize]
        public void Initialize()
        {
            var accountConnection = new AccountConnection<TestEntity>(Properties.Resources.AccountConnectionString);

            Repository = new TableStorageRepository<TestEntity>(accountConnection);
            ((ITableStorageAdminRepository)Repository).CreateTableIfNotExists();
        }
        
        [TestCleanup]
        public void Cleanup()
        {
            ((ITableStorageAdminRepository)Repository).DeleteTable();
        } 

        [TestClass]
        public class Query : TableStorageRepositoryTests
        {
            public class PagingTestQuery : Query<TestEntity> {
                public override IQueryable<TestEntity> Execute(IQueryable<TestEntity> query) 
                {
                    return query.Take(5);
                }

                public override IQueryable<TestEntity> Execute(IQueryable<TestEntity> query, int resultsPerPage)
                {
                    return query.Take(resultsPerPage);
                }
            }

            [TestMethod]
            [ExpectedException(typeof(PreconditionException))]
            public void Should_Throw_PreconditionException() 
            {
                // Act
                var results = Repository.Query(null);

                // Assert
                Assert.IsNull(results);
            }
            
            [TestMethod]
            public void Should_Return_First_Five_Rows()
            {
                // Arrange
                for (var i = 0; i < 50; i++)
                {
                    Repository.Add(new TestEntity("PartitionKey1", "RowKey" + i));
                    Repository.SaveBatch();
                }

                // Act
                var results = Repository.Query(new PagingTestQuery());

                // Assert
                Assert.IsNotNull(results);
                Assert.AreEqual(5, results.Results.Count);
            }

            [TestMethod]
            public void Should_Return_First_Five_Rows_Across_Partition_Keys()
            {
                // Arrange
                for (var i = 0; i < 50; i++)
                {
                    Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + i));
                    Repository.SaveBatch();
                }

                // Act
                var results = Repository.Query(new PagingTestQuery());

                // Assert
                Assert.IsNotNull(results);
                Assert.AreEqual(5, results.Results.Count);
            }

            [TestMethod]
            public void Should_Return_Continuation_Token_When_More_Rows_Exist()
            {
                // Arrange
                for (var i = 0; i < 100; i++)
                {
                    for (var j = 0; j < 20; j++)
                    {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                // Act
                var results = Repository.Query(new PagingTestQuery());

                // Assert
                Assert.IsNotNull(results);
                Assert.AreEqual(5, results.Results.Count);
                Assert.IsNotNull(results.ContinuationToken);
                Assert.IsTrue(results.HasMoreResults);
                Assert.IsTrue(results.ContinuationToken.Contains("ResultContinuation"));
                Assert.IsTrue(results.ContinuationToken.Contains("Table"));
                Assert.IsTrue(results.ContinuationToken.Contains("NextPartitionKey"));
                Assert.IsTrue(results.ContinuationToken.Contains("NextRowKey"));
            }

            [TestMethod]
            public void Should_Return_Next_Rows_Using_Valid_Continuation()
            {
                // Arrange
                for (var i = 0; i < 100; i++)
                {
                    for (var j = 0; j < 20; j++)
                    {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                var firstResults = Repository.Query(new PagingTestQuery());

                // Assert
                Assert.IsNotNull(firstResults);

                // Act
                var secondResults = Repository.Query(new PagingTestQuery {ContinuationTokenString = firstResults.ContinuationToken});

                // Assert
                Assert.IsNotNull(secondResults);
                Assert.AreEqual(5, secondResults.Results.Count);

                foreach (var secondResult in secondResults.Results)
                {
                    Assert.IsTrue(!firstResults.Results.Contains(secondResult));
                }
            }
        }

        [TestClass]
        public class FirstOrDefault : TableStorageRepositoryTests
        {
            [TestMethod]
            [ExpectedException(typeof(PreconditionException))]
            public void Should_Throw_PreconditionException() {
                
                // Act
                var results = Repository.FirstOrDefault(null);

                // Assert
                Assert.IsNull(results);
            }
            
            [TestMethod]
            public void Should_Return_Null_As_Query_Returns_No_Data()
            {
                // Act
                var result = Repository.FirstOrDefault(new ListByPartitionKeyQuery<TestEntity>(Guid.NewGuid().ToString()));

                // Assert
                Assert.IsNull(result);
            }

            [TestMethod]
            public void Should_Return_First_Row()
            {
                // Arrange
                for (var i = 0; i < 50; i++)
                {
                    Repository.Add(new TestEntity("PartitionKey1", "RowKey" + i));
                    Repository.SaveBatch();
                }

                // Act
                var result = Repository.FirstOrDefault(new ListByPartitionKeyQuery<TestEntity>("PartitionKey1"));

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual("PartitionKey1", result.PartitionKey);
            }

            [TestMethod]
            public void Should_Return_First_Row_Of_Paged_Results() {
                // Arrange
                for(var i = 0; i < 50; i++) {
                    Repository.Add(new TestEntity("PartitionKey1", "RowKey" + i));
                    Repository.SaveBatch();
                }

                // Act
                var result = Repository.FirstOrDefault(new ListByPartitionKeyQuery<TestEntity>("PartitionKey1"));

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual("PartitionKey1", result.PartitionKey);
            }
        }

        [TestClass]
        public class First : TableStorageRepositoryTests
        {
            [TestMethod, ExpectedException(typeof(InvalidOperationException))]
            public void Should_Throw_Exception_When_No_Data_Is_Returned()
            {
                // Act - Assert
                Repository.First(new ListByPartitionKeyQuery<TestEntity>(Guid.NewGuid().ToString()));
            }

            [TestMethod]
            [ExpectedException(typeof(PreconditionException))]
            public void Should_Throw_PreconditionException() {

                // Act
                var results = Repository.First(null);

                // Assert
                Assert.IsNull(results);
            }

            [TestMethod]
            public void Should_Return_First_Row()
            {
                // Arrange
                for (var i = 0; i < 50; i++)
                {
                    Repository.Add(new TestEntity("PartitionKey1", "RowKey" + i));
                    Repository.SaveBatch();
                }

                // Act
                var result = Repository.First(new ListByPartitionKeyQuery<TestEntity>("PartitionKey1"));

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual("PartitionKey1", result.PartitionKey);
            }

            [TestMethod]
            public void Should_Return_First_Row_Of_Paged_Results() {
                // Arrange
                for(var i = 0; i < 50; i++) {
                    Repository.Add(new TestEntity("PartitionKey1", "RowKey" + i));
                    Repository.SaveBatch();
                }

                // Act
                var result = Repository.First(new ListByPartitionKeyQuery<TestEntity>("PartitionKey1"));

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual("PartitionKey1", result.PartitionKey);
            }
        }

        [TestClass]
        public class GetByPartitionKeyAndRowKey : TableStorageRepositoryTests
        {
            [TestMethod]
            [ExpectedException(typeof(PreconditionException))]
            public void Should_Throw_PreconditionException_If_PartionKey_Null() 
            {
                // Act
                var result = Repository.GetByPartitionKeyAndRowKey(Guid.NewGuid().ToString(), null);

                // Assert
                Assert.IsNull(result);
            }

            [TestMethod]
            [ExpectedException(typeof(PreconditionException))]
            public void Should_Throw_PreconditionException_If_RowKey_Null() 
            {
                // Act
                var result = Repository.GetByPartitionKeyAndRowKey(null, Guid.NewGuid().ToString());

                // Assert
                Assert.IsNull(result);
            }
            
            [TestMethod]
            public void Should_Return_Null_As_Query_Returns_No_Data()
            {
                // Act
                var result = Repository.GetByPartitionKeyAndRowKey(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

                // Assert
                Assert.IsNull(result);
            }

            [TestMethod]
            public void Should_Return_First_Row()
            {
                // Arrange
                for (var i = 0; i < 50; i++)
                {
                    Repository.Add(new TestEntity("PartitionKey1", "RowKey" + i));
                    Repository.SaveBatch();
                }

                // Act
                var result = Repository.GetByPartitionKeyAndRowKey("PartitionKey1", "RowKey45");

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual("PartitionKey1", result.PartitionKey);
                Assert.AreEqual("RowKey45", result.RowKey);
            }
        }

        [TestClass]
        public class ListByPartitionKey : TableStorageRepositoryTests
        {
            [TestMethod]
            [ExpectedException(typeof(PreconditionException))]
            public void Should_Throw_PreconditionException_If_PartitionKey_Null() 
            {
                // Act
                var result = Repository.ListByPartitionKey(null);

                // Assert
                Assert.IsNull(result);
            }
            
            [TestMethod]
            public void Should_Return_Empty_List_When_Partition_Key_Does_Not_Have_Rows()
            {
                // Act
                var result = Repository.ListByPartitionKey(Guid.NewGuid().ToString());

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual(0, result.Results.Count);
            }

            [TestMethod]
            public void Should_Return_Valid_Rows()
            {
                // Arrange
                for (var i = 0; i < 10; i++)
                {
                    for (var j = 0; j < 10; j++)
                    {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                // Act
                var result = Repository.ListByPartitionKey("PartitionKey8");

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual(10, result.Results.Count);
                Assert.IsFalse(result.HasMoreResults);
                Assert.IsTrue(string.IsNullOrEmpty(result.ContinuationToken));    
                Assert.IsTrue(result.Results.All(x => x.PartitionKey == "PartitionKey8"));
            }

            [TestMethod]
            public void Should_Return_Valid_Rows_PagedResults() {
                // Arrange
                const string partiionKey = "PartitionKey0";
                const int resultPerPage = 20;
                
                for(var i = 0; i < 2; i++) {
                    for(var j = 0; j < 10; j++) {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                // Act
                var result = Repository.ListByPartitionKey(partiionKey, resultPerPage);

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual(10, result.Results.Count);
                Assert.IsFalse(result.HasMoreResults);
                Assert.IsTrue(string.IsNullOrWhiteSpace(result.ContinuationToken));
                Assert.IsTrue(result.Results.All(x => x.PartitionKey == partiionKey));
            }

            [TestMethod]
            public void Should_Return_Valid_Rows_PagedResults_WithMore() {
                // Arrange
                const string partiionKey = "PartitionKey0";
                const int resultPerPage = 5;

                for(var i = 0; i < 2; i++) {
                    for(var j = 0; j < 10; j++) {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                // Act
                var result = Repository.ListByPartitionKey(partiionKey, resultPerPage);

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual(resultPerPage, result.Results.Count);
                Assert.IsTrue(result.HasMoreResults);
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.ContinuationToken));
                Assert.IsTrue(result.Results.All(x => x.PartitionKey == partiionKey));
            }
        }

        [TestClass]
        public class ListAllByPartitionKey : TableStorageRepositoryTests
        {
            [TestMethod]
            [ExpectedException(typeof(PreconditionException))]
            public void Should_Throw_PreconditionException_If_PartitionKey_Null()
            {
                // Act
                var result = ((IWantItAll<TestEntity>)Repository).ListAllByPartitionKey(null);

                // Assert
                Assert.IsNull(result);
            }

            [TestMethod]
            public void Should_Return_Empty_List_When_Partition_Key_Does_Not_Have_Rows()
            {
                // Act
                var result = ((IWantItAll<TestEntity>)Repository).ListAllByPartitionKey(Guid.NewGuid().ToString());

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual(0, result.Count);
            }

            [TestMethod]
            public void Should_Return_Valid_Rows()
            {
                // Arrange
                for (var i = 0; i < 1001; i++)
                {
                    Repository.Add(new TestEntity("PartitionKey1", "RowKey" + i));
                    Repository.SaveBatch();
                }

                // Act
                var results = ((IWantItAll<TestEntity>)Repository).ListAllByPartitionKey("PartitionKey1");

                // Assert
                Assert.IsNotNull(results);
                Assert.AreEqual(1001, results.Count);
                Assert.IsTrue(results.All(x => x.PartitionKey == "PartitionKey1"));
            }
        }

        [TestClass]
        public class ListAll : TableStorageRepositoryTests
        {
            [TestMethod]
            [ExpectedException(typeof(PreconditionException))]
            public void Should_Throw_PreconditionException_Invalid_ResultPerPage() {
                // Arrange
                for(var i = 0; i < 10; i++) {
                    Repository.Add(new TestEntity("PartitionKey1", "RowKey" + i));
                    Repository.SaveBatch();
                }

                // Act
                var results = Repository.ListAll(0);

                // Assert
                Assert.IsNotNull(results);
                Assert.AreEqual(50, results.Results.Count);
            }
            
            [TestMethod]
            public void Should_Return_All_Rows_In_Partition()
            {
                // Arrange
                for (var i = 0; i < 50; i++)
                {
                    Repository.Add(new TestEntity("PartitionKey1", "RowKey" + i));
                    Repository.SaveBatch();
                }

                // Act
                var results = Repository.ListAll();

                // Assert
                Assert.IsNotNull(results);
                Assert.AreEqual(50, results.Results.Count);
            }

            [TestMethod]
            public void Should_Return_All_Rows_Across_Partition_Keys()
            {
                // Arrange
                const int resultCount = 50;
                for(var i = 0; i < resultCount; i++)
                {
                    Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + i));
                    Repository.SaveBatch();
                }

                // Act
                var results = Repository.ListAll();

                // Assert
                Assert.IsNotNull(results);
                Assert.AreEqual(resultCount, results.Results.Count);
                Assert.IsFalse(results.HasMoreResults);
                Assert.IsTrue(string.IsNullOrEmpty(results.ContinuationToken));  
            }

            [TestMethod]
            public void Should_Return_Continuation_Token_When_Item_Count_Greater_Than_1000()
            {
                // Arrange
                for (var i = 0; i < 100; i++)
                {
                    for (var j = 0; j < 20; j++)
                    {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                // Act
                var results = Repository.ListAll();

                // Assert
                Assert.IsNotNull(results);
                Assert.AreEqual(1000, results.Results.Count);
                Assert.IsNotNull(results.ContinuationToken);
                Assert.IsTrue(results.ContinuationToken.Contains("ResultContinuation"));
                Assert.IsTrue(results.ContinuationToken.Contains("Table"));
                Assert.IsTrue(results.ContinuationToken.Contains("NextPartitionKey"));
                Assert.IsTrue(results.ContinuationToken.Contains("NextRowKey"));
            }

            [TestMethod]
            public void Should_Return_Continuation_Token_PagedResults() 
            {
                // Arrange
                const int resultPerPage = 20;

                // Arrange
                for(var i = 0; i < 10; i++) 
                {
                    for(var j = 0; j < 10; j++) 
                    {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                // Act
                var result = Repository.ListAll(resultPerPage);

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual(resultPerPage, result.Results.Count);
                Assert.IsTrue(result.HasMoreResults);
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.ContinuationToken));
                Assert.IsTrue(result.ContinuationToken.Contains("ResultContinuation"));
                Assert.IsTrue(result.ContinuationToken.Contains("Table"));
                Assert.IsTrue(result.ContinuationToken.Contains("NextPartitionKey"));
                Assert.IsTrue(result.ContinuationToken.Contains("NextRowKey"));
            }

            [TestMethod]
            public void Should_Not_Return_Continuation_Token_PagedResults_SecondCall() 
            {
                // Arrange
                const int resultPerPage = 20;

                // Arrange
                // Add thirty items to table
                for(var i = 0; i < 3; i++) 
                {
                    for(var j = 0; j < 10; j++) 
                    {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                // Act
                var result = Repository.ListAll(resultPerPage);

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual(resultPerPage, result.Results.Count);
                Assert.IsTrue(result.HasMoreResults);
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.ContinuationToken));
                Assert.IsTrue(result.ContinuationToken.Contains("ResultContinuation"));
                Assert.IsTrue(result.ContinuationToken.Contains("Table"));
                Assert.IsTrue(result.ContinuationToken.Contains("NextPartitionKey"));
                Assert.IsTrue(result.ContinuationToken.Contains("NextRowKey"));

                // Act
                result = Repository.ListAll(resultPerPage, result.ContinuationToken);

                // Assert
                Assert.IsNotNull(result);
                Assert.AreNotEqual(resultPerPage, result.Results.Count);
                Assert.IsFalse(result.HasMoreResults);
                Assert.IsTrue(string.IsNullOrWhiteSpace(result.ContinuationToken));
            }
        }

        [TestClass]
        public class Find : TableStorageRepositoryTests
        {
            [TestMethod]
            [ExpectedException(typeof(PreconditionException))]
            public void Should_Throw_PreconditionException_If_Query_Null() 
            {
                // Act
                var results = new List<TestEntity>(Repository.Find((Query<TestEntity>)null));

                // Assert
                Assert.IsNull(results);
            }
            
            [TestMethod]
            public void Should_Return_Expected_Rows_From_Expression()
            {
                // Arrange
                for (var i = 0; i < 10; i++)
                {
                    for (var j = 0; j < 10; j++)
                    {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                // Act
                var results = new List<TestEntity>(Repository.Find(x => x.PartitionKey == "PartitionKey2"));

                // Assert
                Assert.IsNotNull(results);
                Assert.AreEqual(10, results.Count);
            }

            [TestMethod]
            public void Should_Return_Expected_Rows_From_Query()
            {
                // Arrange
                for (var i = 0; i < 10; i++)
                {
                    for (var j = 0; j < 10; j++)
                    {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                // Act
                var results = new List<TestEntity>(Repository.Find(new ListByPartitionKeyQuery<TestEntity>("PartitionKey2")));

                // Assert
                Assert.IsNotNull(results);
                Assert.AreEqual(10, results.Count);
            }

            [TestMethod]
            public void Should_Not_Return_Rows_From_Query() {
                // Arrange
                for(var i = 0; i < 10; i++) {
                    for(var j = 0; j < 10; j++) {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                // Act
                var results = new List<TestEntity>(Repository.Find(new ListByPartitionKeyQuery<TestEntity>("PartitionKey25")));

                // Assert
                Assert.IsNotNull(results);
                Assert.AreEqual(0, results.Count);
            }

            [TestMethod]
            public void Should_Return_Expected_Row_From_Query() {
                // Arrange
                for(var i = 0; i < 10; i++) {
                    for(var j = 0; j < 10; j++) {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                // Act
                var results = new List<TestEntity>(Repository.Find(new GetByPartitionKeyAndRowKeyQuery<TestEntity>("PartitionKey2", "RowKey2")));

                // Assert
                Assert.IsNotNull(results);
                Assert.AreEqual(1, results.Count);
            }

            [TestMethod]
            public void Should_Not_Return_Row_From_Query() {
                // Arrange
                for(var i = 0; i < 10; i++) {
                    for(var j = 0; j < 10; j++) {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                // Act
                var results = new List<TestEntity>(Repository.Find(new GetByPartitionKeyAndRowKeyQuery<TestEntity>("PartitionKey2", "RowKey25")));

                // Assert
                Assert.IsNotNull(results);
                Assert.AreEqual(0, results.Count);
            }

            [TestMethod]
            [ExpectedException(typeof(PreconditionException))]
            public void Should_Throw_PreconditionException() {
                // Arrange
                for(var i = 0; i < 10; i++) {
                    for(var j = 0; j < 10; j++) {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                // Act
                var results = new List<TestEntity>(Repository.Find(new ListByPartitionKeyQuery<TestEntity>("PartitionKey2"), 0));

                // Assert
                Assert.IsNotNull(results);
                Assert.AreEqual(5, results.Count);
            }

            [TestMethod]
            public void Should_Return_Expected_Number_Of_Rows_From_Query() {
                // Arrange
                for(var i = 0; i < 10; i++) {
                    for(var j = 0; j < 10; j++) {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                // Act
                var results = new List<TestEntity>(Repository.Find(new ListByPartitionKeyQuery<TestEntity>("PartitionKey2"), 5));

                // Assert
                Assert.IsNotNull(results);
                Assert.AreEqual(5, results.Count);
            }
        }

        [TestClass]
        public class Delete : TableStorageRepositoryTests 
        {
            [TestMethod]
            [ExpectedException(typeof(PreconditionException))]
            public void Should_Throw_PreconditionException_If_Entity_Null() 
            {
                // Act
                Repository.Delete(null); ;

                // Assert
                Assert.IsNull(null);
            }
            
            [TestMethod]
            public void Should_Delete_Entity_If_Present() 
            {
                // Arrange
                for(var i = 0; i < 1; i++) {
                    for(var j = 0; j < 10; j++) {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j));
                    }
                    Repository.SaveBatch();
                }

                // Act
                var result = Repository.GetByPartitionKeyAndRowKey("PartitionKey0", "RowKey1");

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual("PartitionKey0", result.PartitionKey);
                Assert.AreEqual("RowKey1", result.RowKey);

                // Act
                Repository.Delete(result);
                Repository.SaveBatch();

                result = Repository.GetByPartitionKeyAndRowKey("PartitionKey0", "RowKey1");

                // Assert
                Assert.IsNull(result);
            }
        }

        [TestClass]
        public class Update : TableStorageRepositoryTests 
        {
            [TestMethod]
            public void Should_Update_Entity_If_Present() 
            {
                // Arrange
                for(var i = 0; i < 1; i++) {
                    for(var j = 0; j < 10; j++) {
                        Repository.Add(new TestEntity("PartitionKey" + i, "RowKey" + j) {TestValue = "Created"});
                    }
                    Repository.SaveBatch();
                }

                // Act
                var result = Repository.GetByPartitionKeyAndRowKey("PartitionKey0", "RowKey1");

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual("PartitionKey0", result.PartitionKey);
                Assert.AreEqual("RowKey1", result.RowKey);

                var current = result.TestValue;

                // Act
                result.TestValue = "Updated";
                Repository.Update(result);
                Repository.SaveAndReplaceOnUpdate();

                result = Repository.GetByPartitionKeyAndRowKey("PartitionKey0", "RowKey1");

                // Assert
                Assert.IsNotNull(result);
                Assert.IsTrue(result.TestValue == "Updated");
                Assert.AreNotEqual(result.TestValue, current);
            }
        }
    }
}