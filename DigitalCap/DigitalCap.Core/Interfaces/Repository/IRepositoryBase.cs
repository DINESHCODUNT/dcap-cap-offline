using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace DigitalCap.Core.Interfaces.Repository
{
    public interface IRepositoryBase<TEntity, TKey>
        where TEntity : IEntity<TKey>
    {
        Task<TKey> InsertAsync(TEntity entity);

        TKey Insert(TEntity entity);
        void SoftDeleteById(string spname, Guid id);

        void InsertMany(IEnumerable<TEntity> entity);

        int BulkInsert(IEnumerable<TEntity> entities);

        Task<bool> UpdateAsync(TEntity entity);

        bool Update(TEntity entity);
        void UpdateMany(List<TEntity> entity);

        Task<bool> DeleteAsync(TKey id);

        bool Delete(TKey id);

        void DeleteFiltered(Expression<Func<TEntity, bool>> filter);
        Task<List<string>> GetClassNumbers();
        Task<TEntity?> GetAsync(TKey id, bool includeDeleted = false, bool useTransaction = true);

        TEntity Get(TKey id, bool includeDeleted = false, bool useTransaction = true);

        Task<IQueryable<TEntity>> GetAllAsync(bool useTransaction = true);

        IQueryable<TEntity> GetAll(bool useTransaction = true);

        IQueryable<TEntity> GetFiltered(Expression<Func<TEntity, bool>> filter);

        Task<IQueryable<TEntity>> GetFilteredAsync(Expression<Func<TEntity, bool>> filter, bool IncludeDeleted = false);

        TEntity FirstOrDefault(Expression<Func<TEntity, bool>> filter);

        TEntity First(Expression<Func<TEntity, bool>> filter);

        IQueryable<TEntity> ExecuteQuery(string sql);

        IQueryable<TEntity> ExecuteQuery(string sql, object[] parameters);

        IQueryable<T> ExecuteStoreProc<T>(string sql, object parameters);
        Task<IQueryable<T>> ExecuteStoreProcAsync<T>(string sql, object parameters);

        /// <summary>
        /// Discards any changes made during the current transaction. If any other repositories share the transaction, their changes will also be discarded.
        /// </summary>
        void Rollback();

        /// <summary>
        /// Commits any changes made during the current transaction. If any other repositories share the transaction, their changes will also be committed.
        /// </summary>
        void Commit();
        void RemoveCacheKeysByValue(string[] keyValue, string uniqueId, string contextInfo = "");
        Task<bool> UpdateQueryExecutionAsync(TEntity entity);

    }
}
