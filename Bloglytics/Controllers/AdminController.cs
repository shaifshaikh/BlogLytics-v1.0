using Bloglytics.DTO;
using Bloglytics.Models;
using Bloglytics.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Bloglytics.Controllers
{
    public class AdminController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IBlogRepository _blogRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ICommentRepository _commentRepository;
        private readonly string _connectionString;

        public AdminController(
            IConfiguration configuration,
            IBlogRepository blogRepository,
            ICategoryRepository categoryRepository,
            ICommentRepository commentRepository)
        {
            _configuration = configuration;
            _blogRepository = blogRepository;
            _categoryRepository = categoryRepository;
            _commentRepository = commentRepository;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }

        // Check if user is admin
        private bool IsAdmin()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            return userRole == "Admin";
        }

        // GET: Admin/Index - Admin Dashboard
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsAdmin())
            {
                TempData["ErrorMessage"] = "Access denied. Admin only.";
                return RedirectToAction("Index", "Dashboard");
            }

            try
            {
                var model = new AdminDashboardViewModel
                {
                    TotalUsers = await GetTotalUsersAsync(),
                    TotalBlogs = await GetTotalBlogsAsync(),
                    TotalPublished = await GetTotalPublishedBlogsAsync(),
                    TotalDrafts = await GetTotalDraftBlogsAsync(),
                    TotalComments = await GetTotalCommentsAsync(),
                    PendingComments = await GetPendingCommentsCountAsync(),
                    TotalCategories = await GetTotalCategoriesAsync(),
                    TotalViews = await GetTotalViewsAsync()
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading admin dashboard.";
                return View(new AdminDashboardViewModel());
            }
        }

        // GET: Admin/ManageBlogs - View and manage all blogs
        [HttpGet]
        public async Task<IActionResult> ManageBlogs(int page = 1, string status = null, string search = null)
        {
            if (!IsAdmin())
            {
                TempData["ErrorMessage"] = "Access denied. Admin only.";
                return RedirectToAction("Index", "Dashboard");
            }

            try
            {
                int pageSize = 20;
                var model = new ManageBlogsViewModel
                {
                    CurrentPage = page,
                    SelectedStatus = status,
                    SearchQuery = search
                };

                // Get all blogs based on filters
                model.Blogs = await GetBlogsForAdminAsync(page, pageSize, status, search);
                model.TotalBlogs = await GetBlogsCountForAdminAsync(status, search);
                model.TotalPages = (int)Math.Ceiling((double)model.TotalBlogs / pageSize);

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading blogs.";
                return View(new ManageBlogsViewModel());
            }
        }

        // GET: Admin/ManageUsers - View and manage all users
        [HttpGet]
        public async Task<IActionResult> ManageUsers()
        {
            if (!IsAdmin())
            {
                TempData["ErrorMessage"] = "Access denied. Admin only.";
                return RedirectToAction("Index", "Dashboard");
            }

            try
            {
                var users = await GetAllUsersAsync();
                return View(users);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading users.";
                return View(new List<AdminUserDto>());
            }
        }

        // GET: Admin/ManageCategories - View and manage categories
        [HttpGet]
        public async Task<IActionResult> ManageCategories()
        {
            if (!IsAdmin())
            {
                TempData["ErrorMessage"] = "Access denied. Admin only.";
                return RedirectToAction("Index", "Dashboard");
            }

            try
            {
                var categories = await _categoryRepository.GetAllCategoriesAsync();
                return View(categories.ToList());
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading categories.";
                return View(new List<Category>());
            }
        }

        // GET: Admin/ManageComments - Approve/reject comments
        [HttpGet]
        public async Task<IActionResult> ManageComments()
        {
            if (!IsAdmin())
            {
                TempData["ErrorMessage"] = "Access denied. Admin only.";
                return RedirectToAction("Index", "Dashboard");
            }

            try
            {
                var pendingComments = await _commentRepository.GetPendingCommentsAsync();
                return View(pendingComments.ToList());
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading comments.";
                return View(new List<Comment>());
            }
        }

        // POST: Admin/DeleteBlog - Admin can delete any blog
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBlog(int id)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Access denied" });
            }

            try
            {
                var blog = await _blogRepository.GetBlogByIdAsync(id);
                if (blog == null)
                {
                    return Json(new { success = false, message = "Blog not found" });
                }

                // Delete blog
                bool deleted = await _blogRepository.DeleteBlogAsync(id);

                if (deleted)
                {
                    return Json(new { success = true, message = "Blog deleted successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to delete blog" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // POST: Admin/ChangeUserStatus - Activate/deactivate user
        [HttpPost]
        public async Task<IActionResult> ChangeUserStatus(int userId, bool isActive)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Access denied" });
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string query = "UPDATE Users SET IsActive = @IsActive WHERE UserId = @UserId";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@IsActive", isActive);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                return Json(new { success = true, message = "User status updated" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // POST: Admin/ApproveComment
        [HttpPost]
        public async Task<IActionResult> ApproveComment(int commentId)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Access denied" });
            }

            try
            {
                bool approved = await _commentRepository.ApproveCommentAsync(commentId);
                return Json(new { success = approved, message = approved ? "Comment approved" : "Failed to approve" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // POST: Admin/DeleteComment
        [HttpPost]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Access denied" });
            }

            try
            {
                bool deleted = await _commentRepository.DeleteCommentAsync(commentId);
                return Json(new { success = deleted, message = deleted ? "Comment deleted" : "Failed to delete" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // POST: Admin/AddCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory(string categoryName, string description)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Access denied" });
            }

            try
            {
                var category = new Category
                {
                    CategoryName = categoryName,
                    Description = description,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                int categoryId = await _categoryRepository.CreateCategoryAsync(category);
                return Json(new { success = true, message = "Category added successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // POST: Admin/DeleteCategory
        [HttpPost]
        public async Task<IActionResult> DeleteCategory(int categoryId)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Access denied" });
            }

            try
            {
                bool deleted = await _categoryRepository.DeleteCategoryAsync(categoryId);
                return Json(new { success = deleted, message = deleted ? "Category deleted" : "Failed to delete" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // Helper methods
        private async Task<int> GetTotalUsersAsync()
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT COUNT(*) FROM Users WHERE IsActive = 1";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        private async Task<int> GetTotalBlogsAsync()
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT COUNT(*) FROM BlogPosts";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        private async Task<int> GetTotalPublishedBlogsAsync()
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

        private async Task<int> GetTotalDraftBlogsAsync()
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT COUNT(*) FROM BlogPosts WHERE Status = 'Draft'";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        private async Task<int> GetTotalCommentsAsync()
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT COUNT(*) FROM Comments";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        private async Task<int> GetPendingCommentsCountAsync()
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT COUNT(*) FROM Comments WHERE IsApproved = 0";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        private async Task<int> GetTotalCategoriesAsync()
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT COUNT(*) FROM Categories WHERE IsActive = 1";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        private async Task<int> GetTotalViewsAsync()
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT SUM(ViewCount) FROM BlogPosts";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    return result != DBNull.Value ? Convert.ToInt32(result) : 0;
                }
            }
        }

        private async Task<List<Blog>> GetBlogsForAdminAsync(int page, int pageSize, string status, string search)
        {
            var blogs = new List<Blog>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                var conditions = new List<string>();
                if (!string.IsNullOrEmpty(status))
                {
                    conditions.Add("bp.Status = @Status");
                }
                if (!string.IsNullOrEmpty(search))
                {
                    conditions.Add("(bp.Title LIKE @Search OR u.FullName LIKE @Search)");
                }

                string whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";

                string query = $@"
                    SELECT 
                        bp.PostId, bp.Title, bp.Slug, bp.Status, bp.ViewCount, bp.CreatedAt, bp.PublishedAt,
                        u.FullName AS AuthorName,
                        c.CategoryName,
                        (SELECT COUNT(*) FROM Comments WHERE PostId = bp.PostId) AS CommentCount
                    FROM BlogPosts bp
                    INNER JOIN Users u ON bp.AuthorId = u.UserId
                    INNER JOIN Categories c ON bp.CategoryId = c.CategoryId
                    {whereClause}
                    ORDER BY bp.CreatedAt DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (!string.IsNullOrEmpty(status))
                        cmd.Parameters.AddWithValue("@Status", status);
                    if (!string.IsNullOrEmpty(search))
                        cmd.Parameters.AddWithValue("@Search", $"%{search}%");

                    cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            blogs.Add(new Blog
                            {
                                PostId = Convert.ToInt32(reader["PostId"]),
                                Title = reader["Title"].ToString(),
                                Slug = reader["Slug"].ToString(),
                                Status = reader["Status"].ToString(),
                                ViewCount = Convert.ToInt32(reader["ViewCount"]),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                                PublishedAt = reader["PublishedAt"] != DBNull.Value ? Convert.ToDateTime(reader["PublishedAt"]) : (DateTime?)null,
                                AuthorName = reader["AuthorName"].ToString(),
                                CategoryName = reader["CategoryName"].ToString(),
                                CommentCount = Convert.ToInt32(reader["CommentCount"])
                            });
                        }
                    }
                }
            }

            return blogs;
        }

        private async Task<int> GetBlogsCountForAdminAsync(string status, string search)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                var conditions = new List<string>();
                if (!string.IsNullOrEmpty(status))
                {
                    conditions.Add("bp.Status = @Status");
                }
                if (!string.IsNullOrEmpty(search))
                {
                    conditions.Add("(bp.Title LIKE @Search OR u.FullName LIKE @Search)");
                }

                string whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";

                string query = $@"
                    SELECT COUNT(*) 
                    FROM BlogPosts bp
                    INNER JOIN Users u ON bp.AuthorId = u.UserId
                    {whereClause}";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (!string.IsNullOrEmpty(status))
                        cmd.Parameters.AddWithValue("@Status", status);
                    if (!string.IsNullOrEmpty(search))
                        cmd.Parameters.AddWithValue("@Search", $"%{search}%");

                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        private async Task<List<AdminUserDto>> GetAllUsersAsync()
        {
            var users = new List<AdminUserDto>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        u.UserId, u.Email, u.FullName, u.Role, u.IsActive, u.CreatedAt, u.LastLoginAt,
                        (SELECT COUNT(*) FROM BlogPosts WHERE AuthorId = u.UserId) AS TotalBlogs,
                        (SELECT COUNT(*) FROM Comments WHERE UserId = u.UserId) AS TotalComments
                    FROM Users u
                    ORDER BY u.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            users.Add(new AdminUserDto
                            {
                                UserId = Convert.ToInt32(reader["UserId"]),
                                Email = reader["Email"].ToString(),
                                FullName = reader["FullName"].ToString(),
                                Role = reader["Role"].ToString(),
                                IsActive = Convert.ToBoolean(reader["IsActive"]),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                                LastLoginAt = reader["LastLoginAt"] != DBNull.Value ? Convert.ToDateTime(reader["LastLoginAt"]) : (DateTime?)null,
                                TotalBlogs = Convert.ToInt32(reader["TotalBlogs"]),
                                TotalComments = Convert.ToInt32(reader["TotalComments"])
                            });
                        }
                    }
                }
            }

            return users;
        }
    }

    // ViewModels
  
}