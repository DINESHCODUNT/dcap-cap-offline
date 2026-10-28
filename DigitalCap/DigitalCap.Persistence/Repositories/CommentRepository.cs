using Dapper;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Models;
using DigitalCap.Persistence.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Transactions;

namespace DigitalCap.Persistence.Repositories
{
    public class CommentRepository : ICommentRepository
    {
        private readonly IUnitOfWork _unitOfWork;

        protected IDbConnection Connection => _unitOfWork.Connection;
        protected IDbTransaction Transaction => _unitOfWork.Transaction;

        public CommentRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }
        public void Commit()
        {
            _unitOfWork?.Commit();
        }

        // ------------------------------------------------------------------ offline (local SQLite)
        // Comments are downloaded with the project (dbo.DataTransferComments). The reference had no offline comment
        // path (its CommentService always called the SQL Server procedures), and the sync does not upload Comments,
        // so offline the list is read locally and create/update/delete are refused instead of being lost.

        private const string OfflineCommentWriteMessage =
            "Comments can only be added, edited or deleted online; changes made offline would not be synchronized.";

        private const string SqliteCommentColumns = @"
            SELECT c.Id, c.ProjectId, c.ProjectTaskId, c.CreatedBy AS UserId,
                   COALESCE(u.ProviderDisplayName, c.CreatedBy) AS CreatedBy, c.LastModifiedDate, c.Comment
            FROM Comments c
            LEFT JOIN AspNetUserLogins u ON c.CreatedBy = u.UserId";

        /// <summary>Project-level comments (no task), newest first.</summary>
        private const string SqliteReadCommentsByProjectId = SqliteCommentColumns + @"
            WHERE c.ProjectId = @ProjectId AND COALESCE(c.ProjectTaskId, 0) = 0 AND COALESCE(c.IsDeleted, 0) = 0
            ORDER BY c.LastModifiedDate DESC";

        private const string SqliteReadCommentsByTaskId = SqliteCommentColumns + @"
            WHERE c.ProjectTaskId = @ProjectTaskId AND COALESCE(c.IsDeleted, 0) = 0
            ORDER BY c.LastModifiedDate DESC";

        public async Task<IEnumerable<CommentModel>>GetCommentsByProjectIdAsync(int projectId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<CommentModel>(SqliteReadCommentsByProjectId, new { ProjectId = projectId }, Transaction);

            var result = await Connection.QueryAsync<CommentModel>(
                sql: "[CAP].[Read_Comments_By_Project_Id]",
                param: new { ProjectId = projectId },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            var comments = result.Select(c =>
            {
                c.UserId ??= c.CreatedBy;
                c.CreatedBy ??= c.UserId;
                return c;
            }).ToList();

            return comments;
        }

        public async Task<IEnumerable<CommentModel>>GetCommentsByProjectTaskIdAsync(int projectTaskId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<CommentModel>(SqliteReadCommentsByTaskId, new { ProjectTaskId = projectTaskId }, Transaction);

            var result = await Connection.QueryAsync<CommentModel>(
                sql: "[CAP].[Read_Comments_By_Task_Id]",
                param: new { ProjectTaskId = projectTaskId },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );
            var comments = result.Select(c =>
               {
                c.UserId ??= c.CreatedBy;
                c.CreatedBy ??= c.UserId;
                return c;
                }).ToList();

             return comments;
        }

        public async Task<IEnumerable<CommentModel>> CreateCommentAsync(int projectId,int? projectTaskId,
            string userId,string comment)
        {
            if (Connection.IsSqlite())
                throw new InvalidOperationException(OfflineCommentWriteMessage);

            var result = await Connection.QueryAsync<CommentModel>(
                        "[CAP].[Create_Comment]",
            new
                {
                    ProjectId = projectId,
                    ProjectTaskId = projectTaskId,
                    CreatedBy = userId,
                    Comment = comment
                },
            commandType: CommandType.StoredProcedure,
            transaction: Transaction
            );
            this.Commit();
            if (projectTaskId.HasValue)
                return await GetCommentsByProjectTaskIdAsync(projectTaskId.Value);

            return await GetCommentsByProjectIdAsync(projectId);
            
        }

       

        public async Task<IEnumerable<CommentModel>> UpdateCommentAsync(int commentId,int projectId,int? projectTaskId,
            string comment,string user)
        {
            if (Connection.IsSqlite())
                throw new InvalidOperationException(OfflineCommentWriteMessage);
           
            //  Execute update SP
            await Connection.ExecuteAsync(
                "[CAP].[Update_Comment]",
                new
                {
                    Id = commentId,
                    ProjectId = projectId,
                    Comment = comment,
                    UpdatedBy = user
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            // Re-fetch updated comments list
            IEnumerable<CommentModel> comments;

            if (projectTaskId.HasValue)
            {
                comments = await Connection.QueryAsync<CommentModel>(
                    "[CAP].[Read_Comments_By_Task_Id]",
                    new { ProjectTaskId = projectTaskId.Value },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );
            }
            else
            {
                comments = await Connection.QueryAsync<CommentModel>(
                    "[CAP].[Read_Comments_By_Project_Id]",
                    new { ProjectId = projectId },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );
            }
                var commentList = comments.Select(c =>
                {
                    c.UserId ??= c.CreatedBy;
                    c.CreatedBy ??= c.UserId;
                    return c;
                }).ToList();
            
            return commentList;

        }


        public async Task<IEnumerable<CommentModel>>DeleteCommentAsync(int commentId,int projectId,string comment,string user)
        {
            if (Connection.IsSqlite())
                throw new InvalidOperationException(OfflineCommentWriteMessage);

            var result = await Connection.QueryAsync<CommentModel>(
                sql: "[CAP].[Delete_Comment]",
                param: new
                {
                    Id = commentId,
                    ProjectId = projectId,
                    Comment = comment,
                    DeletedBy = user
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            var commentList = result.Select(c =>
            {
                c.UserId ??= c.CreatedBy;
                c.CreatedBy ??= c.UserId;
                return c;
            }).ToList();

            return commentList;
        }
    }
}

