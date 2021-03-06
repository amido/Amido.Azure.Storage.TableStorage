﻿using System;
using System.Collections.Generic;
using System.Data.Services.Client;
using System.Linq;
using System.Linq.Expressions;
using System.Xml.Serialization;
using Amido.Azure.Storage.TableStorage.Account;
using Amido.Azure.Storage.TableStorage.Dbc;
using Amido.Azure.Storage.TableStorage.Paging;
using Amido.Azure.Storage.TableStorage.Queries;
using Amido.Azure.Storage.TableStorage.Serialization;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;

namespace Amido.Azure.Storage.TableStorage
{
    /// <summary>
    /// Class TableStorageRepository
    /// </summary>
    /// <typeparam name="TEntity">The type of the T entity.</typeparam>
    public class TableStorageRepository<TEntity> : TableServiceContext, ITableStorageRepository<TEntity>, ITableStorageAdminRepository, IWantItAll<TEntity> where TEntity : TableServiceEntity
    {
        private readonly string tableName;
        private readonly CloudTableClient cloudTableClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="TableStorageRepository{TEntity}" /> class.
        /// </summary>
        /// <param name="accountConfiguration">The account configuration.</param>
        public TableStorageRepository(AccountConfiguration<TEntity> accountConfiguration)
            : this(GetCloudStorageAccountByConfigurationSetting(accountConfiguration.AccountName), accountConfiguration.TableName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TableStorageRepository{TEntity}" /> class.
        /// </summary>
        /// <param name="accountConnection">The account connection.</param>
        public TableStorageRepository(AccountConnection<TEntity> accountConnection)
            : this(GetCloudStorageAccountByConnectionString(accountConnection.ConnectionString), accountConnection.TableName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TableStorageRepository{TEntity}" /> class.
        /// </summary>
        /// <param name="accountStorageCredentials">The account storage credentials.</param>
        public TableStorageRepository(AccountStorageCredentials<TEntity> accountStorageCredentials)
            : this(GetCloudStorageAccountByStorageCredentials(accountStorageCredentials), accountStorageCredentials.TableName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TableStorageRepository{TEntity}" /> class.
        /// </summary>
        /// <param name="cloudStorageAccount">The cloud storage account.</param>
        /// <param name="tableName">Name of the table.</param>
        protected TableStorageRepository(CloudStorageAccount cloudStorageAccount, string tableName)
            : base(cloudStorageAccount.TableEndpoint.AbsoluteUri, cloudStorageAccount.Credentials)
        {
            cloudTableClient = cloudStorageAccount.CreateCloudTableClient();
            this.tableName = tableName;
            MergeOption = MergeOption.PreserveChanges;
            IgnoreResourceNotFoundException = true;
        }

        /// <summary>
        /// Queries against a table and returns a <see cref="PagedResults{TEntity}"/> of results.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>PagedResults{`0}.</returns>
        /// <exception cref="PreconditionException">If query object is null.</exception>
        public PagedResults<TEntity> Query(Query<TEntity> query)
        {
            Contract.Requires(query != null, "query is null.");
            var serializer = new XmlSerializer(typeof(ResultContinuation));
            var cloudTableQuery = query.Execute(CreateQuery<TEntity>(tableName)).AsTableServiceQuery();
            var resultContinuation = ResultContinuationSerializer.DeserializeToken(serializer, query.ContinuationTokenString);
            var response = cloudTableQuery.EndExecuteSegmented(cloudTableQuery.BeginExecuteSegmented(resultContinuation, null, null));
            
            return CreatePagedResults(serializer, response);
        }

        /// <summary>
        /// Queries against a table and returns a <see cref="PagedResults{TEntity}" /> of results.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="resultsPerPage">The results per page.</param>
        /// <returns>PagedResults{`0}.</returns>
        /// <exception cref="PreconditionException">If query object is null.</exception>
        public PagedResults<TEntity> Query(Query<TEntity> query, int resultsPerPage) {
            Contract.Requires(query != null, "query is null.");
            Contract.Requires(resultsPerPage > 0, "resultsPerPage is zero or less.");
            var serializer = new XmlSerializer(typeof(ResultContinuation));
            var cloudTableQuery = query.Execute(CreateQuery<TEntity>(tableName), resultsPerPage).AsTableServiceQuery();
            var resultContinuation = ResultContinuationSerializer.DeserializeToken(serializer, query.ContinuationTokenString);
            var response = cloudTableQuery.EndExecuteSegmented(cloudTableQuery.BeginExecuteSegmented(resultContinuation, null, null));

            return CreatePagedResults(serializer, response);
        }

        /// <summary>
        /// Returns the first item matching the query, or null of none found.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>The first item found or null if none.</returns>
        /// <exception cref="PreconditionException">If query object is null.</exception>
        public TEntity FirstOrDefault(Query<TEntity> query)
        {
            Contract.Requires(query != null, "query is null.");
            return query.Execute(CreateQuery<TEntity>(tableName)).FirstOrDefault();
        }

        /// <summary>
        /// Returns the first item matching the query.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>The first item found.</returns>
        /// <exception cref="PreconditionException">If query object is null.</exception>
        /// <exception cref="InvalidOperationException">If not result are found matching the query.</exception>
        public TEntity First(Query<TEntity> query)
        {
            Contract.Requires(query != null, "query is null.");
            return query.Execute(CreateQuery<TEntity>(tableName)).First();
        }

        /// <summary>
        /// Returns an entity based upon partition key and row key.
        /// </summary>
        /// <param name="partitionKey">The partition key.</param>
        /// <param name="rowKey">The row key.</param>
        /// <returns>An instance of <typeparamref name="TEntity"/></returns>
        /// <exception cref="PreconditionException">partitionKey or rowKey are null or empty.</exception>
        public TEntity GetByPartitionKeyAndRowKey(string partitionKey, string rowKey)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(partitionKey), "partitionKey is null.");
            Contract.Requires(!string.IsNullOrWhiteSpace(rowKey), "rowKey is null.");
            return FirstOrDefault(new GetByPartitionKeyAndRowKeyQuery<TEntity>(partitionKey, rowKey));
        }

        /// <summary>
        /// Returns a paged list of results based upon a partition key. If a continuationToken is passed, it will return the next page of results.
        /// </summary>
        /// <param name="partitionKey">The partition key.</param>
        /// <param name="continuationToken">The continuation token.</param>
        /// <returns>PagedResults{TEntity}.</returns>
        /// <exception cref="PreconditionException">If partitionKey is null or empty.</exception>
        public PagedResults<TEntity> ListByPartitionKey(string partitionKey, string continuationToken = null)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(partitionKey), "partitionKey is null.");
            return Query(new ListByPartitionKeyQuery<TEntity>(partitionKey) { ContinuationTokenString = continuationToken });
        }

        /// <summary>
        /// Returns a paged list of results based upon a partition key. The number of results returned can be constrained by passing a value for resultsPerPage.
        /// If a continuationToken is passed, it will return the next page of results.
        /// </summary>
        /// <param name="partitionKey">The partition key.</param>
        /// <param name="resultsPerPage">The results per page.</param>
        /// <param name="continuationToken">The continuation token.</param>
        /// <returns>PagedResults{TEntity}.</returns>
        /// <exception cref="PreconditionException">If partitionKey is null or empty or resultsPerPage is less than one.</exception>
        public PagedResults<TEntity> ListByPartitionKey(string partitionKey, int resultsPerPage, string continuationToken = null)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(partitionKey), "partitionKey is null.");
            return Query(new ListByPartitionKeyQuery<TEntity>(partitionKey) { ContinuationTokenString = continuationToken }, resultsPerPage);
        }

        /// <summary>
        /// Lists all entities within a particular partition. Caution: This method will retrieve ALL entities unpaged.
        /// </summary>
        /// <param name="partitionKey">The partition key.</param>
        /// <returns>A list of entities.</returns>
        public List<TEntity> ListAllByPartitionKey(string partitionKey)
        {
            string continuationToken = null;
            var entities = new List<TEntity>();
            do
            {
                var pagedResults = ListByPartitionKey(partitionKey, continuationToken);
                continuationToken = pagedResults.ContinuationToken;
                entities.AddRange(pagedResults.Results);
            } while (continuationToken != null);

            return entities;
        }

        /// <summary>
        /// Returns a paged list of results. If a continuationToken is passed, it will return the next page of results.
        /// </summary>
        /// <param name="continuationToken">The continuation token.</param>
        /// <returns>PagedResults{TEntity}.</returns>
        public PagedResults<TEntity> ListAll(string continuationToken = null)
        {
            return Query(new ListAllQuery<TEntity> { ContinuationTokenString = continuationToken });
        }

        /// <summary>
        /// Returns a paged list of results. The number of results returned can be constrained by passing a value for resultsPerPage.
        /// If a continuationToken is passed, it will return the next page of results.
        /// </summary>
        /// <param name="resultsPerPage">The result per page.</param>
        /// <param name="continuationToken">The continuation token.</param>
        /// <returns>PagedResults{TEntity}.</returns>
        /// <exception cref="PreconditionException">If resultsPerPage is less than one.</exception>
        public PagedResults<TEntity> ListAll(int resultsPerPage, string continuationToken = null)
        {
            return Query(new ListAllQuery<TEntity> { ContinuationTokenString = continuationToken }, resultsPerPage);
        }

        /// <summary>
        /// Finds results based upon a given expression.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>IQueryable{TEntity}.</returns>
        /// <exception cref="PreconditionException">If expression is null.</exception>
        public IQueryable<TEntity> Find(Expression<Func<TEntity, bool>> expression)
        {
            Contract.Requires(expression!=null, "expression is null.");
            return CreateQuery<TEntity>(tableName).Where(expression);
        }

        /// <summary>
        /// Finds results based upon a given <see cref="Query{TEntity}"/> instance.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>IQueryable{TEntity}.</returns>
        /// <exception cref="PreconditionException">If query is null.</exception>
        public IQueryable<TEntity> Find(Query<TEntity> query)
        {
            Contract.Requires(query != null, "query is null.");
            return query.Execute(CreateQuery<TEntity>(tableName));
        }

        /// <summary>
        /// Finds results based upon a given <see cref="Query{TEntity}"/> instance. Results can be limited by specifying the resultsPerPage to return.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="resultsPerPage">The results per page.</param>
        /// <returns>IQueryable{TEntity}.</returns>
        /// <exception cref="PreconditionException">If query is null or resultsPerPage is less than one.</exception>
        public IQueryable<TEntity> Find(Query<TEntity> query, int resultsPerPage) 
        {
            Contract.Requires(query != null, "query is null.");
            Contract.Requires(resultsPerPage > 0, "resultsPerPage is zero or less.");
            return query.Execute(CreateQuery<TEntity>(tableName), resultsPerPage);
        }

        /// <summary>
        /// Adds the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <exception cref="PreconditionException">If entity is null.</exception>
        public virtual void Add(TEntity entity)
        {
            Contract.Requires(entity != null, "entity is null");
            AddObject(tableName, entity);
        }

        /// <summary>
        /// Updates the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <exception cref="PreconditionException">If entity is null.</exception>
        public virtual void Update(TEntity entity)
        {
            Contract.Requires(entity != null, "entity is null");
            UpdateObject(entity);
        }

        /// <summary>
        /// Attaches the entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        public virtual void AttachEntity(TEntity entity)
        {
            Contract.Requires(entity != null, "entity is null");
            AttachTo(tableName, entity);
        }

        /// <summary>
        /// Attaches the entity for upsert.
        /// </summary>
        /// <param name="entity">The entity.</param>
        public virtual void AttachEntityForUpsert(TEntity entity)
        {
            Contract.Requires(entity != null, "entity is null");
            AttachTo(tableName, entity, null);
        }

        /// <summary>
        /// Detaches the entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        public virtual void DetachEntity(TEntity entity)
        {
            Contract.Requires(entity != null, "entity is null");
            Detach(entity);
        }

        /// <summary>
        /// Deletes the specified entity from the table.
        /// </summary>
        /// <param name="entity">The entity.</param>
        public virtual void Delete(TEntity entity)
        {
            Contract.Requires(entity != null, "entity is null");
            DeleteObject(entity);
        }

        /// <summary>
        /// Saves any pending changes. Calls SaveChangesWithRetries passing SaveChangesOptions.Batch.
        /// </summary>
        public void SaveBatch()
        {
            SaveChangesWithRetries(SaveChangesOptions.Batch);
        }

        /// <summary>
        /// Saves any pending changes. Calls SaveChangesWithRetries passing SaveChangesOptions.ReplaceOnUpdate.
        /// </summary>
        public void SaveAndReplaceOnUpdate()
        {
            SaveChangesWithRetries(SaveChangesOptions.ReplaceOnUpdate);
        }

        /// <summary>
        /// Saves any pending changes. Calls SaveChangesWithRetries without passing any SaveChangesOptions.
        /// </summary>
        /// <remarks>Best used after calling a delete.</remarks>
        public void Save()
        {
            SaveChangesWithRetries();
        }

        /// <summary>
        /// Creates the table if not exists.
        /// </summary>
        public void CreateTableIfNotExists()
        {
            cloudTableClient.CreateTableIfNotExist(tableName);
        }

        /// <summary>
        /// Deletes a table from Table storage.
        /// </summary>
        public void DeleteTable()
        {
            cloudTableClient.DeleteTableIfExist(tableName);
        }

        private static PagedResults<TEntity> CreatePagedResults(XmlSerializer serializer, ResultSegment<TEntity> response)
        {
            var pagedResults = new PagedResults<TEntity>();
            pagedResults.Results.AddRange(response.Results);
            pagedResults.ContinuationToken = response.ContinuationToken == null ? null :  ResultContinuationSerializer.SerializeToken(serializer, response.ContinuationToken);
            pagedResults.HasMoreResults = response.ContinuationToken != null;
            return pagedResults;
        }

        /// <summary>
        /// Gets the cloud storage account by connection string.
        /// </summary>
        /// <param name="storageConnectionString">The storage connection string.</param>
        /// <returns>CloudStorageAccount.</returns>
        /// <exception cref="System.InvalidOperationException">Unable to find cloud storage account</exception>
        protected static CloudStorageAccount GetCloudStorageAccountByConnectionString(string storageConnectionString)
        {
            try
            {
                return CloudStorageAccount.Parse(storageConnectionString);
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Unable to find cloud storage account", error);
            }
        }

        /// <summary>
        /// Gets the cloud storage account by configuration setting.
        /// </summary>
        /// <param name="configurationSetting">The configuration setting.</param>
        /// <returns>CloudStorageAccount.</returns>
        /// <exception cref="System.InvalidOperationException">Unable to find cloud storage account</exception>
        protected static CloudStorageAccount GetCloudStorageAccountByConfigurationSetting(string configurationSetting)
        {           
            try
            {
                return CloudStorageAccount.FromConfigurationSetting(configurationSetting);
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Unable to find cloud storage account", error);
            }
        }

        public static CloudStorageAccount GetCloudStorageAccountByStorageCredentials(AccountStorageCredentials<TEntity> accountStorageCredentials)
        {
            Contract.Assert(!string.IsNullOrEmpty(accountStorageCredentials.AccountName));

            try
            {
                return new CloudStorageAccount(
                    new StorageCredentialsAccountAndKey(
                        accountStorageCredentials.AccountName, accountStorageCredentials.AccountKey), accountStorageCredentials.UseHttps);
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Unable to find cloud storage account", error);
            }
        }
    }
}