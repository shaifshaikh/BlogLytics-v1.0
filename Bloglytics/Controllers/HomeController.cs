using Bloglytics.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Bloglytics.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public HomeController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }

        // GET: Home/Index - Public Blog Homepage
        public async Task<IActionResult> Index(int page = 1, int pageSize = 6, string category = null, string search = null)
        {
            var model = new HomeViewModel
            {
                CurrentPage = page,
                PageSize = pageSize,
                SelectedCategory = category,
                SearchQuery = search
            };

            try
            {
                // Get all categories for filter
                model.Categories = await GetCategoriesAsync();

                // Get featured posts
                model.FeaturedPosts = await GetFeaturedPostsAsync();

                // Get blog posts with pagination
                var (posts, totalCount) = await GetBlogPostsAsync(page, pageSize, category, search);
                model.BlogPosts = posts;
                model.TotalPosts = totalCount;
                model.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                // Get popular posts
                model.PopularPosts = await GetPopularPostsAsync();
            }
            catch (Exception ex)
            {
                // Log error
                TempData["ErrorMessage"] = "Error loading blog posts.";
            }

            return View(model);
        }

        // GET: Home/Post/{id} - Single Blog Post
        public async Task<IActionResult> Post(int id)
        {
            try
            {
                var post = await GetBlogPostByIdAsync(id);

                if (post == null)
                {
                    return NotFound();
                }

                // Increment view count
                await IncrementViewCountAsync(id);

                // Get related posts
                post.RelatedPosts = await GetRelatedPostsAsync(post.CategoryId, id);

                // Get comments
                post.Comments = await GetPostCommentsAsync(id);

                return View(post);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading blog post.";
                return RedirectToAction("Index");
            }
        }

        // Get Categories
        private async Task<List<CategoryDto>> GetCategoriesAsync()
        {
            var categories = new List<CategoryDto>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT c.CategoryId, c.CategoryName, COUNT(bp.PostId) AS PostCount
                    FROM Categories c
                    LEFT JOIN BlogPosts bp ON c.CategoryId = bp.CategoryId AND bp.Status = 'Published'
                    WHERE c.IsActive = 1
                    GROUP BY c.CategoryId, c.CategoryName
                    ORDER BY c.CategoryName";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            categories.Add(new CategoryDto
                            {
                                CategoryId = Convert.ToInt32(reader["CategoryId"]),
                                CategoryName = reader["CategoryName"].ToString(),
                                PostCount = Convert.ToInt32(reader["PostCount"])
                            });
                        }
                    }
                }
            }

            return categories;
        }

        // Get Featured Posts
        private async Task<List<BlogPostDto>> GetFeaturedPostsAsync()
        {
            var posts = new List<BlogPostDto>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT TOP 3
                        bp.PostId, bp.Title, bp.Slug, bp.Summary, bp.FeaturedImage, 
                        bp.ViewCount, bp.PublishedAt,
                        u.FullName AS AuthorName,
                        c.CategoryName,
                        (SELECT COUNT(*) FROM Comments WHERE PostId = bp.PostId AND IsApproved = 1) AS CommentCount
                    FROM BlogPosts bp
                    INNER JOIN Users u ON bp.AuthorId = u.UserId
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    WHERE bp.Status = 'Published'
                    ORDER BY bp.ViewCount DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            posts.Add(MapBlogPost(reader));
                        }
                    }
                }
            }

            return posts;
        }

        // Get Blog Posts with Pagination
        private async Task<(List<BlogPostDto>, int)> GetBlogPostsAsync(int page, int pageSize, string category, string search)
        {
            var posts = new List<BlogPostDto>();
            int totalCount = 0;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Build query with filters
                var queryConditions = "bp.Status = 'Published'";

                if (!string.IsNullOrEmpty(category))
                {
                    queryConditions += " AND c.CategoryName = @Category";
                }

                if (!string.IsNullOrEmpty(search))
                {
                    queryConditions += " AND (bp.Title LIKE @Search OR bp.Summary LIKE @Search)";
                }

                // Get total count
                string countQuery = $@"
                    SELECT COUNT(*) 
                    FROM BlogPosts bp
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    WHERE {queryConditions}";

                using (SqlCommand cmd = new SqlCommand(countQuery, conn))
                {
                    if (!string.IsNullOrEmpty(category))
                        cmd.Parameters.AddWithValue("@Category", category);

                    if (!string.IsNullOrEmpty(search))
                        cmd.Parameters.AddWithValue("@Search", $"%{search}%");

                    totalCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // Get paginated posts
                string query = $@"
                    SELECT 
                        bp.PostId, bp.Title, bp.Slug, bp.Summary, bp.FeaturedImage, 
                        bp.ViewCount, bp.PublishedAt,
                        u.FullName AS AuthorName,
                        c.CategoryName,
                        (SELECT COUNT(*) FROM Comments WHERE PostId = bp.PostId AND IsApproved = 1) AS CommentCount
                    FROM BlogPosts bp
                    INNER JOIN Users u ON bp.AuthorId = u.UserId
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    WHERE {queryConditions}
                    ORDER BY bp.PublishedAt DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (!string.IsNullOrEmpty(category))
                        cmd.Parameters.AddWithValue("@Category", category);

                    if (!string.IsNullOrEmpty(search))
                        cmd.Parameters.AddWithValue("@Search", $"%{search}%");

                    cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            posts.Add(MapBlogPost(reader));
                        }
                    }
                }
            }

            return (posts, totalCount);
        }

        // Get Popular Posts
        private async Task<List<BlogPostDto>> GetPopularPostsAsync()
        {
            var posts = new List<BlogPostDto>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT TOP 5
                        bp.PostId, bp.Title, bp.Slug, bp.ViewCount, bp.PublishedAt,
                        c.CategoryName
                    FROM BlogPosts bp
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    WHERE bp.Status = 'Published'
                    ORDER BY bp.ViewCount DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            posts.Add(new BlogPostDto
                            {
                                PostId = Convert.ToInt32(reader["PostId"]),
                                Title = reader["Title"].ToString(),
                                Slug = reader["Slug"].ToString(),
                                ViewCount = Convert.ToInt32(reader["ViewCount"]),
                                PublishedAt = Convert.ToDateTime(reader["PublishedAt"]),
                                CategoryName = reader["CategoryName"].ToString()
                            });
                        }
                    }
                }
            }

            return posts;
        }

        // Get Single Blog Post
        private async Task<BlogPostDetailDto> GetBlogPostByIdAsync(int postId)
        {
            BlogPostDetailDto post = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        bp.PostId, bp.Title, bp.Slug, bp.Content, bp.Summary, bp.FeaturedImage, 
                        bp.ViewCount, bp.PublishedAt, bp.CategoryId,
                        u.FullName AS AuthorName, u.Bio AS AuthorBio,
                        c.CategoryName
                    FROM BlogPosts bp
                    INNER JOIN Users u ON bp.AuthorId = u.UserId
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    WHERE bp.PostId = @PostId AND bp.Status = 'Published'";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", postId);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            post = new BlogPostDetailDto
                            {
                                PostId = Convert.ToInt32(reader["PostId"]),
                                Title = reader["Title"].ToString(),
                                Slug = reader["Slug"].ToString(),
                                Content = reader["Content"].ToString(),
                                Summary = reader["Summary"]?.ToString(),
                                FeaturedImage = reader["FeaturedImage"]?.ToString(),
                                ViewCount = Convert.ToInt32(reader["ViewCount"]),
                                PublishedAt = Convert.ToDateTime(reader["PublishedAt"]),
                                CategoryId = Convert.ToInt32(reader["CategoryId"]),
                                CategoryName = reader["CategoryName"].ToString(),
                                AuthorName = reader["AuthorName"].ToString(),
                                AuthorBio = reader["AuthorBio"]?.ToString()
                            };
                        }
                    }
                }
            }

            return post;
        }

        // Get Related Posts
        private async Task<List<BlogPostDto>> GetRelatedPostsAsync(int categoryId, int excludePostId)
        {
            var posts = new List<BlogPostDto>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT TOP 3
                        bp.PostId, bp.Title, bp.Slug, bp.Summary, bp.FeaturedImage, 
                        bp.ViewCount, bp.PublishedAt,
                        u.FullName AS AuthorName,
                        c.CategoryName
                    FROM BlogPosts bp
                    INNER JOIN Users u ON bp.AuthorId = u.UserId
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    WHERE bp.CategoryId = @CategoryId AND bp.PostId != @ExcludePostId AND bp.Status = 'Published'
                    ORDER BY bp.PublishedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CategoryId", categoryId);
                    cmd.Parameters.AddWithValue("@ExcludePostId", excludePostId);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            posts.Add(MapBlogPost(reader));
                        }
                    }
                }
            }

            return posts;
        }

        // Get Post Comments
        private async Task<List<CommentDto>> GetPostCommentsAsync(int postId)
        {
            var comments = new List<CommentDto>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        c.CommentId, c.Content, c.CreatedAt,
                        u.FullName AS CommenterName
                    FROM Comments c
                    INNER JOIN Users u ON c.UserId = u.UserId
                    WHERE c.PostId = @PostId AND c.IsApproved = 1 AND c.ParentCommentId IS NULL
                    ORDER BY c.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", postId);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            comments.Add(new CommentDto
                            {
                                CommentId = Convert.ToInt32(reader["CommentId"]),
                                Content = reader["Content"].ToString(),
                                CommenterName = reader["CommenterName"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                            });
                        }
                    }
                }
            }

            return comments;
        }

        // Increment View Count
        private async Task IncrementViewCountAsync(int postId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "UPDATE BlogPosts SET ViewCount = ViewCount + 1 WHERE PostId = @PostId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", postId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // Helper method to map blog post
        private BlogPostDto MapBlogPost(SqlDataReader reader)
        {
            return new BlogPostDto
            {
                PostId = Convert.ToInt32(reader["PostId"]),
                Title = reader["Title"].ToString(),
                Slug = reader["Slug"].ToString(),
                Summary = reader["Summary"]?.ToString(),
                FeaturedImage = reader["FeaturedImage"]?.ToString(),
                ViewCount = Convert.ToInt32(reader["ViewCount"]),
                PublishedAt = Convert.ToDateTime(reader["PublishedAt"]),
                AuthorName = reader["AuthorName"].ToString(),
                CategoryName = reader["CategoryName"].ToString(),
                CommentCount = reader["CommentCount"] != DBNull.Value ? Convert.ToInt32(reader["CommentCount"]) : 0
            };
        }
    }

    // ViewModels
    
}