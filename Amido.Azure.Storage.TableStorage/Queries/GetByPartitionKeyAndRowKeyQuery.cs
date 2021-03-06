﻿using System.Linq;
using Microsoft.WindowsAzure.StorageClient;

namespace Amido.Azure.Storage.TableStorage.Queries
{
    internal class GetByPartitionKeyAndRowKeyQuery<TEntity> : Query<TEntity> where TEntity : TableServiceEntity
    {
        private readonly string partitionKey;
        private readonly string rowKey;

        public GetByPartitionKeyAndRowKeyQuery(string partitionKey, string rowKey)
        {
            this.partitionKey = partitionKey;
            this.rowKey = rowKey;
        }

        public override IQueryable<TEntity> Execute(IQueryable<TEntity> query)
        {
            return Execute(query, 0);
        }

        public override IQueryable<TEntity> Execute(IQueryable<TEntity> query, int resultsPerPage)
        {
            //use of results per page is irrelevant in this query, so just ignore and return result.
            return query.Where(x => x.PartitionKey == partitionKey && x.RowKey == rowKey);
        }
    }
}
