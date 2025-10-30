using Bloglytics.DTO;
using Bloglytics.Models;
using Bloglytics.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;

namespace Bloglytics.Repository { 
    public class BlogRepository : IBlogRepository
    {
        private readonly string _connectionString;

        public BlogRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<IEnumerable<Blog>> GetAllPublishedBlogsAsync(int page = 1, int pageSize = 9)
        {
            var blogs = new List<Blog>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        bp.PostId, bp.Title, bp.Slug, bp.Summary, bp.FeaturedImage, 
                        bp.ViewCount, bp.PublishedAt, bp.CreatedAt,
                        u.FullName AS AuthorName,
                        c.CategoryName,
                        (SELECT COUNT(*) FROM Comments WHERE PostId = bp.PostId AND IsApproved = 1) AS CommentCount,
                        (SELECT COUNT(*) FROM UserEngagement WHERE PostId = bp.PostId AND ActionType = 'Like') AS LikeCount
                    FROM BlogPosts bp
                    INNER JOIN Users u ON bp.AuthorId = u.UserId
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    WHERE bp.Status = 'Published'
                    ORDER BY bp.PublishedAt DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            blogs.Add(MapBlogFromReader(reader));
                        }
                    }
                }
            }

            return blogs;
        }

        public async Task<IEnumerable<Blog>> GetBlogsByUserIdAsync(int userId)
        {
            var blogs = new List<Blog>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        bp.PostId, bp.Title, bp.Slug, bp.Summary, bp.FeaturedImage, 
                        bp.ViewCount, bp.Status, bp.PublishedAt, bp.CreatedAt,
                        c.CategoryName,
                        (SELECT COUNT(*) FROM Comments WHERE PostId = bp.PostId AND IsApproved = 1) AS CommentCount,
                        (SELECT COUNT(*) FROM UserEngagement WHERE PostId = bp.PostId AND ActionType = 'Like') AS LikeCount
                    FROM BlogPosts bp
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    WHERE bp.AuthorId = @UserId
                    ORDER BY bp.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var blog = MapBlogFromReader(reader);
                            blog.Status = reader["Status"].ToString();
                            blogs.Add(blog);
                        }
                    }
                }
            }

            return blogs;
        }

        public async Task<Blog> GetBlogByIdAsync(int blogId)
        {
            Blog blog = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        bp.PostId, bp.Title, bp.Slug, bp.Content, bp.Summary, bp.FeaturedImage, 
                        bp.AuthorId, bp.CategoryId, bp.ViewCount, bp.Status, bp.PublishedAt, bp.CreatedAt,
                        u.FullName AS AuthorName,
                        c.CategoryName,
                        (SELECT COUNT(*) FROM Comments WHERE PostId = bp.PostId AND IsApproved = 1) AS CommentCount,
                        (SELECT COUNT(*) FROM UserEngagement WHERE PostId = bp.PostId AND ActionType = 'Like') AS LikeCount
                    FROM BlogPosts bp
                    INNER JOIN Users u ON bp.AuthorId = u.UserId
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    WHERE bp.PostId = @PostId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", blogId);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            blog = new Blog
                            {
                                PostId = Convert.ToInt32(reader["PostId"]),
                                Title = reader["Title"].ToString(),
                                Slug = reader["Slug"].ToString(),
                                Content = reader["Content"].ToString(),
                                Summary = reader["Summary"]?.ToString(),
                                FeaturedImage = reader["FeaturedImage"]?.ToString(),
                                AuthorId = Convert.ToInt32(reader["AuthorId"]),
                                CategoryId = Convert.ToInt32(reader["CategoryId"]),
                                ViewCount = Convert.ToInt32(reader["ViewCount"]),
                                Status = reader["Status"].ToString(),
                                PublishedAt = reader["PublishedAt"] != DBNull.Value ? Convert.ToDateTime(reader["PublishedAt"]) : (DateTime?)null,
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                                AuthorName = reader["AuthorName"].ToString(),
                                CategoryName = reader["CategoryName"].ToString(),
                                CommentCount = reader["CommentCount"] != DBNull.Value ? Convert.ToInt32(reader["CommentCount"]) : 0,
                                LikeCount = reader["LikeCount"] != DBNull.Value ? Convert.ToInt32(reader["LikeCount"]) : 0
                            };
                        }
                    }
                }
            }

            return blog;
        }

        public async Task<Blog> GetBlogBySlugAsync(string slug)
        {
            Blog blog = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        bp.PostId, bp.Title, bp.Slug, bp.Content, bp.Summary, bp.FeaturedImage, 
                        bp.AuthorId, bp.CategoryId, bp.ViewCount, bp.Status, bp.PublishedAt, bp.CreatedAt,
                        u.FullName AS AuthorName,
                        c.CategoryName
                    FROM BlogPosts bp
                    INNER JOIN Users u ON bp.AuthorId = u.UserId
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    WHERE bp.Slug = @Slug AND bp.Status = 'Published'";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Slug", slug);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            blog = MapBlogFromReader(reader);
                        }
                    }
                }
            }

            return blog;
        }

        public async Task<int> CreateBlogAsync(Blog blog)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    INSERT INTO BlogPosts (Title, Slug, Content, Summary, FeaturedImage, AuthorId, CategoryId, Status, PublishedAt, CreatedAt)
                    VALUES (@Title, @Slug, @Content, @Summary, @FeaturedImage, @AuthorId, @CategoryId, @Status, @PublishedAt, @CreatedAt);
                    SELECT CAST(SCOPE_IDENTITY() as int)";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Title", blog.Title);
                    cmd.Parameters.AddWithValue("@Slug", blog.Slug);
                    cmd.Parameters.AddWithValue("@Content", blog.Content);
                    cmd.Parameters.AddWithValue("@Summary", blog.Summary ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@FeaturedImage", blog.FeaturedImage ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@AuthorId", blog.AuthorId);
                    cmd.Parameters.AddWithValue("@CategoryId", blog.CategoryId);
                    cmd.Parameters.AddWithValue("@Status", blog.Status);
                    cmd.Parameters.AddWithValue("@PublishedAt", blog.Status == "Published" ? DateTime.Now : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                    int postId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    return postId;
                }
            }
        }

        public async Task<bool> UpdateBlogAsync(Blog blog)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    UPDATE BlogPosts 
                    SET Title = @Title, Slug = @Slug, Content = @Content, Summary = @Summary, 
                        FeaturedImage = @FeaturedImage, CategoryId = @CategoryId, Status = @Status, 
                        UpdatedAt = @UpdatedAt, PublishedAt = @PublishedAt
                    WHERE PostId = @PostId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", blog.PostId);
                    cmd.Parameters.AddWithValue("@Title", blog.Title);
                    cmd.Parameters.AddWithValue("@Slug", blog.Slug);
                    cmd.Parameters.AddWithValue("@Content", blog.Content);
                    cmd.Parameters.AddWithValue("@Summary", blog.Summary ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@FeaturedImage", blog.FeaturedImage ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@CategoryId", blog.CategoryId);
                    cmd.Parameters.AddWithValue("@Status", blog.Status);
                    cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);
                    cmd.Parameters.AddWithValue("@PublishedAt", blog.Status == "Published" && blog.PublishedAt == null ? DateTime.Now : blog.PublishedAt ?? (object)DBNull.Value);

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<bool> DeleteBlogAsync(int blogId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "DELETE FROM BlogPosts WHERE PostId = @PostId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", blogId);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<IEnumerable<Blog>> GetTrendingBlogsAsync(int count = 10)
        {
            var blogs = new List<Blog>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT TOP (@Count)
                        bp.PostId, bp.Title, bp.Slug, bp.Summary, bp.FeaturedImage, 
                        bp.ViewCount, bp.PublishedAt,
                        u.FullName AS AuthorName,
                        c.CategoryName,
                        (SELECT COUNT(*) FROM Comments WHERE PostId = bp.PostId AND IsApproved = 1) AS CommentCount,
                        (SELECT COUNT(*) FROM UserEngagement WHERE PostId = bp.PostId AND ActionType = 'Like') AS LikeCount
                    FROM BlogPosts bp
                    INNER JOIN Users u ON bp.AuthorId = u.UserId
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    WHERE bp.Status = 'Published' AND bp.PublishedAt >= DATEADD(DAY, -30, GETDATE())
                    ORDER BY (bp.ViewCount * 0.6 + 
                             (SELECT COUNT(*) FROM UserEngagement WHERE PostId = bp.PostId AND ActionType = 'Like') * 0.4) DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Count", count);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            blogs.Add(MapBlogFromReader(reader));
                        }
                    }
                }
            }

            return blogs;
        }

        public async Task<bool> IncrementViewCountAsync(int blogId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "UPDATE BlogPosts SET ViewCount = ViewCount + 1 WHERE PostId = @PostId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", blogId);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<bool> LikeBlogAsync(int blogId, int userId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    IF NOT EXISTS (SELECT 1 FROM UserEngagement WHERE PostId = @PostId AND UserId = @UserId AND ActionType = 'Like')
                    BEGIN
                        INSERT INTO UserEngagement (UserId, PostId, ActionType, ActionDate)
                        VALUES (@UserId, @PostId, 'Like', GETDATE())
                    END";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", blogId);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    await cmd.ExecuteNonQueryAsync();
                    return true;
                }
            }
        }

        public async Task<bool> UnlikeBlogAsync(int blogId, int userId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "DELETE FROM UserEngagement WHERE PostId = @PostId AND UserId = @UserId AND ActionType = 'Like'";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", blogId);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<bool> HasUserLikedBlogAsync(int blogId, int userId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "SELECT COUNT(*) FROM UserEngagement WHERE PostId = @PostId AND UserId = @UserId AND ActionType = 'Like'";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", blogId);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }

        public async Task<int> GetLikesCountAsync(int blogId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "SELECT COUNT(*) FROM UserEngagement WHERE PostId = @PostId AND ActionType = 'Like'";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", blogId);
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        public async Task<int> GetTotalBlogsCountAsync()
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "SELECT COUNT(*) FROM BlogPosts WHERE Status = 'Published'";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        public async Task<int> GetTotalBlogsByUserIdAsync(int userId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "SELECT COUNT(*) FROM BlogPosts WHERE AuthorId = @UserId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        // Implement remaining methods...
        public async Task<IEnumerable<Blog>> GetBlogsByCategoryAsync(string categoryName, int page = 1, int pageSize = 9)
        {
            var blogs = new List<Blog>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        bp.PostId, bp.Title, bp.Slug, bp.Summary, bp.FeaturedImage, 
                        bp.ViewCount, bp.PublishedAt, bp.CreatedAt,
                        u.FullName AS AuthorName,
                        c.CategoryName,
                        (SELECT COUNT(*) FROM Comments WHERE PostId = bp.PostId AND IsApproved = 1) AS CommentCount,
                        (SELECT COUNT(*) FROM UserEngagement WHERE PostId = bp.PostId AND ActionType = 'Like') AS LikeCount
                    FROM BlogPosts bp
                    INNER JOIN Users u ON bp.AuthorId = u.UserId
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    WHERE bp.Status = 'Published' AND c.CategoryName = @CategoryName
                    ORDER BY bp.PublishedAt DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CategoryName", categoryName);
                    cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            blogs.Add(MapBlogFromReader(reader));
                        }
                    }
                }
            }

            return blogs;
        }

        public async Task<IEnumerable<Blog>> GetPopularBlogsAsync(int count = 5)
        {
            var blogs = new List<Blog>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT TOP (@Count)
                        bp.PostId, bp.Title, bp.Slug, bp.Summary, bp.FeaturedImage, 
                        bp.ViewCount, bp.PublishedAt,
                        u.FullName AS AuthorName,
                        c.CategoryName,
                        (SELECT COUNT(*) FROM Comments WHERE PostId = bp.PostId AND IsApproved = 1) AS CommentCount,
                        (SELECT COUNT(*) FROM UserEngagement WHERE PostId = bp.PostId AND ActionType = 'Like') AS LikeCount
                    FROM BlogPosts bp
                    INNER JOIN Users u ON bp.AuthorId = u.UserId
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    WHERE bp.Status = 'Published'
                    ORDER BY bp.ViewCount DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Count", count);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            blogs.Add(MapBlogFromReader(reader));
                        }
                    }
                }
            }

            return blogs;
        }

        public async Task<IEnumerable<Blog>> GetRecentBlogsAsync(int count = 5)
        {
            var blogs = new List<Blog>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT TOP (@Count)
                        bp.PostId, bp.Title, bp.Slug, bp.Summary, bp.FeaturedImage, 
                        bp.ViewCount, bp.PublishedAt, bp.CreatedAt,
                        u.FullName AS AuthorName,
                        c.CategoryName,
                        (SELECT COUNT(*) FROM Comments WHERE PostId = bp.PostId AND IsApproved = 1) AS CommentCount,
                        (SELECT COUNT(*) FROM UserEngagement WHERE PostId = bp.PostId AND ActionType = 'Like') AS LikeCount
                    FROM BlogPosts bp
                    INNER JOIN Users u ON bp.AuthorId = u.UserId
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    WHERE bp.Status = 'Published'
                    ORDER BY bp.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Count", count);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            blogs.Add(MapBlogFromReader(reader));
                        }
                    }
                }
            }

            return blogs;
        }

        public async Task<IEnumerable<Blog>> SearchBlogsAsync(string keyword, int page = 1, int pageSize = 9)
        {
            var blogs = new List<Blog>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        bp.PostId, bp.Title, bp.Slug, bp.Summary, bp.FeaturedImage, 
                        bp.ViewCount, bp.PublishedAt, bp.CreatedAt,
                        u.FullName AS AuthorName,
                        c.CategoryName,
                        (SELECT COUNT(*) FROM Comments WHERE PostId = bp.PostId AND IsApproved = 1) AS CommentCount,
                        (SELECT COUNT(*) FROM UserEngagement WHERE PostId = bp.PostId AND ActionType = 'Like') AS LikeCount
                    FROM BlogPosts bp
                    INNER JOIN Users u ON bp.AuthorId = u.UserId
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    WHERE bp.Status = 'Published' 
                      AND (bp.Title LIKE @Keyword OR bp.Summary LIKE @Keyword OR bp.Content LIKE @Keyword)
                    ORDER BY bp.PublishedAt DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Keyword", $"%{keyword}%");
                    cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            blogs.Add(MapBlogFromReader(reader));
                        }
                    }
                }
            }

            return blogs;
        }

        public async Task<IEnumerable<Blog>> GetAllBlogsForAdminAsync(int page = 1, int pageSize = 20)
        {
            // Get all blogs regardless of status
            return new List<Blog>();
        }

        public async Task<bool> ChangeStatusAsync(int blogId, string status)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "UPDATE BlogPosts SET Status = @Status WHERE PostId = @PostId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", blogId);
                    cmd.Parameters.AddWithValue("@Status", status);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        // Helper method to map from SqlDataReader to Blog
        private Blog MapBlogFromReader(SqlDataReader reader)
        {
            return new Blog
            {
                PostId = Convert.ToInt32(reader["PostId"]),
                Title = reader["Title"].ToString(),
                Slug = reader["Slug"].ToString(),
                Summary = reader["Summary"]?.ToString(),
                FeaturedImage = reader["FeaturedImage"]?.ToString(),
                ViewCount = Convert.ToInt32(reader["ViewCount"]),
                PublishedAt = reader["PublishedAt"] != DBNull.Value ? Convert.ToDateTime(reader["PublishedAt"]) : (DateTime?)null,
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.Now,
                AuthorName = reader["AuthorName"]?.ToString(),
                CategoryName = reader["CategoryName"].ToString(),
                CommentCount = reader["CommentCount"] != DBNull.Value ? Convert.ToInt32(reader["CommentCount"]) : 0,
                LikeCount = reader["LikeCount"] != DBNull.Value ? Convert.ToInt32(reader["LikeCount"]) : 0
            };
        }
    }
}
