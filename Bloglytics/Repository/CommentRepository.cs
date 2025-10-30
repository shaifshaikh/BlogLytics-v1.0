using Bloglytics.DTO;
using Bloglytics.Repository;
using Microsoft.Data.SqlClient;

namespace Bloglytics.Repository
{
    public class CommentRepository : ICommentRepository
    {
        private readonly string _connectionString;

        public CommentRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<IEnumerable<Comment>> GetCommentsByBlogIdAsync(int blogId)
        {
            var comments = new List<Comment>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        c.CommentId, c.PostId, c.UserId, c.Content, c.ParentCommentId, 
                        c.IsApproved, c.CreatedAt,
                        u.FullName AS CommenterName
                    FROM Comments c
                    INNER JOIN Users u ON c.UserId = u.UserId
                    WHERE c.PostId = @PostId AND c.IsApproved = 1
                    ORDER BY c.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", blogId);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            comments.Add(new Comment
                            {
                                CommentId = Convert.ToInt32(reader["CommentId"]),
                                PostId = Convert.ToInt32(reader["PostId"]),
                                UserId = Convert.ToInt32(reader["UserId"]),
                                Content = reader["Content"].ToString(),
                                ParentCommentId = reader["ParentCommentId"] != DBNull.Value ? Convert.ToInt32(reader["ParentCommentId"]) : (int?)null,
                                IsApproved = Convert.ToBoolean(reader["IsApproved"]),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                                CommenterName = reader["CommenterName"].ToString()
                            });
                        }
                    }
                }
            }

            return comments;
        }

        public async Task<IEnumerable<Comment>> GetPendingCommentsAsync()
        {
            var comments = new List<Comment>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        c.CommentId, c.PostId, c.UserId, c.Content, c.ParentCommentId, 
                        c.IsApproved, c.CreatedAt,
                        u.FullName AS CommenterName,
                        bp.Title AS PostTitle
                    FROM Comments c
                    INNER JOIN Users u ON c.UserId = u.UserId
                    INNER JOIN BlogPosts bp ON c.PostId = bp.PostId
                    WHERE c.IsApproved = 0
                    ORDER BY c.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            comments.Add(new Comment
                            {
                                CommentId = Convert.ToInt32(reader["CommentId"]),
                                PostId = Convert.ToInt32(reader["PostId"]),
                                UserId = Convert.ToInt32(reader["UserId"]),
                                Content = reader["Content"].ToString(),
                                ParentCommentId = reader["ParentCommentId"] != DBNull.Value ? Convert.ToInt32(reader["ParentCommentId"]) : (int?)null,
                                IsApproved = Convert.ToBoolean(reader["IsApproved"]),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                                CommenterName = reader["CommenterName"].ToString(),
                                PostTitle = reader["PostTitle"].ToString()
                            });
                        }
                    }
                }
            }

            return comments;
        }

        public async Task<Comment> GetCommentByIdAsync(int commentId)
        {
            Comment comment = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        c.CommentId, c.PostId, c.UserId, c.Content, c.ParentCommentId, 
                        c.IsApproved, c.CreatedAt,
                        u.FullName AS CommenterName
                    FROM Comments c
                    INNER JOIN Users u ON c.UserId = u.UserId
                    WHERE c.CommentId = @CommentId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CommentId", commentId);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            comment = new Comment
                            {
                                CommentId = Convert.ToInt32(reader["CommentId"]),
                                PostId = Convert.ToInt32(reader["PostId"]),
                                UserId = Convert.ToInt32(reader["UserId"]),
                                Content = reader["Content"].ToString(),
                                ParentCommentId = reader["ParentCommentId"] != DBNull.Value ? Convert.ToInt32(reader["ParentCommentId"]) : (int?)null,
                                IsApproved = Convert.ToBoolean(reader["IsApproved"]),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                                CommenterName = reader["CommenterName"].ToString()
                            };
                        }
                    }
                }
            }

            return comment;
        }

        public async Task<int> CreateCommentAsync(Comment comment)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    INSERT INTO Comments (PostId, UserId, Content, ParentCommentId, IsApproved, CreatedAt)
                    VALUES (@PostId, @UserId, @Content, @ParentCommentId, @IsApproved, @CreatedAt);
                    SELECT CAST(SCOPE_IDENTITY() as int)";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", comment.PostId);
                    cmd.Parameters.AddWithValue("@UserId", comment.UserId);
                    cmd.Parameters.AddWithValue("@Content", comment.Content);
                    cmd.Parameters.AddWithValue("@ParentCommentId", comment.ParentCommentId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsApproved", comment.IsApproved);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                    int commentId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    return commentId;
                }
            }
        }

        public async Task<bool> UpdateCommentAsync(Comment comment)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    UPDATE Comments 
                    SET Content = @Content, IsApproved = @IsApproved
                    WHERE CommentId = @CommentId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CommentId", comment.CommentId);
                    cmd.Parameters.AddWithValue("@Content", comment.Content);
                    cmd.Parameters.AddWithValue("@IsApproved", comment.IsApproved);

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<bool> DeleteCommentAsync(int commentId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "DELETE FROM Comments WHERE CommentId = @CommentId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CommentId", commentId);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<bool> ApproveCommentAsync(int commentId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "UPDATE Comments SET IsApproved = 1 WHERE CommentId = @CommentId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CommentId", commentId);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<bool> RejectCommentAsync(int commentId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "UPDATE Comments SET IsApproved = 0 WHERE CommentId = @CommentId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CommentId", commentId);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<int> GetCommentsCountByBlogIdAsync(int blogId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "SELECT COUNT(*) FROM Comments WHERE PostId = @PostId AND IsApproved = 1";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", blogId);
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }
    }
}