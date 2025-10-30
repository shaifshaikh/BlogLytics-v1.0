using Bloglytics.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Bloglytics.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public DashboardController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }

        // GET: Dashboard/Index
        public async Task<IActionResult> Index()
        {
            // Check if user is logged in
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new DashboardViewModel
            {
                UserId = int.Parse(userId),
                UserName = HttpContext.Session.GetString("UserName"),
                UserEmail = HttpContext.Session.GetString("UserEmail"),
                UserRole = HttpContext.Session.GetString("UserRole")
            };

            try
            {
                // Get dashboard statistics
                model.Statistics = await GetDashboardStatisticsAsync(model.UserId, model.UserRole);

                // Get recent posts
                model.RecentPosts = await GetRecentPostsAsync(model.UserId, model.UserRole);

                // Get top performing posts
                model.TopPosts = await GetTopPerformingPostsAsync(model.UserId, model.UserRole);

                // Get recent comments
                model.RecentComments = await GetRecentCommentsAsync(model.UserId, model.UserRole);
            }
            catch (Exception ex)
            {
                // Log error
                TempData["ErrorMessage"] = "Error loading dashboard data.";
            }

            return View(model);
        }

        // Get Dashboard Statistics
        private async Task<DashboardStatistics> GetDashboardStatisticsAsync(int userId, string role)
        {
            var stats = new DashboardStatistics();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = role == "Admin"
                    ? @"
                        SELECT 
                            (SELECT COUNT(*) FROM BlogPosts WHERE Status = 'Published') AS TotalPosts,
                            (SELECT COUNT(*) FROM BlogPosts WHERE Status = 'Draft') AS DraftPosts,
                            (SELECT SUM(ViewCount) FROM BlogPosts) AS TotalViews,
                            (SELECT COUNT(*) FROM Comments WHERE IsApproved = 1) AS TotalComments,
                            (SELECT COUNT(*) FROM Users WHERE IsActive = 1) AS TotalUsers,
                            (SELECT COUNT(*) FROM BlogPosts WHERE Status = 'Published' AND PublishedAt >= DATEADD(DAY, -30, GETDATE())) AS PostsThisMonth
                    "
                    : @"
                        SELECT 
                            (SELECT COUNT(*) FROM BlogPosts WHERE AuthorId = @UserId AND Status = 'Published') AS TotalPosts,
                            (SELECT COUNT(*) FROM BlogPosts WHERE AuthorId = @UserId AND Status = 'Draft') AS DraftPosts,
                            (SELECT SUM(ViewCount) FROM BlogPosts WHERE AuthorId = @UserId) AS TotalViews,
                            (SELECT COUNT(*) FROM Comments WHERE PostId IN (SELECT PostId FROM BlogPosts WHERE AuthorId = @UserId) AND IsApproved = 1) AS TotalComments,
                            0 AS TotalUsers,
                            (SELECT COUNT(*) FROM BlogPosts WHERE AuthorId = @UserId AND Status = 'Published' AND PublishedAt >= DATEADD(DAY, -30, GETDATE())) AS PostsThisMonth
                    ";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (role != "Admin")
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                    }

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            stats.TotalPosts = reader["TotalPosts"] != DBNull.Value ? Convert.ToInt32(reader["TotalPosts"]) : 0;
                            stats.DraftPosts = reader["DraftPosts"] != DBNull.Value ? Convert.ToInt32(reader["DraftPosts"]) : 0;
                            stats.TotalViews = reader["TotalViews"] != DBNull.Value ? Convert.ToInt32(reader["TotalViews"]) : 0;
                            stats.TotalComments = reader["TotalComments"] != DBNull.Value ? Convert.ToInt32(reader["TotalComments"]) : 0;
                            stats.TotalUsers = reader["TotalUsers"] != DBNull.Value ? Convert.ToInt32(reader["TotalUsers"]) : 0;
                            stats.PostsThisMonth = reader["PostsThisMonth"] != DBNull.Value ? Convert.ToInt32(reader["PostsThisMonth"]) : 0;
                        }
                    }
                }
            }

            return stats;
        }

        // Get Recent Posts
        private async Task<List<PostSummary>> GetRecentPostsAsync(int userId, string role)
        {
            var posts = new List<PostSummary>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = role == "Admin"
                    ? @"
                        SELECT TOP 5 
                            bp.PostId, bp.Title, bp.Status, bp.ViewCount, bp.CreatedAt, bp.PublishedAt,
                            u.FullName AS AuthorName, c.CategoryName
                        FROM BlogPosts bp
                        INNER JOIN Users u ON bp.AuthorId = u.UserId
                        INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                        ORDER BY bp.CreatedAt DESC
                    "
                    : @"
                        SELECT TOP 5 
                            bp.PostId, bp.Title, bp.Status, bp.ViewCount, bp.CreatedAt, bp.PublishedAt,
                            u.FullName AS AuthorName, c.CategoryName
                        FROM BlogPosts bp
                        INNER JOIN Users u ON bp.AuthorId = u.UserId
                        INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                        WHERE bp.AuthorId = @UserId
                        ORDER BY bp.CreatedAt DESC
                    ";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (role != "Admin")
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                    }

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            posts.Add(new PostSummary
                            {
                                PostId = Convert.ToInt32(reader["PostId"]),
                                Title = reader["Title"].ToString(),
                                Status = reader["Status"].ToString(),
                                ViewCount = Convert.ToInt32(reader["ViewCount"]),
                                AuthorName = reader["AuthorName"].ToString(),
                                CategoryName = reader["CategoryName"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                                PublishedAt = reader["PublishedAt"] != DBNull.Value ? Convert.ToDateTime(reader["PublishedAt"]) : (DateTime?)null
                            });
                        }
                    }
                }
            }

            return posts;
        }

        // Get Top Performing Posts
        private async Task<List<PostSummary>> GetTopPerformingPostsAsync(int userId, string role)
        {
            var posts = new List<PostSummary>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = role == "Admin"
                    ? @"
                        SELECT TOP 5 
                            bp.PostId, bp.Title, bp.Status, bp.ViewCount, bp.CreatedAt, bp.PublishedAt,
                            u.FullName AS AuthorName, c.CategoryName
                        FROM BlogPosts bp
                        INNER JOIN Users u ON bp.AuthorId = u.UserId
                        INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                        WHERE bp.Status = 'Published'
                        ORDER BY bp.ViewCount DESC
                    "
                    : @"
                        SELECT TOP 5 
                            bp.PostId, bp.Title, bp.Status, bp.ViewCount, bp.CreatedAt, bp.PublishedAt,
                            u.FullName AS AuthorName, c.CategoryName
                        FROM BlogPosts bp
                        INNER JOIN Users u ON bp.AuthorId = u.UserId
                        INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                        WHERE bp.AuthorId = @UserId AND bp.Status = 'Published'
                        ORDER BY bp.ViewCount DESC
                    ";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (role != "Admin")
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                    }

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            posts.Add(new PostSummary
                            {
                                PostId = Convert.ToInt32(reader["PostId"]),
                                Title = reader["Title"].ToString(),
                                Status = reader["Status"].ToString(),
                                ViewCount = Convert.ToInt32(reader["ViewCount"]),
                                AuthorName = reader["AuthorName"].ToString(),
                                CategoryName = reader["CategoryName"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                                PublishedAt = reader["PublishedAt"] != DBNull.Value ? Convert.ToDateTime(reader["PublishedAt"]) : (DateTime?)null
                            });
                        }
                    }
                }
            }

            return posts;
        }

        // Get Recent Comments
        private async Task<List<CommentSummary>> GetRecentCommentsAsync(int userId, string role)
        {
            var comments = new List<CommentSummary>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = role == "Admin"
                    ? @"
                        SELECT TOP 5 
                            c.CommentId, c.Content, c.CreatedAt, c.IsApproved,
                            u.FullName AS CommenterName,
                            bp.Title AS PostTitle, bp.PostId
                        FROM Comments c
                        INNER JOIN Users u ON c.UserId = u.UserId
                        INNER JOIN BlogPosts bp ON c.PostId = bp.PostId
                        ORDER BY c.CreatedAt DESC
                    "
                    : @"
                        SELECT TOP 5 
                            c.CommentId, c.Content, c.CreatedAt, c.IsApproved,
                            u.FullName AS CommenterName,
                            bp.Title AS PostTitle, bp.PostId
                        FROM Comments c
                        INNER JOIN Users u ON c.UserId = u.UserId
                        INNER JOIN BlogPosts bp ON c.PostId = bp.PostId
                        WHERE bp.AuthorId = @UserId
                        ORDER BY c.CreatedAt DESC
                    ";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (role != "Admin")
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                    }

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            comments.Add(new CommentSummary
                            {
                                CommentId = Convert.ToInt32(reader["CommentId"]),
                                Content = reader["Content"].ToString(),
                                CommenterName = reader["CommenterName"].ToString(),
                                PostTitle = reader["PostTitle"].ToString(),
                                PostId = Convert.ToInt32(reader["PostId"]),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                                IsApproved = Convert.ToBoolean(reader["IsApproved"])
                            });
                        }
                    }
                }
            }

            return comments;
        }
    }

    // ViewModels

}